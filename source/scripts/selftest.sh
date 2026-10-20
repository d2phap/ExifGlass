#!/usr/bin/env bash
# Runs --selftest on the published binary in __artifacts/publish/<rid>; a non-zero exit fails the script.
# Usage: scripts/selftest.sh [<rid>]
. "$(dirname "$0")/_common.sh"

RID="${1:-$(host_rid)}"
rid_info "$RID"
EXE="$ARTIFACTS_DIR/publish/$RID/$RID_EXE"
new_report "selftest-$RID"

if [ ! -x "$EXE" ]; then
  report "FAILED: $EXE not found; run scripts/publish-aot.sh first"
  exit 1
fi

set +e
OUTPUT="$("$EXE" --selftest 2>&1)"
CODE=$?
set -e
echo "$OUTPUT" | while IFS= read -r line; do report "$line"; done

report "Exit code: $CODE"
echo "Report: $REPORT"
exit $CODE
