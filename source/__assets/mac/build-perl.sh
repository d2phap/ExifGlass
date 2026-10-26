#!/usr/bin/env bash
#
# Builds the relocatable Perl the Mac App Store build runs ExifTool on (plan 3, S7) into __artifacts/perl/osx-arm64, runs ExifTool on
# it, and signs every Mach-O file when SIGN_IDENTITY is set. pack-mac-pkg-appstore.sh ships it in Contents/Resources/perl, where
# ExifToolLocator.BundledPerl finds it; the DMG build keeps /usr/bin/perl. FewerPhotos's script-build-perl.sh.
#
# Env: PERL_VERSION (default 5.40.2), PERL_SHA256 (checked when set; printed otherwise, to pin), SIGN_IDENTITY,
#      ENTITLEMENTS (sign with these, e.g. ExifGlass.perl.entitlements)

set -euo pipefail

PERL_VERSION="${PERL_VERSION:-5.40.2}"
SOURCE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
BUILD_DIR="$SOURCE_DIR/__artifacts/perl/build"
OUT_DIR="$SOURCE_DIR/__artifacts/perl/osx-arm64"
TARBALL="$BUILD_DIR/perl-$PERL_VERSION.tar.gz"
EXIFTOOL="$SOURCE_DIR/__assets/exiftool/unix/exiftool"

[[ -f "$EXIFTOOL" ]] || { echo "Error: no ExifTool at $EXIFTOOL (see __assets/exiftool/README.md)" >&2; exit 1; }

mkdir -p "$BUILD_DIR"
if [[ ! -f "$TARBALL" ]]; then
	echo "==> Downloading perl-$PERL_VERSION"
	curl -fsSL --retry 2 -o "$TARBALL.part" "https://www.cpan.org/src/5.0/perl-$PERL_VERSION.tar.gz"
	mv "$TARBALL.part" "$TARBALL"
fi

GOT="$(shasum -a 256 "$TARBALL" | cut -d' ' -f1)"
if [[ -n "${PERL_SHA256:-}" && "$GOT" != "$PERL_SHA256" ]]; then
	echo "Error: perl-$PERL_VERSION.tar.gz sha256 $GOT, expected $PERL_SHA256" >&2
	exit 1
fi
[[ -n "${PERL_SHA256:-}" ]] || echo "    sha256 $GOT (unpinned: compare it with cpan.org's, then pass PERL_SHA256)"

rm -rf "$BUILD_DIR/perl-$PERL_VERSION" "$OUT_DIR"
tar -xzf "$TARBALL" -C "$BUILD_DIR"

# userelocatableinc: @INC follows the binary, so the tree runs from wherever the bundle puts it
echo "==> Configuring and building (several minutes)"
(
	cd "$BUILD_DIR/perl-$PERL_VERSION"
	./Configure -des -Dprefix=/perl -Duserelocatableinc -Dman1dir=none -Dman3dir=none -Uusethreads \
		-Accflags="-mmacosx-version-min=14.0" -Aldflags="-mmacosx-version-min=14.0" >/dev/null
	make -j"$(sysctl -n hw.ncpu)" >/dev/null
	make install DESTDIR="$OUT_DIR" >/dev/null
)

# ExifTool needs neither the docs nor the build tools
rm -rf "$OUT_DIR/perl/lib/perl5/$PERL_VERSION/pod" "$OUT_DIR/perl/lib/perl5/$PERL_VERSION/unicore/TestProp.pl"
find "$OUT_DIR/perl/lib" -name "*.pod" -delete

PERL="$OUT_DIR/perl/bin/perl"
echo "==> ExifTool on it: $("$PERL" "$EXIFTOOL" -ver)"
"$PERL" "$EXIFTOOL" -j -a -G0:1 "$SOURCE_DIR/tests/fixtures/ai/a1111.png" >/dev/null

MACHO=()
while IFS= read -r -d '' f; do
	if file -b "$f" | grep -q '^Mach-O'; then MACHO+=("$f"); fi
done < <(find "$OUT_DIR" -type f -print0)
echo "==> ${#MACHO[@]} Mach-O files, $(du -sh "$OUT_DIR" | cut -f1) in all"

if [[ -n "${SIGN_IDENTITY:-}" ]]; then
	ENT_ARGS=()
	[[ -n "${ENTITLEMENTS:-}" ]] && ENT_ARGS=(--entitlements "$ENTITLEMENTS")
	for f in "${MACHO[@]}"; do
		codesign --force --timestamp --options runtime ${ENT_ARGS[@]+"${ENT_ARGS[@]}"} --sign "$SIGN_IDENTITY" "$f"
	done
	codesign --verify --strict "$PERL"
	echo "==> Signed every Mach-O file; ExifTool on the signed Perl: $("$PERL" "$EXIFTOOL" -ver)"
fi

echo "Done: $OUT_DIR/perl"
