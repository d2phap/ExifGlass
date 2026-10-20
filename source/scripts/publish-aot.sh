#!/usr/bin/env bash
# Publishes a trimmed NativeAOT build into __artifacts/publish/<rid>; trim and AOT warnings are errors.
# Usage: scripts/publish-aot.sh [osx-arm64|linux-x64] [--runtime-async off]
. "$(dirname "$0")/_common.sh"

RID=""
ASYNC="on"
while [ $# -gt 0 ]; do
  case "$1" in
    --runtime-async) ASYNC="$2"; shift 2 ;;
    *) RID="$1"; shift ;;
  esac
done
[ -z "$RID" ] && RID="$(host_rid)"
rid_info "$RID"

ASYNC_FLAG="true"
[ "$ASYNC" = "off" ] && ASYNC_FLAG="false"
OUT_DIR="$ARTIFACTS_DIR/publish/$RID"
new_report "publish-aot-$RID"

report "Publishing $RID_HEAD for $RID (Platform=$RID_PLATFORM, RuntimeAsync=$ASYNC)"
rm -rf "$OUT_DIR"

START=$(date +%s)
set +e
OUTPUT="$(dotnet publish "$SOURCE_DIR/$RID_HEAD/$RID_HEAD.csproj" -c Release -r "$RID" -p:Platform="$RID_PLATFORM" -p:ExifGlassRuntimeAsync="$ASYNC_FLAG" -o "$OUT_DIR" 2>&1)"
CODE=$?
set -e
echo "$OUTPUT" | grep -E 'error|warning|->' | while IFS= read -r line; do report "  $line"; done || true

if [ $CODE -ne 0 ]; then
  report "FAILED: dotnet publish exit code $CODE"
  exit $CODE
fi

if [ -e "$OUT_DIR/exiftool" ]; then
  # A publish from any host can drop the script's execute bit
  chmod +x "$OUT_DIR/exiftool"
  EXIFTOOL="exiftool"
else
  EXIFTOOL="MISSING (see source/__assets/exiftool/README.md)"
fi

EXE_SIZE=$(wc -c < "$OUT_DIR/$RID_EXE" | tr -d ' ')
TOTAL_MB=$(find "$OUT_DIR" -type f ! -name '*.pdb' ! -name '*.dbg' ! -path '*.dSYM/*' -exec cat {} + | wc -c | awk '{printf "%.1f", $1/1048576}')
report "Executable: $EXE_SIZE bytes; payload without symbols: $TOTAL_MB MB; ExifTool: $EXIFTOOL; $(( $(date +%s) - START )) s"
report "PASSED: $OUT_DIR"
echo "Report: $REPORT"
