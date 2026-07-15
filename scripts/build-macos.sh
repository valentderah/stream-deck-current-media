#!/bin/bash
set -euo pipefail

cd "$(dirname "$0")/.."

MAC_DIR="dist/ru.valentderah.current-media.sdPlugin/mac"

rm -rf "$MAC_DIR"
mkdir -p "$MAC_DIR/arm64" "$MAC_DIR/x64"

# 1. mediaremote-adapter (MediaRemote access via platform perl)
echo "==> Building mediaremote-adapter..."
chmod +x native/mediaremote-adapter/build-framework.sh
bash native/mediaremote-adapter/build-framework.sh

FRAMEWORK_SRC="native/mediaremote-adapter/build/MediaRemoteAdapter.framework"
PERL_SRC="native/mediaremote-adapter/src/bin/mediaremote-adapter.pl"
LICENSE_SRC="native/mediaremote-adapter/src/LICENSE"

rm -rf "$MAC_DIR/MediaRemoteAdapter.framework"
cp -R "$FRAMEWORK_SRC" "$MAC_DIR/"
cp "$PERL_SRC" "$MAC_DIR/mediaremote-adapter.pl"
chmod +x "$MAC_DIR/mediaremote-adapter.pl"
codesign --force --sign - "$MAC_DIR/MediaRemoteAdapter.framework/MediaRemoteAdapter"

if [ -f "$LICENSE_SRC" ]; then
  cp "$LICENSE_SRC" "$MAC_DIR/mediaremote-adapter.LICENSE"
fi

# 2. .NET — publish separate SingleFile binaries per architecture
echo "==> Publishing .NET (macOS)..."
dotnet publish src/StreamDeckCurrentMedia.csproj \
  -c Release -f net8.0 -r osx-arm64 --self-contained true \
  -p:PublishSingleFile=true -p:TargetFrameworks=net8.0 -o build/osx-arm64

dotnet publish src/StreamDeckCurrentMedia.csproj \
  -c Release -f net8.0 -r osx-x64 --self-contained true \
  -p:PublishSingleFile=true -p:TargetFrameworks=net8.0 -o build/osx-x64

cp build/osx-arm64/StreamDeckCurrentMedia "$MAC_DIR/arm64/StreamDeckCurrentMedia"
rm -f "$MAC_DIR/arm64/libSkiaSharp.dylib"
chmod +x "$MAC_DIR/arm64/StreamDeckCurrentMedia"
codesign --force --sign - "$MAC_DIR/arm64/StreamDeckCurrentMedia"

cp build/osx-x64/StreamDeckCurrentMedia   "$MAC_DIR/x64/StreamDeckCurrentMedia"
rm -f "$MAC_DIR/x64/libSkiaSharp.dylib"
chmod +x "$MAC_DIR/x64/StreamDeckCurrentMedia"
codesign --force --sign - "$MAC_DIR/x64/StreamDeckCurrentMedia"

# 3. Architecture dispatcher script
echo "==> Generating entry.sh..."
cat > "$MAC_DIR/entry.sh" << 'EOF'
#!/bin/bash
ARCH=$(uname -m)
DIR="$(cd "$(dirname "$0")" && pwd)"
chmod +x "$DIR/arm64/StreamDeckCurrentMedia" "$DIR/x64/StreamDeckCurrentMedia" "$DIR/mediaremote-adapter.pl" 2>/dev/null || true
if [ "$ARCH" = "arm64" ]; then
    exec "$DIR/arm64/StreamDeckCurrentMedia" "$@"
else
    exec "$DIR/x64/StreamDeckCurrentMedia" "$@"
fi
EOF
chmod +x "$MAC_DIR/entry.sh"
codesign --force --sign - "$MAC_DIR/entry.sh"

echo "    Saved to ${MAC_DIR}"
