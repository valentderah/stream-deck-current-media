#!/bin/bash
set -euo pipefail

cd "$(dirname "$0")/.."

PLUGIN_DIR="ru.valentderah.current-media.sdPlugin/mac"
mkdir -p "$PLUGIN_DIR/arm64" "$PLUGIN_DIR/x64"

# 1. mediaremote-adapter (MediaRemote access via platform perl)
chmod +x native/mediaremote-adapter/build-framework.sh
bash native/mediaremote-adapter/build-framework.sh

FRAMEWORK_SRC="native/mediaremote-adapter/build/MediaRemoteAdapter.framework"
PERL_SRC="native/mediaremote-adapter/src/bin/mediaremote-adapter.pl"
LICENSE_SRC="native/mediaremote-adapter/src/LICENSE"

rm -rf "$PLUGIN_DIR/MediaRemoteAdapter.framework"
cp -R "$FRAMEWORK_SRC" "$PLUGIN_DIR/"
cp "$PERL_SRC" "$PLUGIN_DIR/mediaremote-adapter.pl"
chmod +x "$PLUGIN_DIR/mediaremote-adapter.pl"
codesign --force --sign - "$PLUGIN_DIR/MediaRemoteAdapter.framework/MediaRemoteAdapter"

if [ -f "$LICENSE_SRC" ]; then
  cp "$LICENSE_SRC" "$PLUGIN_DIR/mediaremote-adapter.LICENSE"
fi

# 2. .NET — publish separate SingleFile binaries per architecture
dotnet publish src/StreamDeckCurrentMedia.csproj \
  -c Release -f net8.0 -r osx-arm64 --self-contained true \
  -p:PublishSingleFile=true -p:TargetFrameworks=net8.0 -o build/osx-arm64

dotnet publish src/StreamDeckCurrentMedia.csproj \
  -c Release -f net8.0 -r osx-x64 --self-contained true \
  -p:PublishSingleFile=true -p:TargetFrameworks=net8.0 -o build/osx-x64

cp build/osx-arm64/StreamDeckCurrentMedia "$PLUGIN_DIR/arm64/StreamDeckCurrentMedia"
rm -f "$PLUGIN_DIR/arm64/libSkiaSharp.dylib"
chmod +x "$PLUGIN_DIR/arm64/StreamDeckCurrentMedia"
codesign --force --sign - "$PLUGIN_DIR/arm64/StreamDeckCurrentMedia"

cp build/osx-x64/StreamDeckCurrentMedia   "$PLUGIN_DIR/x64/StreamDeckCurrentMedia"
rm -f "$PLUGIN_DIR/x64/libSkiaSharp.dylib"
chmod +x "$PLUGIN_DIR/x64/StreamDeckCurrentMedia"
codesign --force --sign - "$PLUGIN_DIR/x64/StreamDeckCurrentMedia"

# 3. Architecture dispatcher script
cat > "$PLUGIN_DIR/entry.sh" << 'EOF'
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
chmod +x "$PLUGIN_DIR/entry.sh"
codesign --force --sign - "$PLUGIN_DIR/entry.sh"
