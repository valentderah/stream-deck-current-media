using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using BarRaider.SdTools;
using CurrentMedia.Imaging;
using Windows.Foundation;
using Windows.Media.Control;
using Windows.Storage.Streams;
using SmtcManager = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager;
using SmtcMediaProperties = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties;
using SmtcPlaybackInfo = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackInfo;
using SmtcPlaybackStatus = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus;
using SmtcSession = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;
using SmtcTimeline = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionTimelineProperties;
using WinRtBuffer = Windows.Storage.Streams.Buffer;

namespace CurrentMedia.Windows;

public sealed class WindowsMediaManager : IMediaManager
{
    private const int MaxThumbnailAttempts = 3;
    private const int MaxConsecutiveCallFailures = 3;
    private const long PropertiesTtlMs = 5_000;
    private const uint MaxThumbnailBytes = 12 * 1024 * 1024;
    private const char SignatureSeparator = '\u001f';

    private readonly RefreshLoop _loop;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly Dictionary<SmtcSession, SessionEntry> _entries = new(ReferenceEqualityComparer.Instance);
    private readonly List<SessionEntry> _ordered = new();
    private readonly ConcurrentDictionary<SmtcSession, byte> _propertyChanges = new(ReferenceEqualityComparer.Instance);

    private volatile SmtcManager? _manager;
    private volatile TaskCompletionSource _managerReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile string? _lastPlayingSessionId;
    private int _forcePublish;
    private int _managerLost;
    private int _acquireFailures;
    private int _consecutiveCallFailures;
    private long _nextAcquireTicks;
    private string _publishedSignature = "";
    private volatile bool _disposed;

    public event EventHandler<MediaState>? MediaStateChanged;

    public WindowsMediaManager()
    {
        _loop = new RefreshLoop(
            RefreshAsync,
            new RefreshLoopOptions(),
            ex => Logger.Instance.LogMessage(TracingLevel.ERROR, $"Media refresh pass failed: {ex.Message}"));
    }

    public Task InitializeAsync()
    {
        ScheduleUpdate(force: true);
        return Task.CompletedTask;
    }

    public Task RequestUpdateAsync()
    {
        ScheduleUpdate(force: true);
        return Task.CompletedTask;
    }

    public Task PlayPauseAsync() =>
        RunCommandAsync(session => session.TryTogglePlayPauseAsync(), "TryTogglePlayPauseAsync");

    public Task NextAsync() =>
        RunCommandAsync(session => session.TrySkipNextAsync(), "TrySkipNextAsync");

    public Task PreviousAsync() =>
        RunCommandAsync(session => session.TrySkipPreviousAsync(), "TrySkipPreviousAsync");

    public Task SeekByAsync(int offsetSeconds) => SeekAsync(TimeSpan.FromSeconds(offsetSeconds));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _shutdownCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _loop.Dispose();
        ReleaseManager("plugin shutdown");
        _managerReady.TrySetResult();

        try
        {
            _shutdownCts.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void ScheduleUpdate(bool force)
    {
        if (_disposed)
        {
            return;
        }

        if (force)
        {
            Interlocked.Exchange(ref _forcePublish, 1);
        }

        _loop.Start();
        _loop.Request();
    }

    private async Task RefreshAsync(CancellationToken token)
    {
        if (_disposed)
        {
            return;
        }

        var force = Interlocked.Exchange(ref _forcePublish, 0) == 1;

        if (Interlocked.Exchange(ref _managerLost, 0) == 1)
        {
            ReleaseManager("session manager stopped responding");
        }

        var manager = await EnsureManagerAsync(token).ConfigureAwait(false);
        if (_disposed || manager == null || !SyncSessions(manager))
        {
            if (!_disposed)
            {
                Publish(InactiveState(), force);
            }

            return;
        }

        ApplyPropertyChanges();

        var entry = PickSession(manager);
        var state = entry == null
            ? InactiveState()
            : await BuildStateAsync(entry, token).ConfigureAwait(false);

        if (!_disposed)
        {
            Publish(state, force);
        }
    }

    private async Task<SmtcManager?> EnsureManagerAsync(CancellationToken token)
    {
        var existing = _manager;
        if (existing != null)
        {
            return existing;
        }

        if (Environment.TickCount64 < Interlocked.Read(ref _nextAcquireTicks))
        {
            return null;
        }

        var manager = await InvokeAsync(
                SmtcManager.RequestAsync,
                MediaClientTimeouts.SessionManagerRequest,
                "SMTC RequestAsync",
                token,
                countsTowardManagerHealth: false)
            .ConfigureAwait(false);

        if (manager == null || _disposed)
        {
            if (manager == null)
            {
                ScheduleAcquireRetry();
            }

            return null;
        }

        try
        {
            manager.CurrentSessionChanged += HandleCurrentSessionChanged;
            manager.SessionsChanged += HandleSessionsChanged;
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Failed to subscribe to session manager: {ex.Message}");
            ScheduleAcquireRetry();
            return null;
        }

        if (_disposed)
        {
            try
            {
                manager.CurrentSessionChanged -= HandleCurrentSessionChanged;
                manager.SessionsChanged -= HandleSessionsChanged;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Failed to detach manager events: {ex.Message}");
            }

            return null;
        }

        _manager = manager;
        Interlocked.Exchange(ref _acquireFailures, 0);
        Interlocked.Exchange(ref _consecutiveCallFailures, 0);
        _managerReady.TrySetResult();
        Logger.Instance.LogMessage(TracingLevel.INFO, "SMTC session manager acquired");
        return manager;
    }

    private void ScheduleAcquireRetry()
    {
        var failures = Interlocked.Increment(ref _acquireFailures);
        var seconds = Math.Min(30d, Math.Pow(2, Math.Min(failures - 1, 5)));
        Interlocked.Exchange(ref _nextAcquireTicks, Environment.TickCount64 + (long)(seconds * 1000));
        Logger.Instance.LogMessage(
            TracingLevel.WARN,
            $"SMTC session manager unavailable, next attempt in {seconds:F0}s");
    }

    private void ReleaseManager(string reason)
    {
        var manager = _manager;
        _manager = null;
        _managerReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Interlocked.Exchange(ref _consecutiveCallFailures, 0);

        if (manager != null)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN, $"Releasing SMTC session manager: {reason}");

            try
            {
                manager.CurrentSessionChanged -= HandleCurrentSessionChanged;
                manager.SessionsChanged -= HandleSessionsChanged;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Failed to detach manager events: {ex.Message}");
            }
        }

        foreach (var entry in _entries.Values)
        {
            Detach(entry);
        }

        _entries.Clear();
        _ordered.Clear();
        _propertyChanges.Clear();
    }

    private bool SyncSessions(SmtcManager manager)
    {
        IReadOnlyList<SmtcSession> sessions;
        try
        {
            sessions = manager.GetSessions();
        }
        catch (Exception ex)
        {
            NoteCallFailure("GetSessions", ex);
            return false;
        }

        _ordered.Clear();
        var live = new HashSet<SmtcSession>(ReferenceEqualityComparer.Instance);

        foreach (var session in sessions)
        {
            if (session == null || !live.Add(session))
            {
                continue;
            }

            if (!_entries.TryGetValue(session, out var entry))
            {
                entry = Attach(session);
                if (entry == null)
                {
                    continue;
                }

                _entries[session] = entry;
            }

            _ordered.Add(entry);
        }

        if (_entries.Count == _ordered.Count)
        {
            return true;
        }

        foreach (var session in _entries.Keys.Where(session => !live.Contains(session)).ToList())
        {
            if (_entries.Remove(session, out var stale))
            {
                Detach(stale);
            }

            _propertyChanges.TryRemove(session, out _);
        }

        return true;
    }

    private SessionEntry? Attach(SmtcSession session)
    {
        var entry = new SessionEntry(session, ReadSourceId(session), ReadSourceAppInfo(session));

        try
        {
            session.MediaPropertiesChanged += OnMediaPropertiesChanged;
            session.PlaybackInfoChanged += OnPlaybackInfoChanged;
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN, $"Failed to attach session {entry.Id}: {ex.Message}");
            Detach(entry);
            return null;
        }

        return entry;
    }

    private void Detach(SessionEntry entry)
    {
        try
        {
            entry.Session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            entry.Session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN, $"Failed to detach session {entry.Id}: {ex.Message}");
        }
    }

    private void ApplyPropertyChanges()
    {
        foreach (var session in _propertyChanges.Keys)
        {
            if (!_propertyChanges.TryRemove(session, out _))
            {
                continue;
            }

            if (_entries.TryGetValue(session, out var entry))
            {
                entry.PropertiesStale = true;
            }
        }
    }

    private void HandleCurrentSessionChanged(SmtcManager sender, CurrentSessionChangedEventArgs args)
    {
        _loop.Request();
    }

    private void HandleSessionsChanged(SmtcManager sender, SessionsChangedEventArgs args)
    {
        _loop.Request();
    }

    private void OnMediaPropertiesChanged(SmtcSession session, MediaPropertiesChangedEventArgs args)
    {
        _propertyChanges[session] = 0;
        _loop.Request();
    }

    private void OnPlaybackInfoChanged(SmtcSession session, PlaybackInfoChangedEventArgs args)
    {
        _loop.Request();
    }

    private SessionEntry? PickSession(SmtcManager manager)
    {
        if (_ordered.Count == 0)
        {
            return null;
        }

        var currentId = ReadCurrentSessionId(manager);
        var lastPlayingId = _lastPlayingSessionId;
        var pausedCurrent = -1;
        var pausedLastPlaying = -1;
        var anyPaused = -1;

        for (var i = 0; i < _ordered.Count; i++)
        {
            var entry = _ordered[i];
            entry.Playback = ReadPlaybackInfo(entry);

            switch (entry.Playback?.PlaybackStatus)
            {
                case SmtcPlaybackStatus.Playing:
                    return entry;
                case SmtcPlaybackStatus.Paused:
                    if (pausedCurrent < 0 && currentId != null && entry.Id == currentId)
                    {
                        pausedCurrent = i;
                    }

                    if (pausedLastPlaying < 0 && lastPlayingId != null && entry.Id == lastPlayingId)
                    {
                        pausedLastPlaying = i;
                    }

                    if (anyPaused < 0)
                    {
                        anyPaused = i;
                    }

                    break;
            }
        }

        if (pausedCurrent >= 0)
        {
            return _ordered[pausedCurrent];
        }

        if (pausedLastPlaying >= 0)
        {
            return _ordered[pausedLastPlaying];
        }

        return anyPaused >= 0 ? _ordered[anyPaused] : _ordered[0];
    }

    private async Task<MediaState> BuildStateAsync(SessionEntry entry, CancellationToken token)
    {
        var playback = entry.Playback;
        if (playback == null)
        {
            return InactiveState();
        }

        var track = await EnsureTrackAsync(entry, token).ConfigureAwait(false);
        if (track == null || !track.HasText)
        {
            return InactiveState();
        }

        if (playback.PlaybackStatus == SmtcPlaybackStatus.Playing)
        {
            _lastPlayingSessionId = entry.Id;
        }

        var state = new MediaState
        {
            Title = track.Title,
            Artist = track.Artist,
            Artists = new List<string>(track.Artists),
            AlbumArtist = track.AlbumArtist,
            AlbumTitle = track.AlbumTitle,
            CoverArtBase64 = track.CoverArtBase64,
            Status = playback.PlaybackStatus switch
            {
                SmtcPlaybackStatus.Playing => "Playing",
                SmtcPlaybackStatus.Paused => "Paused",
                _ => "Stopped"
            },
            IsActive = true
        };

        ApplyTimeline(entry, playback.PlaybackStatus, state);

        state.AppIconBase64 = await WindowsAppIconProcessor
            .GetAppIconBase64Async(entry.Id, entry.SourceAppInfo, MediaClientTimeouts.AppIcon, token)
            .ConfigureAwait(false);

        return state;
    }

    private async Task<TrackInfo?> EnsureTrackAsync(SessionEntry entry, CancellationToken token)
    {
        if (!entry.NeedsProperties)
        {
            return entry.Track;
        }

        var properties = await InvokeAsync(
                entry.Session.TryGetMediaPropertiesAsync,
                MediaClientTimeouts.SessionOperation,
                "TryGetMediaPropertiesAsync",
                token,
                countsTowardManagerHealth: true)
            .ConfigureAwait(false);

        if (properties == null)
        {
            return entry.Track;
        }

        var track = TrackInfo.TryCreate(properties);
        if (track == null)
        {
            return entry.Track;
        }

        entry.MarkPropertiesRead();

        var previous = entry.Track;
        if (previous != null && previous.Identity == track.Identity)
        {
            track.CoverArtBase64 = previous.CoverArtBase64;
            track.ThumbnailAttempts = previous.ThumbnailAttempts;
        }

        if (track.CoverArtBase64.Length == 0
            && track.ThumbnailAttempts < MaxThumbnailAttempts
            && properties.Thumbnail != null)
        {
            track.ThumbnailAttempts++;
            track.CoverArtBase64 = await ReadThumbnailAsync(properties.Thumbnail, token).ConfigureAwait(false);

            if (track.CoverArtBase64.Length == 0 && track.ThumbnailAttempts < MaxThumbnailAttempts)
            {
                // Retry on a later pass instead of sleeping inside this one.
                entry.PropertiesStale = true;
            }
        }

        entry.Track = track;
        return track;
    }

    private async Task<string> ReadThumbnailAsync(IRandomAccessStreamReference thumbnail, CancellationToken token)
    {
        using var stream = await InvokeAsync(
                thumbnail.OpenReadAsync,
                MediaClientTimeouts.Thumbnail,
                "thumbnail OpenReadAsync",
                token,
                countsTowardManagerHealth: false)
            .ConfigureAwait(false);

        if (stream == null || stream.Size == 0)
        {
            return string.Empty;
        }

        if (stream.Size > MaxThumbnailBytes)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN, $"Skipping oversized thumbnail ({stream.Size} bytes)");
            return string.Empty;
        }

        var size = (uint)stream.Size;
        var buffer = new WinRtBuffer(size);

        try
        {
            stream.Seek(0);
        }
        catch (Exception ex)
        {
            NoteCallFailure("thumbnail Seek", ex);
            return string.Empty;
        }

        var read = await InvokeAsync(
                () => stream.ReadAsync(buffer, size, InputStreamOptions.None),
                MediaClientTimeouts.Thumbnail,
                "thumbnail ReadAsync",
                token,
                countsTowardManagerHealth: false)
            .ConfigureAwait(false);

        return read == null ? string.Empty : Convert.ToBase64String(read.ToArray());
    }

    private void ApplyTimeline(SessionEntry entry, SmtcPlaybackStatus status, MediaState state)
    {
        SmtcTimeline? timeline;
        try
        {
            timeline = entry.Session.GetTimelineProperties();
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN, $"Failed to read timeline of {entry.Id}: {ex.Message}");
            return;
        }

        if (timeline == null)
        {
            return;
        }

        state.Position = GetEffectivePlaybackPosition(timeline, status).TotalSeconds;

        var duration = timeline.EndTime - timeline.StartTime;
        if (duration > TimeSpan.Zero)
        {
            state.Duration = duration.TotalSeconds;
        }
    }

    private void Publish(MediaState state, bool force)
    {
        if (_disposed)
        {
            return;
        }

        var signature = BuildSignature(state);
        if (!force && signature == _publishedSignature)
        {
            return;
        }

        _publishedSignature = signature;

        try
        {
            ImagePipeline.PrepareCache(state);
            MediaStateChanged?.Invoke(this, state);
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Failed to publish media state: {ex.Message}");
        }
    }

    private async Task RunCommandAsync(Func<SmtcSession, IAsyncOperation<bool>> command, string name)
    {
        var session = await GetCommandSessionAsync().ConfigureAwait(false);
        if (session == null)
        {
            return;
        }

        await InvokeAsync(
                () => command(session),
                MediaClientTimeouts.SessionOperation,
                name,
                _shutdownCts.Token,
                countsTowardManagerHealth: true)
            .ConfigureAwait(false);

        ScheduleUpdate(force: false);
    }

    private async Task SeekAsync(TimeSpan offset)
    {
        var session = await GetCommandSessionAsync().ConfigureAwait(false);
        if (session == null)
        {
            return;
        }

        TimeSpan target;
        try
        {
            var playback = session.GetPlaybackInfo();
            if (playback == null || !playback.Controls.IsPlaybackPositionEnabled)
            {
                return;
            }

            var timeline = session.GetTimelineProperties();
            if (timeline == null)
            {
                return;
            }

            target = GetEffectivePlaybackPosition(timeline, playback.PlaybackStatus) + offset;

            if (target < timeline.StartTime)
            {
                target = timeline.StartTime;
            }

            if (timeline.EndTime > TimeSpan.Zero && target > timeline.EndTime)
            {
                target = timeline.EndTime;
            }
        }
        catch (Exception ex)
        {
            NoteCallFailure("seek", ex);
            return;
        }

        await InvokeAsync(
                () => session.TryChangePlaybackPositionAsync(target.Ticks),
                MediaClientTimeouts.SessionOperation,
                "TryChangePlaybackPositionAsync",
                _shutdownCts.Token,
                countsTowardManagerHealth: true)
            .ConfigureAwait(false);

        ScheduleUpdate(force: false);
    }

    private async Task<SmtcSession?> GetCommandSessionAsync()
    {
        var manager = await WaitForManagerAsync().ConfigureAwait(false);
        if (manager == null)
        {
            return null;
        }

        try
        {
            var sessions = manager.GetSessions();
            if (sessions.Count == 0)
            {
                return null;
            }

            var currentId = ReadCurrentSessionId(manager);
            var lastPlayingId = _lastPlayingSessionId;
            SmtcSession? pausedCurrent = null;
            SmtcSession? pausedLastPlaying = null;
            SmtcSession? anyPaused = null;

            foreach (var session in sessions)
            {
                SmtcPlaybackInfo? playback;
                try
                {
                    playback = session.GetPlaybackInfo();
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"Failed to read playback info: {ex.Message}");
                    continue;
                }

                if (playback?.PlaybackStatus == SmtcPlaybackStatus.Playing)
                {
                    return session;
                }

                if (playback?.PlaybackStatus != SmtcPlaybackStatus.Paused)
                {
                    continue;
                }

                var id = ReadSourceId(session);

                if (pausedCurrent == null && currentId != null && id == currentId)
                {
                    pausedCurrent = session;
                }

                if (pausedLastPlaying == null && lastPlayingId != null && id == lastPlayingId)
                {
                    pausedLastPlaying = session;
                }

                anyPaused ??= session;
            }

            return pausedCurrent ?? pausedLastPlaying ?? anyPaused ?? sessions[0];
        }
        catch (Exception ex)
        {
            NoteCallFailure("GetSessions", ex);
            return null;
        }
    }

    private async Task<SmtcManager?> WaitForManagerAsync()
    {
        var manager = _manager;
        if (manager != null)
        {
            return manager;
        }

        ScheduleUpdate(force: false);

        try
        {
            await _managerReady.Task
                .WaitAsync(MediaClientTimeouts.ManagerReady, _shutdownCts.Token)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN, "SMTC session manager is not ready");
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (ObjectDisposedException)
        {
            return null;
        }

        return _manager;
    }

    private Task<T?> InvokeAsync<T>(
        Func<IAsyncOperation<T>> start,
        TimeSpan timeout,
        string name,
        CancellationToken token,
        bool countsTowardManagerHealth) =>
        InvokeCoreAsync(
            () =>
            {
                var operation = start();
                return (operation, operation.AsTask());
            },
            timeout,
            name,
            token,
            countsTowardManagerHealth);

    private Task<T?> InvokeAsync<T, TProgress>(
        Func<IAsyncOperationWithProgress<T, TProgress>> start,
        TimeSpan timeout,
        string name,
        CancellationToken token,
        bool countsTowardManagerHealth) =>
        InvokeCoreAsync(
            () =>
            {
                var operation = start();
                return (operation, operation.AsTask());
            },
            timeout,
            name,
            token,
            countsTowardManagerHealth);

    private async Task<T?> InvokeCoreAsync<T>(
        Func<(IAsyncInfo Operation, Task<T> Task)> start,
        TimeSpan timeout,
        string name,
        CancellationToken token,
        bool countsTowardManagerHealth)
    {
        if (_disposed)
        {
            return default;
        }

        IAsyncInfo operation;
        Task<T> pending;
        try
        {
            (operation, pending) = start();
        }
        catch (Exception ex)
        {
            NoteCallFailure(name, ex, countsTowardManagerHealth);
            return default;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, _shutdownCts.Token);

        try
        {
            var result = await pending.WaitAsync(timeout, cts.Token).ConfigureAwait(false);
            Interlocked.Exchange(ref _consecutiveCallFailures, 0);
            return result;
        }
        catch (TimeoutException)
        {
            Abandon(operation, pending, name);
            Logger.Instance.LogMessage(
                TracingLevel.WARN,
                $"{name} timed out after {timeout.TotalMilliseconds:F0}ms");
            if (countsTowardManagerHealth)
            {
                CountFailure();
            }

            return default;
        }
        catch (OperationCanceledException)
        {
            Abandon(operation, pending, name);
            return default;
        }
        catch (Exception ex)
        {
            NoteCallFailure(name, ex, countsTowardManagerHealth);
            return default;
        }
    }

    private static void Abandon<T>(IAsyncInfo operation, Task<T> pending, string name)
    {
        pending.ContinueWith(
            static task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        try
        {
            operation.Cancel();
        }
        catch (Exception ex)
        {
            // A disconnected media app can fail the cancel itself; the operation is dropped either way.
            Logger.Instance.LogMessage(TracingLevel.WARN, $"Failed to cancel {name}: {ex.Message}");
        }
    }

    private void NoteCallFailure(string name, Exception ex, bool countsTowardManagerHealth = true)
    {
        Logger.Instance.LogMessage(TracingLevel.WARN, $"{name} failed: {ex.Message}");

        if (ex is COMException or InvalidComObjectException)
        {
            MarkManagerLost();
            return;
        }

        if (countsTowardManagerHealth)
        {
            CountFailure();
        }
    }

    private void CountFailure()
    {
        if (Interlocked.Increment(ref _consecutiveCallFailures) >= MaxConsecutiveCallFailures)
        {
            MarkManagerLost();
        }
    }

    private void MarkManagerLost()
    {
        if (_manager == null || _disposed)
        {
            return;
        }

        Interlocked.Exchange(ref _managerLost, 1);
        _loop.Request();
    }

    private string ReadSourceId(SmtcSession session)
    {
        try
        {
            return session.SourceAppUserModelId ?? string.Empty;
        }
        catch (Exception ex)
        {
            NoteCallFailure("SourceAppUserModelId", ex);
            return string.Empty;
        }
    }

    private string? ReadCurrentSessionId(SmtcManager manager)
    {
        try
        {
            var current = manager.GetCurrentSession();
            return current == null ? null : ReadSourceId(current);
        }
        catch (Exception ex)
        {
            NoteCallFailure("GetCurrentSession", ex);
            return null;
        }
    }

    private SmtcPlaybackInfo? ReadPlaybackInfo(SessionEntry entry)
    {
        try
        {
            return entry.Session.GetPlaybackInfo();
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN, $"Failed to read playback info of {entry.Id}: {ex.Message}");
            return null;
        }
    }

    private static object? ReadSourceAppInfo(SmtcSession session)
    {
        try
        {
            return session.GetType().GetProperty("SourceAppInfo")?.GetValue(session);
        }
        catch
        {
            return null;
        }
    }

    private static MediaState InactiveState() => new() { IsActive = false };

    // Position and duration are deliberately left out: nothing renders them, so a ticking
    // playback position must not force every key to redraw.
    private static string BuildSignature(MediaState state) => string.Join(
        SignatureSeparator,
        state.IsActive ? "1" : "0",
        state.Status,
        state.Title,
        state.Artist,
        state.AlbumTitle,
        state.AlbumArtist,
        state.CoverArtBase64.Length.ToString(CultureInfo.InvariantCulture),
        state.AppIconBase64.Length.ToString(CultureInfo.InvariantCulture));

    private static TimeSpan GetEffectivePlaybackPosition(SmtcTimeline timeline, SmtcPlaybackStatus status)
    {
        var position = timeline.Position;
        if (status == SmtcPlaybackStatus.Playing)
        {
            position += DateTimeOffset.UtcNow - timeline.LastUpdatedTime;
        }

        return position;
    }

    private sealed class SessionEntry
    {
        private long _propertiesReadAt;

        public SessionEntry(SmtcSession session, string id, object? sourceAppInfo)
        {
            Session = session;
            Id = id;
            SourceAppInfo = sourceAppInfo;
        }

        public SmtcSession Session { get; }
        public string Id { get; }
        public object? SourceAppInfo { get; }
        public SmtcPlaybackInfo? Playback { get; set; }
        public TrackInfo? Track { get; set; }
        public bool PropertiesStale { get; set; } = true;

        // Some sources never raise MediaPropertiesChanged, so a cached track also expires on its own.
        public bool NeedsProperties =>
            PropertiesStale
            || Track == null
            || Environment.TickCount64 - _propertiesReadAt > PropertiesTtlMs;

        public void MarkPropertiesRead()
        {
            PropertiesStale = false;
            _propertiesReadAt = Environment.TickCount64;
        }
    }

    private sealed class TrackInfo
    {
        private TrackInfo(
            string title,
            string artist,
            string albumArtist,
            string albumTitle,
            List<string> artists,
            string identity)
        {
            Title = title;
            Artist = artist;
            AlbumArtist = albumArtist;
            AlbumTitle = albumTitle;
            Artists = artists;
            Identity = identity;
        }

        public string Title { get; }
        public string Artist { get; }
        public string AlbumArtist { get; }
        public string AlbumTitle { get; }
        public List<string> Artists { get; }
        public string Identity { get; }
        public string CoverArtBase64 { get; set; } = "";
        public int ThumbnailAttempts { get; set; }

        public bool HasText => Title.Length > 0 || Artist.Length > 0 || Artists.Count > 0;

        public static TrackInfo? TryCreate(SmtcMediaProperties properties)
        {
            try
            {
                var title = properties.Title ?? string.Empty;
                var artist = properties.Artist ?? string.Empty;
                var albumTitle = properties.AlbumTitle ?? string.Empty;

                return new TrackInfo(
                    title,
                    artist,
                    properties.AlbumArtist ?? string.Empty,
                    albumTitle,
                    SplitArtists(artist),
                    string.Join(SignatureSeparator, title, artist, albumTitle));
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Failed to read media properties: {ex.Message}");
                return null;
            }
        }

        private static List<string> SplitArtists(string artist)
        {
            if (artist.Length == 0)
            {
                return new List<string>();
            }

            return artist
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => part.Length > 0)
                .ToList();
        }
    }
}
