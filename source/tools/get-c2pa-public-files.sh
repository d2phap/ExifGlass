#!/usr/bin/env bash
# Downloads the public C2PA test files listed in c2pa-public-files.txt into source/tests/fixtures/c2pa-public (git-ignored).
# Usage: tools/get-c2pa-public-files.sh [--destination <folder>] [--force]
set -euo pipefail

TOOLS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEST="$TOOLS_DIR/../tests/fixtures/c2pa-public"
FORCE=0
while [ $# -gt 0 ]; do
  case "$1" in
    --destination) DEST="$2"; shift 2 ;;
    --force) FORCE=1; shift ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done

COUNT=0
BYTES=0
while read -r folder repo commit path; do
  case "$folder" in ''|'#'*) continue ;; esac
  path="${path%$'\r'}"
  target="$DEST/$folder/$path"
  if [ "$FORCE" = 1 ] || [ ! -f "$target" ]; then
    curl -fsSL --create-dirs -o "$target" "https://raw.githubusercontent.com/$repo/$commit/$path"
  fi
  COUNT=$((COUNT + 1))
  BYTES=$((BYTES + $(wc -c < "$target")))
done < "$TOOLS_DIR/c2pa-public-files.txt"

echo "$COUNT files, $((BYTES / 1048576)) MB in $(cd "$DEST" && pwd)"
