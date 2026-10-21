#!/usr/bin/env bash
# Runs --perf on the published binary, on the GPU path and the software reference (plan 8): startup, idle memory, a file's load, panel motion, 50 repeated loads.
# Usage: scripts/perf.sh [<file> [<exiftool arguments> [<rid>]]]
# Example (about 2,150 rows): scripts/perf.sh ../__temp/fixtures-private/general/RAW_SONY_ILCE-7M2.ARW '-fast -G -t -m -q -H -charset UTF8 -u -a'
. "$(dirname "$0")/_common.sh"

FILE="${1:-}"
ARGUMENTS="${2:-}"
RID="${3:-$(host_rid)}"
rid_info "$RID"
EXE="$ARTIFACTS_DIR/publish/$RID/$RID_EXE"
new_report "perf-$RID"

if [ ! -x "$EXE" ]; then
  report "FAILED: $EXE not found; run scripts/publish-aot.sh first"
  exit 1
fi

CODE=0
for RENDER in gpu software; do
  # each run starts from fresh settings, so the window opens at the first-run size (D15) and the user's settings stay untouched
  SETTINGS="$(mktemp -d)"
  ARGS=(--perf)
  if [ "$RENDER" = "software" ]; then ARGS+=(--render software); fi
  if [ -n "$ARGUMENTS" ]; then ARGS+=("-p:ExifToolArguments=$ARGUMENTS"); fi
  if [ -n "$FILE" ]; then ARGS+=("$(cd "$(dirname "$FILE")" && pwd)/$(basename "$FILE")"); fi

  report ""
  report "## render $RENDER"

  set +e
  OUTPUT="$(EG_SETTINGS_DIR="$SETTINGS" "$EXE" "${ARGS[@]}" 2>&1)"
  RUN_CODE=$?
  set -e
  echo "$OUTPUT" | while IFS= read -r line; do report "$line"; done
  rm -rf "$SETTINGS"

  report "Exit code: $RUN_CODE"
  if [ $RUN_CODE -ne 0 ]; then CODE=$RUN_CODE; fi
done

echo "Report: $REPORT"
exit $CODE
