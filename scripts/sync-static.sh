#!/bin/bash
set -euo pipefail

cd "$(dirname "$0")/.."

ASSETS_DIR="assets"
DIST_DIR="dist/ru.valentderah.current-media.sdPlugin"

echo "==> Syncing static assets..."
mkdir -p "$DIST_DIR"
rsync -a --delete --exclude 'win/' --exclude 'mac/' "$ASSETS_DIR/" "$DIST_DIR/"
echo "    Done."
