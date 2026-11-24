#!/usr/bin/env bash
# Publishes a trimmed NativeAOT build into __artifacts/publish/<rid>; trim and AOT warnings are errors.
# Usage: scripts/publish-aot.sh [osx-arm64|linux-x64] [--runtime-async off] [--channel MacAppStore]
# The Mac App Store flavor (EG_MAS) goes to __artifacts/publish/<rid>-mas.
# linux-x64 links against the pinned glibc 2.31 sysroot (__assets/linux/linux-x64-sysroot.sh)
# so the binary runs on older distros; EG_NO_SYSROOT=1 skips that for local iteration.
. "$(dirname "$0")/_common.sh"

RID=""
ASYNC="on"
CHANNEL="Direct"
while [ $# -gt 0 ]; do
  case "$1" in
    --runtime-async) ASYNC="$2"; shift 2 ;;
    --channel) CHANNEL="$2"; shift 2 ;;
    *) RID="$1"; shift ;;
  esac
done
[ -z "$RID" ] && RID="$(host_rid)"
rid_info "$RID"

ASYNC_FLAG="true"
[ "$ASYNC" = "off" ] && ASYNC_FLAG="false"
case "$CHANNEL" in
  Direct) SUFFIX="" ;;
  MacAppStore) SUFFIX="-mas" ;;
  *) echo "Unsupported channel: $CHANNEL (Direct or MacAppStore)" >&2; exit 2 ;;
esac
OUT_DIR="$ARTIFACTS_DIR/publish/$RID$SUFFIX"
new_report "publish-aot-$RID$SUFFIX"

report "Publishing $RID_HEAD for $RID (Platform=$RID_PLATFORM, RuntimeAsync=$ASYNC, Channel=$CHANNEL)"
rm -rf "$OUT_DIR"

# NativeAOT links against the BUILD host's glibc, so a build from a new distro will not start on
# older ones (#21). EG_NO_SYSROOT=1 skips it for fast local iteration.
SYSROOT_ARGS=()
if [ "$RID" = "linux-x64" ] && [ "${EG_NO_SYSROOT:-0}" != "1" ]; then
  . "$SOURCE_DIR/__assets/linux/linux-x64-sysroot.sh"
  eg_sysroot_prepare "$ARTIFACTS_DIR/tools"
  eg_force_native_relink "$SOURCE_DIR/$RID_HEAD" "$RID"
  SYSROOT_ARGS=(-p:SysRoot="$EG_SYSROOT")
  report "Linking against the glibc $EG_SYSROOT_GLIBC sysroot: $EG_SYSROOT"
fi

START=$(date +%s)
set +e
OUTPUT="$(dotnet publish "$SOURCE_DIR/$RID_HEAD/$RID_HEAD.csproj" -c Release -r "$RID" -p:Platform="$RID_PLATFORM" -p:ExifGlassRuntimeAsync="$ASYNC_FLAG" -p:ExifGlassChannel="$CHANNEL" ${SYSROOT_ARGS[@]+"${SYSROOT_ARGS[@]}"} -o "$OUT_DIR" 2>&1)"
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
