#!/bin/bash
set -euo pipefail

cd "$(dirname "$0")/.."

WIN_DIR="dist/ru.valentderah.current-media.sdPlugin/win"

rm -rf "$WIN_DIR"
mkdir -p "$WIN_DIR"

echo "==> Publishing .NET (Windows)..."
DOTNET_PROPS=(
  -c Release
  -f net8.0-windows10.0.19041.0
  -r win-x64
  --self-contained true
  -p:PublishSingleFile=true
  -p:EnableCompressionInSingleFile=true
  -p:IncludeNativeLibrariesForSelfExtract=true
  -p:DebugType=none
  -p:DebugSymbols=false
)

if [ "$(uname -s)" != "MINGW"* ] && [ "$(uname -s)" != "CYGWIN"* ]; then
  DOTNET_PROPS+=(-p:EnableWindowsTargeting=true)
fi

dotnet publish src/StreamDeckCurrentMedia.csproj "${DOTNET_PROPS[@]}"

PUBLISH="src/bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/StreamDeckCurrentMedia.exe"
cp "$PUBLISH" "$WIN_DIR/StreamDeckCurrentMedia.exe"

echo "    Saved to ${WIN_DIR}"
