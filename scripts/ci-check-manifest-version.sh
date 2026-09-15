#!/usr/bin/env bash
set -euo pipefail

TAG="${1:?tag required}"
MANIFEST="${2:?manifest path required}"

if [[ ! "$TAG" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Not a three-part release tag ($TAG), skip version check"
  exit 0
fi

if [[ ! -f "$MANIFEST" ]]; then
  echo "Manifest not found: $MANIFEST"
  exit 1
fi

EXPECTED="${TAG#v}.0"
ACTUAL="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1], encoding="utf-8"))["Version"])' "$MANIFEST")"

if [[ "$ACTUAL" != "$EXPECTED" ]]; then
  echo "manifest Version '$ACTUAL' does not match tag $TAG (expected $EXPECTED)"
  exit 1
fi

echo "manifest Version $ACTUAL matches $TAG"
