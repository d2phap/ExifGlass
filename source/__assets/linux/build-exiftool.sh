#!/usr/bin/env bash
#
# ExifGlass - EXIF Metadata Viewing Tool
# Copyright (C) 2023 - 2026 DUONG DIEU PHAP
# Project homepage: https://github.com/d2phap/ExifGlass
#
# This program is free software: you can redistribute it and/or modify
# it under the terms of the GNU General Public License as published by
# the Free Software Foundation, either version 3 of the License, or
# (at your option) any later version.
#
# This program is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU General Public License for more details.
#
# You should have received a copy of the GNU General Public License
# along with this program.  If not, see <https://www.gnu.org/licenses/>.
#
# -----------------------------------------------------------------------------
# Compiles a self-contained ExifTool executable from the bundled Perl distribution using
# PAR::Packer (pp) -- the same tool ExifTool uses to build its Windows exe. The result embeds a
# Perl interpreter plus every ExifTool module, so the shipped app needs NO system Perl.
#
# pp bundles the *host* Perl + native libs, so it can only build for the OS/arch it runs on --
# build on Linux (a Linux CI runner, or WSL on Windows), per target architecture. Driven by the
# VS Code "build-exiftool-linux-<arch>" tasks (see ../../../.vscode/tasks.json); run it by hand as:
#
#   ./build-exiftool.sh <arch> [--force]
#     x64       the only supported Linux arch (maps to the linux-x64 RID)
#     --force   rebuild even if the output binary already exists
#
# Requires PAR::Packer + ExifTool's helper modules. On Debian/Ubuntu (libperl-dev supplies the
# Perl headers pp links its C loader against -- build-essential alone is not enough):
#   sudo apt-get install -y perl cpanminus build-essential libperl-dev
#   sudo cpanm --notest PAR::Packer Archive::Zip Compress::Zlib Digest::SHA \
#                       IO::Compress::Bzip2 Time::Piece IO::String
#
# Source is the shared Perl distribution at __assets/exiftool/unix (the `exiftool` script + lib/).
# Output is cached per-RID at __assets/exiftool/unix/build/linux-<arch>/exiftool and picked up by
# ExifGlass.Linux.csproj. Idempotent: skips when the output already exists (so build/publish tasks
# that depend on it stay fast), unless --force is passed.
# -----------------------------------------------------------------------------
set -euo pipefail

ARCH="${1:?target architecture required: x64}"
FORCE="${2:-}"

case "$ARCH" in
  x64) ;;
  *) echo "ERROR: unsupported architecture '$ARCH' (expected: x64)." >&2; exit 1 ;;
esac

# Resolve paths relative to this script's location: __assets/linux -> __assets -> source.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOURCE_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
SRC="$SOURCE_DIR/__assets/exiftool/unix"
OUT="$SOURCE_DIR/__assets/exiftool/unix/build/linux-$ARCH/exiftool"

if [ -f "$OUT" ] && [ "$FORCE" != "--force" ]; then
  echo "ExifTool already built: $OUT  (pass '--force' as the 2nd argument to rebuild)"
  exit 0
fi

if [ ! -f "$SRC/exiftool" ]; then
  echo "ERROR: ExifTool source not found at '$SRC/exiftool'." >&2
  echo "       Put exiftool.org's Image-ExifTool distribution (the 'exiftool' script + 'lib/')" >&2
  echo "       under that folder first." >&2
  exit 1
fi

# --- Require PAR::Packer ------------------------------------------------------
if ! command -v pp >/dev/null 2>&1; then
  cat >&2 <<'EOF'
ERROR: PAR::Packer (`pp`) is not installed -- it is required to compile ExifTool.
Install it plus ExifTool's helper modules, e.g. on Debian/Ubuntu (libperl-dev supplies the Perl
headers pp links its C loader against -- build-essential alone is not enough):

  sudo apt-get install -y perl cpanminus build-essential libperl-dev
  sudo cpanm --notest PAR::Packer Archive::Zip Compress::Zlib Digest::SHA \
                      IO::Compress::Bzip2 Time::Piece IO::String

then build again. To skip compiling and ship the Perl source instead (the target machine will
then need Perl), build with:  dotnet build -p:BuildExifToolBinary=false
EOF
  exit 1
fi

mkdir -p "$(dirname "$OUT")"

echo "Compiling self-contained ExifTool -> $OUT"
# -I lib                  : ExifTool's module path (build-time dependency scan)
# -M 'Image::ExifTool::*' : force-include EVERY module ExifTool loads dynamically at runtime
#                           (format readers, Charset/*, Lang/* -- pp's static scanner can't see
#                           these `require`s). -M 'Image::ExifTool' adds the top module itself.
# -M <helper>             : CPAN modules ExifTool uses (mirrors ExifTool's own pp_build_exe.args,
#                           minus the Win32-only ones).
# -a <src;dest>           : non-module data files loaded at runtime (the geolocation database).
pp -o "$OUT" \
   -I "$SRC/lib" \
   -M 'Image::ExifTool' \
   -M 'Image::ExifTool::*' \
   -M 'File::RandomAccess' \
   -M 'Archive::Zip' -M 'Compress::Zlib' -M 'Digest::MD5' -M 'Digest::SHA' \
   -M 'IO::Compress::Bzip2' -M 'Time::HiRes' -M 'Time::Piece' -M 'IO::String' -M 'Encode' \
   -a "$SRC/lib/Image/ExifTool/Geolocation.dat;lib/Image/ExifTool/Geolocation.dat" \
   "$SRC/exiftool"

chmod +x "$OUT"
# NOTE: do NOT `strip` the result. PAR appends its archive (the embedded Perl + modules + the
# exiftool script) to the ELF as a trailing overlay; modern binutils `strip` rewrites the ELF and
# discards that overlay, silently truncating the binary so it degrades to a bare PAR loader
# ("par.pl: Can't open perl script ..."). The size saving isn't worth shipping a broken tool.

# --- Verify it runs standalone (no system Perl needed) -----------------------
# Check stdout carries a real version number (a truncated/broken PAR binary prints its errors to
# stderr and leaves stdout empty), and that stderr is clean.
ERR_LOG="$(mktemp)"
trap 'rm -f "$ERR_LOG"' EXIT
VER="$("$OUT" -ver 2>"$ERR_LOG" || true)"
if ! printf '%s' "$VER" | grep -Eq '^[0-9]+\.[0-9]+'; then
  echo "ERROR: the built ExifTool did not run ('$OUT' -ver did not print a version)." >&2
  [ -s "$ERR_LOG" ] && { echo "       stderr was:" >&2; sed 's/^/         /' "$ERR_LOG" >&2; }
  echo "       If it reports \"Can't locate <Module>.pm\", add '-M <Module>' to the pp command" >&2
  echo "       above and rebuild. If it mentions PAR/par.pl or a missing archive, the binary was" >&2
  echo "       truncated (e.g. by 'strip') -- rebuild without stripping." >&2
  exit 1
fi
if [ -s "$ERR_LOG" ]; then
  echo "ERROR: ExifTool ran but printed to stderr (the binary may be partially broken):" >&2
  sed 's/^/         /' "$ERR_LOG" >&2
  exit 1
fi
echo "OK: built ExifTool $VER ($(du -h "$OUT" | cut -f1))"
