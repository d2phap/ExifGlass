#!/usr/bin/env bash
# Runs every test project and writes a report; --runtime-async off rebuilds without Runtime Async (A/B run).
# Usage: scripts/test.sh [--runtime-async on|off] [--filter <test filter>]
. "$(dirname "$0")/_common.sh"

ASYNC="on"
FILTER=""
while [ $# -gt 0 ]; do
  case "$1" in
    --runtime-async) ASYNC="$2"; shift 2 ;;
    --filter) FILTER="$2"; shift 2 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done

ASYNC_FLAG="true"
NAME="test"
if [ "$ASYNC" = "off" ]; then ASYNC_FLAG="false"; NAME="test-async-off"; fi
case "$(host_rid)" in
  *-arm64) PLATFORM="ARM64" ;;
  *) PLATFORM="x64" ;;
esac
new_report "$NAME"

PROJECTS="$(find "$SOURCE_DIR/tests" -name '*.Tests.csproj' | sort)"
report "RuntimeAsync=$ASYNC, Platform=$PLATFORM, $(echo "$PROJECTS" | grep -c .) test project(s)"
FAILED=0

while IFS= read -r project; do
  report "== $(basename "$project" .csproj)"
  set +e
  if [ -n "$FILTER" ]; then
    OUTPUT="$(dotnet test "$project" -c Debug -p:Platform="$PLATFORM" -p:ExifGlassRuntimeAsync="$ASYNC_FLAG" --filter "$FILTER" 2>&1)"
  else
    OUTPUT="$(dotnet test "$project" -c Debug -p:Platform="$PLATFORM" -p:ExifGlassRuntimeAsync="$ASYNC_FLAG" 2>&1)"
  fi
  CODE=$?
  set -e
  echo "$OUTPUT" | grep -E 'Passed!|Failed!|Skipped |\[FAIL\]|error |Error Message|Assert\.' | while IFS= read -r line; do report "  $line"; done || true

  if [ $CODE -ne 0 ]; then
    FAILED=$((FAILED + 1))
    report "  FAILED (exit $CODE)"
  fi
done <<< "$PROJECTS"

if [ $FAILED -eq 0 ]; then report "PASSED: all test projects"; else report "FAILED: $FAILED test project(s)"; fi
echo "Report: $REPORT"
exit $FAILED
