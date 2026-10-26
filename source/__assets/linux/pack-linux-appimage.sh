#!/usr/bin/env bash
#
# Packs the Linux build of ExifGlass as one AppImage (linux-x64, plan 3), ImageGlass's and FewerPhotos's packer:
#   1. publishes a fresh NativeAOT build (scripts/publish-aot.sh; the version is compiled in), with the PAR-packed ExifTool;
#   2. stages an AppDir: the payload under usr/lib/exifglass, a minimal AppRun, the Flatpak's desktop entry, metainfo and icons;
#   3. prints the glibc floor of the shipped binaries: an AppImage runs on the host's glibc;
#   4. builds ExifGlass_<label>_linux-x64.AppImage and its .zsync, with update information AppImage managers read, GPG-signed when GPG_KEY is set.
# Like the Flatpak, it ships no system library (libGL, libX11, libfontconfig): those must be the host's.
#
# Env: GPG_KEY=<key id>, APPIMAGETOOL=<path>, APPIMAGETOOL_SHA256=<hex> (checked on download), NO_UPDATE_INFO=1,
#      SKIP_PUBLISH=1 (reuses the last publish: local iteration only)

set -euo pipefail

export ARCH=x86_64
SOURCE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PUBLISH_DIR="$SOURCE_DIR/__artifacts/publish/linux-x64"
FLATPAK_DIR="$SOURCE_DIR/__assets/linux/flatpak"
DIST_DIR="$SOURCE_DIR/__artifacts/bundle"
BUILD_ROOT="$SOURCE_DIR/__artifacts/staging/linux-appimage"
# not APPDIR: the AppImage runtime exports that name and AppRun reads it
APPDIR_STAGE="$BUILD_ROOT/AppDir"
TOOLS_DIR="$SOURCE_DIR/__artifacts/tools"
BUILD_PROPS_FILE="$SOURCE_DIR/Directory.Build.props"
APP_ID="io.github.d2phap.exifglass"
GPG_KEY="${GPG_KEY:-}"
trap 'rm -rf "$BUILD_ROOT"' EXIT

prop() { sed -n "s:.*<$1>\(.*\)</$1>.*:\1:p" "$BUILD_PROPS_FILE" | head -n 1; }

EG_VERSION="$(prop ExifGlassVersion)"
[[ -n "$EG_VERSION" ]] || { echo "Error: could not read ExifGlassVersion from $BUILD_PROPS_FILE" >&2; exit 1; }
EG_RELEASE_TYPE="$(prop ExifGlassReleaseType)"
REL_LABEL="$EG_VERSION"
[[ -n "$EG_RELEASE_TYPE" ]] && REL_LABEL="$EG_VERSION-$EG_RELEASE_TYPE"
APPIMAGE_NAME="ExifGlass_${REL_LABEL}_linux-x64.AppImage"
APPIMAGE_PATH="$DIST_DIR/$APPIMAGE_NAME"
ZSYNC_PATH="$APPIMAGE_PATH.zsync"

# GitHub's "latest" release excludes pre-releases, which a release type marks
ZSYNC_TAG="latest"
[[ -n "$EG_RELEASE_TYPE" ]] && ZSYNC_TAG="latest-all"
UPDATE_INFO=""
[[ "${NO_UPDATE_INFO:-0}" == "1" ]] || UPDATE_INFO="gh-releases-zsync|d2phap|ExifGlass|$ZSYNC_TAG|ExifGlass_*_linux-x64.AppImage.zsync"

if [[ "${SKIP_PUBLISH:-0}" == "1" ]]; then
	echo "==> SKIP_PUBLISH=1: reusing $PUBLISH_DIR (local iteration only)"
else
	bash "$SOURCE_DIR/scripts/publish-aot.sh" linux-x64
fi
[[ -x "$PUBLISH_DIR/ExifGlass" ]] || { echo "Error: no published build at $PUBLISH_DIR" >&2; exit 1; }
if ! head -c 4 "$PUBLISH_DIR/exiftool" 2>/dev/null | grep -q 'ELF'; then
	echo "WARNING: ExifTool ships as its Perl script, so it needs the host's Perl; build the PAR binary with"
	echo "         bash __assets/linux/build-exiftool.sh x64 before a release"
fi

echo "==> Staging the AppDir"
rm -rf "$BUILD_ROOT"
mkdir -p "$APPDIR_STAGE/usr/lib/exifglass" "$APPDIR_STAGE/usr/bin" "$APPDIR_STAGE/usr/share/applications" \
	"$APPDIR_STAGE/usr/share/metainfo" "$APPDIR_STAGE/usr/share/icons/hicolor/512x512/apps" "$APPDIR_STAGE/usr/share/icons/hicolor/scalable/apps" "$DIST_DIR"

# the whole payload: a file list would drop what a later version adds
( cd "$PUBLISH_DIR" && cp -a . "$APPDIR_STAGE/usr/lib/exifglass/" )
find "$APPDIR_STAGE/usr/lib/exifglass" -type f \( -name "*.dbg" -o -name "*.pdb" \) -delete
chmod +x "$APPDIR_STAGE/usr/lib/exifglass/ExifGlass" "$APPDIR_STAGE/usr/lib/exifglass/exiftool"
ln -sf ../lib/exifglass/ExifGlass "$APPDIR_STAGE/usr/bin/exifglass"

# the image is read-only: a portable marker would keep the settings for one run only (D19)
[[ ! -e "$APPDIR_STAGE/usr/lib/exifglass/.egportable" ]] || { echo "Error: .egportable must never ship in an AppImage" >&2; exit 1; }
# the host's GL, X11 and fontconfig must be used: a bundled copy breaks on other drivers and distros
bundled="$(find "$APPDIR_STAGE" \( -name 'libGL*.so*' -o -name 'libEGL*.so*' -o -name 'libX11*.so*' -o -name 'libxcb*.so*' -o -name 'libfontconfig*.so*' -o -name 'libfreetype*.so*' \) -print)"
if [[ -n "$bundled" ]]; then
	echo "Error: system libraries in the payload:" >&2
	echo "$bundled" | sed 's:^:       :' >&2
	exit 1
fi

cp "$FLATPAK_DIR/$APP_ID.png" "$APPDIR_STAGE/$APP_ID.png"
cp "$FLATPAK_DIR/$APP_ID.png" "$APPDIR_STAGE/usr/share/icons/hicolor/512x512/apps/$APP_ID.png"
cp "$FLATPAK_DIR/$APP_ID.svg" "$APPDIR_STAGE/usr/share/icons/hicolor/scalable/apps/$APP_ID.svg"
ln -sf "$APP_ID.png" "$APPDIR_STAGE/.DirIcon"

# one metainfo for both packages: it describes the app, and Flathub is the stricter reader
cp "$FLATPAK_DIR/$APP_ID.metainfo.xml" "$APPDIR_STAGE/usr/share/metainfo/$APP_ID.metainfo.xml"
cp "$FLATPAK_DIR/$APP_ID.desktop" "$APPDIR_STAGE/$APP_ID.desktop"
printf 'X-AppImage-Name=ExifGlass\nX-AppImage-Version=%s\nX-AppImage-Arch=%s\n' "$REL_LABEL" "$ARCH" >> "$APPDIR_STAGE/$APP_ID.desktop"
cp "$APPDIR_STAGE/$APP_ID.desktop" "$APPDIR_STAGE/usr/share/applications/$APP_ID.desktop"
if command -v desktop-file-validate >/dev/null 2>&1; then desktop-file-validate "$APPDIR_STAGE/$APP_ID.desktop"; fi

cat > "$APPDIR_STAGE/AppRun" <<'APPRUN_EOF'
#!/bin/sh
# Everything exported here reaches every host program the app starts (xdg-open, ExifTool), so it exports nothing but APPDIR,
# and never clears APPIMAGE: ChannelPolicy reads it to know it runs as an AppImage.

# the runtime exports APPDIR, an extracted squashfs-root/AppRun doesn't
if [ -z "${APPDIR:-}" ]; then
    APPDIR="$(dirname "$(readlink -f "$0")")"
    export APPDIR
fi

# relative file arguments follow the folder the user started from
if [ -n "${OWD:-}" ] && [ -d "$OWD" ]; then
    cd "$OWD" || :
fi

# exec, not a child: one process, named ExifGlass
exec "$APPDIR/usr/lib/exifglass/ExifGlass" "$@"
APPRUN_EOF
chmod 755 "$APPDIR_STAGE/AppRun"
sh -n "$APPDIR_STAGE/AppRun"

# the highest symbol version any shipped binary needs is the oldest distro it runs on
GLIBC_FLOOR="$(
	{ find "$APPDIR_STAGE/usr/lib/exifglass" -type f \( -name "ExifGlass" -o -name "exiftool" -o -name "*.so" -o -name "*.so.*" \) -print0 \
	  | xargs -0 -r -n1 objdump -T 2>/dev/null || true; } \
	| grep -o 'GLIBC_[0-9][0-9.]*' | sort -uV | tail -n1 || true
)"
echo "==> glibc floor: ${GLIBC_FLOOR:-unknown}"

# no distro packages appimagetool: cache AppImage/appimagetool (its runtime reads zstd) under __artifacts
TOOL="${APPIMAGETOOL:-}"
if [[ -z "$TOOL" ]] && command -v appimagetool >/dev/null 2>&1; then TOOL="$(command -v appimagetool)"; fi
if [[ -z "$TOOL" ]]; then
	TOOL="$TOOLS_DIR/appimagetool-x86_64.AppImage"
	if [[ ! -x "$TOOL" ]]; then
		echo "==> Downloading appimagetool to $TOOLS_DIR"
		mkdir -p "$TOOLS_DIR"
		curl -fsSL --retry 2 -o "$TOOL.part" "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage"
		GOT="$(sha256sum "$TOOL.part" | cut -d' ' -f1)"
		if [[ -n "${APPIMAGETOOL_SHA256:-}" && "$GOT" != "$APPIMAGETOOL_SHA256" ]]; then
			rm -f "$TOOL.part"
			echo "Error: appimagetool sha256 $GOT, expected $APPIMAGETOOL_SHA256 ('continuous' moves: pin it again on purpose)" >&2
			exit 1
		fi
		echo "    sha256 $GOT (pin it: APPIMAGETOOL_SHA256=$GOT)"
		mv "$TOOL.part" "$TOOL"
		chmod +x "$TOOL"
	fi
fi

SIGN_ARGS=()
if [[ -z "$GPG_KEY" ]]; then
	echo "==> GPG_KEY empty: an unsigned AppImage"
elif gpg --list-secret-keys "$GPG_KEY" >/dev/null 2>&1; then
	SIGN_ARGS=(--sign --sign-key "$GPG_KEY")
else
	echo "WARNING: no secret GPG key '$GPG_KEY' in the keyring: an unsigned AppImage (gpg --quick-generate-key \"$GPG_KEY\" default default never makes one)" >&2
fi

UPDATE_ARGS=()
[[ -n "$UPDATE_INFO" ]] && UPDATE_ARGS=(-u "$UPDATE_INFO")

echo "==> Building $APPIMAGE_NAME"
rm -f "$APPIMAGE_PATH" "$ZSYNC_PATH"
# extract-and-run works in containers and without libfuse2; from DIST_DIR, since zsyncmake writes the .zsync into the working folder
( cd "$DIST_DIR" && APPIMAGE_EXTRACT_AND_RUN=1 "$TOOL" --comp zstd ${SIGN_ARGS[@]+"${SIGN_ARGS[@]}"} ${UPDATE_ARGS[@]+"${UPDATE_ARGS[@]}"} "$APPDIR_STAGE" "$APPIMAGE_PATH" )
chmod +x "$APPIMAGE_PATH"

# an image that names an update it has no .zsync for fails every check with "None of the artifacts matched"
if [[ -n "$UPDATE_INFO" ]]; then
	embedded="$("$APPIMAGE_PATH" --appimage-updateinformation 2>/dev/null || true)"
	[[ "$embedded" == "$UPDATE_INFO" ]] || echo "WARNING: the image's update information reads '${embedded:-<none>}', not '$UPDATE_INFO'"
	[[ -f "$ZSYNC_PATH" ]] || echo "WARNING: no $ZSYNC_PATH: this appimagetool has no zsyncmake (install zsync, or drop APPIMAGETOOL)"
fi

echo ""
echo "Done: $APPIMAGE_PATH ($(du -h "$APPIMAGE_PATH" | cut -f1))"
echo "  sha256      : $(sha256sum "$APPIMAGE_PATH" | cut -d' ' -f1)"
[[ -f "$ZSYNC_PATH" ]] && echo "  zsync       : $ZSYNC_PATH (upload it with the image)"
echo "  glibc floor : ${GLIBC_FLOOR:-unknown}"
if [[ ${#SIGN_ARGS[@]} -gt 0 ]]; then echo "  GPG key     : $GPG_KEY (publish its fingerprint: gpg --fingerprint $GPG_KEY)"; fi
echo "  Try it      : $APPIMAGE_PATH [file]   (no FUSE? add --appimage-extract-and-run)"
