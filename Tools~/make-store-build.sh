#!/usr/bin/env sh
# Produces an Asset Store flavored copy of the package in ../AssetStoreBuild/ProjectNavigator:
#  - without the optional reflection file (store guideline 2.5.g),
#  - without the test assembly,
#  - ready to be dropped into Assets/ of an export project (Assets → Export Package...).
set -e
SRC="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$SRC/../AssetStoreBuild/ProjectNavigator"
rm -rf "$OUT"
mkdir -p "$OUT"
cp -r "$SRC/Editor" "$OUT/Editor"
cp "$SRC/README.md" "$SRC/LICENSE" "$SRC/NOTICE" "$SRC/Third-Party Notices.md" "$SRC/CHANGELOG.md" "$OUT/"
rm -f "$OUT/Editor/Core/ProjectBrowserBridge.Internals.cs" "$OUT/Editor/Core/ProjectBrowserBridge.Internals.cs.meta"
echo "Store build written to $OUT (reflection file removed, tests omitted)."
