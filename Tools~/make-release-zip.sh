#!/usr/bin/env sh
# Builds a release zip whose top-level folder is the package folder, for attaching to GitHub Releases:
#   com.eigensoundlab.project-navigator-v<version>.zip
# Users extract it and drop the folder into their project's Packages/ directory.
set -e
SRC="$(cd "$(dirname "$0")/.." && pwd)"
NAME="$(basename "$SRC")"
VERSION="$(sed -n 's/.*"version": *"\([^"]*\)".*/\1/p' "$SRC/package.json" | head -1)"
OUT="$SRC/../$NAME-v$VERSION.zip"
rm -f "$OUT"
( cd "$SRC/.." && zip -qr "$OUT" "$NAME" -x "$NAME/.git/*" "$NAME/AssetStoreBuild/*" )
echo "Release zip written to $OUT"
