# Plugin stability: SMTC session identity, render lock, trim, dead code

Date: 2026-09-15  
Status: draft for review  
Scope: one focused pass on `dev`. No Mac artwork pipeline rewrite (`sips` stays).

## Problem

Windows SMTC tracking, key image publishing, and an idle-file dialog can fail in ways that look like “the plugin froze” or “the cover did not update”. The repo also carries unused types and a Swift tree that the Mac build never ships.

## Goals

- Keep one subscription per live SMTC COM identity across `GetSessions()` RCW churn.
- Publish a new key image when cover or app icon bytes change, even if title/artist and payload length stay the same.
- Stop holding `ImagePipelineCache`’s lock during PNG encode.
- Keep Newtonsoft/BarRaider settings types visible to the trimmer.
- Stop joining the plugin thread to another process’s input queue when picking an idle image.
- Delete confirmed unused code.

## Out of scope

- Replacing macOS `sips` with ImageSharp.
- Wiring `KeyImageDecision` into `NowPlayingAction` (delete it instead).
- Priority queues or SMTC quarantine beyond the existing `SemaphoreSlim` in `InvokeCoreAsync`.
- Changing `RefreshLoop` timings.

## Architecture

No new runtime services. Existing types keep their roles:

- `WindowsMediaManager` owns SMTC sessions, signatures, and WinRT calls (already gated in `InvokeCoreAsync`).
- `ImagePipeline` / `ImagePipelineCache` own decoded cover bitmaps.
- `NativeIdleImagePicker` (Windows) owns the OpenFile dialog.
- Trimmer roots live in `src/StreamDeckCurrentMedia.csproj`.

## 1. Session identity

Today `_entries`, `_propertyChanges`, and the `live` set in `SyncSessions` use `ReferenceEqualityComparer.Instance`. A new RCW for the same `IUnknown` looks like a new session: extra `MediaPropertiesChanged` / `PlaybackInfoChanged` handlers, stale entries treated as removed.

Change all three to `EqualityComparer<SmtcSession>.Default` (CsWinRT COM identity). Do not key by `SourceAppUserModelId`: Chrome and other hosts can expose multiple sessions with the same AUMID.

Behavior stays: subscribe once per identity; detach when `GetSessions()` no longer returns that identity.

## 2. Publish signature

`BuildSignature` currently uses `CoverArtBase64.Length` and `AppIconBase64.Length`. Same title/artist plus same byte length (or both empty) suppresses a real art change.

Replace both length fields with 32-bit FNV-1a. Helper in `src/Core` (for example `Fnv1a.Hash32`). Signature: offset basis `2166136261`, prime `16777619`. Input is `ReadOnlySpan<char>` (and a `string` overload that forwards `.AsSpan()`). Base64 is ASCII-only: hash each character as `(byte)c`. Do **not** call `Encoding.UTF8.GetBytes` or allocate a byte array.

Format the digest as invariant lowercase hex (`x8`) in the signature. Tests: empty span hashes to `811c9dc5`; two equal-length different strings differ; hashing `"A"` does not allocate (or at least does not go through UTF-8 encoding).

Leave `IsActive`, `Status`, title, artist, album fields as they are. Do not put playback position into the signature.

## 3. Image cache lock

`RunWithBitmaps` holds `_lock` through `OverlayRenderer.Apply` and `ToPngDataUri`. Four Now Playing keys serialize on PNG compress.

Under the lock: clone the cached cover for the requested position/crop, and clone `Icon` if the overlay will read it. Dispose/update of `_bitmaps` remains under the same lock in `Update`.

Outside the lock: overlay and PNG encode. Both clones are owned by the caller. `RenderForPosition` **must** `using`-dispose the cloned cover **and** the cloned icon after `OverlayRenderer.Apply` / `ToPngDataUri`. Dropping the icon clone leaks ImageSharp native buffers on every key paint.

`PrepareCache` / `Update` stay as they are (decode under lock).

## 4. Trimming

`PublishTrimmed` is on. Settings flow through `JObject.ToObject<PluginSettings>()` and `Tools.AutoPopulateSettings`. Nested `PluginSettings` on actions can look unused to ILLink.

Add:

```xml
<TrimmerRootAssembly Include="StreamDeckCurrentMedia" />
```

Do not turn off trimming. Do not add `DynamicDependency` in this pass unless a later trimmer warning names a specific type.

## 5. Idle image dialog (Windows)

`StealForeground` calls `AttachThreadInput` against the current foreground thread. If that thread is stuck, the picker STA thread hangs with it.

Remove `StealForeground` and every P/Invoke used only by it:

- `AttachThreadInput`
- `GetForegroundWindow`
- `GetWindowThreadProcessId`
- `BringWindowToTop`
- `SetForegroundWindow`
- `GetCurrentThreadId`

Keep the existing owner window with `WS_EX_TOPMOST | WS_EX_TOOLWINDOW`. Dialog still uses `hwndOwner = owner`. Do not leave unused `DllImport` signatures in the file.

## 6. Dead code to delete

| Path | Why |
| --- | --- |
| `src/Core/KeyImageDecision.cs` | Unused outside its tests; `NowPlayingAction` / `ImagePipeline` duplicate the branches |
| `tests/CurrentMedia.Tests/KeyImageDecisionTests.cs` | Only consumer of the type above |
| `src/Providers/Mac/BridgeStateDto.cs` | Unused; live Mac path is `AdapterPayload` |
| `native/macos-media-bridge/` | Not invoked by `scripts/build-macos.sh` (that script builds `mediaremote-adapter`) |

Do not delete `native/mediaremote-adapter/`.

## Error handling

- COM equality miss: treat as a new session (attach); identity match: keep `SessionEntry` (track cache, stale flags).
- FNV is checksum-only, not cryptographic.
- If clone-outside-lock races with `Update`, the clone is a snapshot; overlay still runs. Do not read `_bitmaps` after releasing the lock.
- Gate skip/timeout behavior in `InvokeCoreAsync` is unchanged.

## Testing

- New: FNV-1a tests (empty → `811c9dc5`; equal length, different bytes).
- Existing `ImagePipelineIdleTests` still pass (no icon/cover clone leak on the idle path).
- After deleting idle-picker imports, the Windows picker file must not reference the removed APIs.
- Existing `RefreshLoopTests` / `MediaClientTimeoutsTests` unchanged.
- Manual Windows: two media sources; change track with same-length art if possible; four Now Playing keys; idle image picker while a game/player is focused.

## Success criteria

- Re-created SMTC RCWs do not double-subscribe.
- Cover-only changes publish a new image.
- PNG encode does not run inside `ImagePipelineCache`’s lock.
- Trimmed publish still round-trips action settings.
- Cloned overlay icon is disposed after each render.
- Idle picker cannot hang on `AttachThreadInput`; unused Win32 imports are gone.
- Listed dead paths are gone from the tree.
