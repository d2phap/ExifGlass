#!/usr/bin/env bash
# Plays ImageGlass 10 against the published binary (P10): live navigation, SHUTDOWN, the app or the host killed, Unicode, no ExifTool left behind.
# Usage: scripts/ipc.sh [all|ig10] [scenario|all] [runs] [exe]
# Each session opens an ExifGlass window, which ImageGlass's EXECUTE brings to the front.
source "$(dirname "$0")/_common.sh"

COMMAND="${1:-ig10}"
SCENARIO="${2:-all}"
RUNS="${3:-3}"
RID="$(host_rid)"
rid_info "$RID"
EXE="${4:-$ARTIFACTS_DIR/publish/$RID/$RID_EXE}"
new_report "ipc-$RID"

if [ ! -x "$EXE" ]; then
  echo "FAILED: $EXE not found; run scripts/publish-aot first" | tee -a "$REPORT"
  exit 1
fi

dotnet build "$SOURCE_DIR/tests/ExifGlass.IpcHost/ExifGlass.IpcHost.csproj" -c Release -v q -nologo
HOST_DLL="$(find "$SOURCE_DIR/tests/ExifGlass.IpcHost/bin/Release" -name ExifGlass.IpcHost.dll | head -n 1)"
OUT="$ARTIFACTS_DIR/ipc/$(host_os_name)"
echo "app: $EXE" | tee -a "$REPORT"

set +e
dotnet "$HOST_DLL" "$COMMAND" "$SCENARIO" --app "$EXE" --out "$OUT" --runs "$RUNS" 2>&1 | tee -a "$REPORT"
CODE="${PIPESTATUS[0]}"
set -e

echo "Tables: $OUT" | tee -a "$REPORT"
echo "Report: $REPORT"
exit "$CODE"
