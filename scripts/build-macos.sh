#!/bin/bash
set -euo pipefail

cd "$(dirname "$0")/.."

PLUGIN_DIR="ru.valentderah.current-media.sdPlugin/mac"
mkdir -p "$PLUGIN_DIR"

# 1. Swift universal binary 
make -C native/macos-media-bridge
cp native/macos-media-bridge/media-bridge "$PLUGIN_DIR/"
chmod +x "$PLUGIN_DIR/media-bridge"
codesign --force --sign - "$PLUGIN_DIR/media-bridge"

# 2. .NET — publish separate SingleFile binaries per architecture
dotnet publish src/StreamDeckCurrentMedia.csproj \
  -c Release -f net8.0 -r osx-arm64 --self-contained true \
  -p:PublishSingleFile=true -o build/osx-arm64

dotnet publish src/StreamDeckCurrentMedia.csproj \
  -c Release -f net8.0 -r osx-x64 --self-contained true \
  -p:PublishSingleFile=true -o build/osx-x64

cp build/osx-arm64/StreamDeckCurrentMedia "$PLUGIN_DIR/StreamDeckCurrentMedia-arm64"
cp build/osx-x64/StreamDeckCurrentMedia   "$PLUGIN_DIR/StreamDeckCurrentMedia-x64"
chmod +x "$PLUGIN_DIR/StreamDeckCurrentMedia-arm64"
chmod +x "$PLUGIN_DIR/StreamDeckCurrentMedia-x64"
codesign --force --sign - "$PLUGIN_DIR/StreamDeckCurrentMedia-arm64"
codesign --force --sign - "$PLUGIN_DIR/StreamDeckCurrentMedia-x64"

# 3. Architecture dispatcher script
cat > "$PLUGIN_DIR/entry.sh" << 'EOF'
#!/bin/bash
ARCH=$(uname -m)
DIR="$(cd "$(dirname "$0")" && pwd)"
if [ "$ARCH" = "arm64" ]; then
    exec "$DIR/StreamDeckCurrentMedia-arm64" "$@"
else
    exec "$DIR/StreamDeckCurrentMedia-x64" "$@"
fi
EOF
chmod +x "$PLUGIN_DIR/entry.sh"
codesign --force --sign - "$PLUGIN_DIR/entry.sh"