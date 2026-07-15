#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
VERSION="${MEDIAREMOTE_ADAPTER_VERSION:-0.7.6}"
SRC_DIR="$SCRIPT_DIR/src"
BUILD_DIR="$SCRIPT_DIR/build"
FRAMEWORK_DIR="$BUILD_DIR/MediaRemoteAdapter.framework"

if [ ! -f "$SRC_DIR/src/adapter/stream.m" ]; then
  TMP_ARCHIVE="$(mktemp -t mediaremote-adapter.XXXXXX.tar.gz)"
  curl -fsSL "https://github.com/ungive/mediaremote-adapter/archive/refs/tags/v${VERSION}.tar.gz" -o "$TMP_ARCHIVE"
  rm -rf "$SRC_DIR"
  mkdir -p "$SRC_DIR"
  tar -xzf "$TMP_ARCHIVE" -C "$SRC_DIR" --strip-components=1
  rm -f "$TMP_ARCHIVE"
fi

mkdir -p "$FRAMEWORK_DIR"

clang -shared \
  -fobjc-arc \
  -fvisibility=default \
  -arch arm64 \
  -arch x86_64 \
  -mmacosx-version-min=11.0 \
  -framework Foundation \
  -framework AppKit \
  -framework UniformTypeIdentifiers \
  -I "$SRC_DIR/include" \
  -I "$SRC_DIR/src" \
  "$SRC_DIR/src/adapter/env.m" \
  "$SRC_DIR/src/adapter/get.m" \
  "$SRC_DIR/src/adapter/globals.m" \
  "$SRC_DIR/src/adapter/keys.m" \
  "$SRC_DIR/src/adapter/now_playing.m" \
  "$SRC_DIR/src/adapter/repeat.m" \
  "$SRC_DIR/src/adapter/seek.m" \
  "$SRC_DIR/src/adapter/send.m" \
  "$SRC_DIR/src/adapter/shuffle.m" \
  "$SRC_DIR/src/adapter/speed.m" \
  "$SRC_DIR/src/adapter/stream.m" \
  "$SRC_DIR/src/adapter/test.m" \
  "$SRC_DIR/src/private/MediaRemote.m" \
  "$SRC_DIR/src/utility/Debounce.m" \
  "$SRC_DIR/src/utility/helpers.m" \
  -o "$FRAMEWORK_DIR/MediaRemoteAdapter"

codesign --force --sign - "$FRAMEWORK_DIR/MediaRemoteAdapter"
echo "Built $FRAMEWORK_DIR"
