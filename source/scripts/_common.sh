#!/usr/bin/env bash
# Shared helpers for the ExifGlass local scripts (sourced); bash 3.2 compatible for macOS.
set -euo pipefail

SOURCE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARTIFACTS_DIR="$SOURCE_DIR/__artifacts"

host_os_name() {
  case "$(uname -s)" in
    Darwin) echo "mac" ;;
    Linux) echo "linux" ;;
    *) echo "win" ;;
  esac
}

host_rid() {
  local arch
  case "$(uname -m)" in
    arm64|aarch64) arch="arm64" ;;
    *) arch="x64" ;;
  esac
  case "$(host_os_name)" in
    mac) echo "osx-$arch" ;;
    linux) echo "linux-$arch" ;;
    *) echo "win-$arch" ;;
  esac
}

# Sets RID_PLATFORM, RID_HEAD and RID_EXE for a RID.
rid_info() {
  local rid="$1"
  case "$rid" in
    *-arm64) RID_PLATFORM="ARM64" ;;
    *) RID_PLATFORM="x64" ;;
  esac
  case "$rid" in
    win-*) RID_HEAD="ExifGlass.Win32"; RID_EXE="ExifGlass.exe" ;;
    osx-*) RID_HEAD="ExifGlass.Mac"; RID_EXE="ExifGlass" ;;
    linux-*) RID_HEAD="ExifGlass.Linux"; RID_EXE="ExifGlass" ;;
    *) echo "Unsupported RID: $rid" >&2; exit 2 ;;
  esac
}

# Creates this run's report file: __artifacts/reports/<date>/<name>-<os>.txt
new_report() {
  local dir="$ARTIFACTS_DIR/reports/$(date +%Y-%m-%d)"
  mkdir -p "$dir"
  REPORT="$dir/$1-$(host_os_name).txt"
  echo "# $1 · $(date '+%Y-%m-%d %H:%M:%S') · $(uname -srm) · $(host_rid)" > "$REPORT"
}

# Prints a line and appends it to the report.
report() {
  echo "$1"
  echo "$1" >> "$REPORT"
}
