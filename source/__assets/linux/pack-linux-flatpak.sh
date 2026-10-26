#!/usr/bin/env bash
#
# Packs the Linux build of ExifGlass for Flatpak (linux-x64, plan 3):
#   1. publishes a fresh NativeAOT build (scripts/publish-aot.sh; the version is compiled in), with the PAR-packed ExifTool;
#   2. tars the payload under one ExifGlass/ folder: ExifGlass_<label>_linux-x64.tar.gz, the source Flathub's manifest points at;
#   3. writes the release url and sha256 into flatpak/io.github.d2phap.exifglass.yaml;
#   4. with flatpak-builder and the 25.08 runtime, builds ExifGlass_<label>_linux-x64.flatpak (GPG-signed when GPG_KEY is set) and
#      installs it for the current user to try.
# Distribution steps: flatpak/README.md
#
# Env: GPG_KEY=<key id>, RELEASE_TAG=<tag> (default: the label), SKIP_PUBLISH=1 (reuses the last publish: local iteration only)

set -euo pipefail

SOURCE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PUBLISH_DIR="$SOURCE_DIR/__artifacts/publish/linux-x64"
FLATPAK_DIR="$SOURCE_DIR/__assets/linux/flatpak"
OUT_DIR="$SOURCE_DIR/__artifacts/bundle"
WORK_DIR="$SOURCE_DIR/__artifacts/staging/linux-flatpak"
STATE_DIR="$WORK_DIR/.flatpak-builder"
BUILD_PROPS_FILE="$SOURCE_DIR/Directory.Build.props"
MANIFEST="$FLATPAK_DIR/io.github.d2phap.exifglass.yaml"
APP_ID="io.github.d2phap.exifglass"
GPG_KEY="${GPG_KEY:-}"
trap 'rm -rf "$WORK_DIR"' EXIT

prop() { sed -n "s:.*<$1>\(.*\)</$1>.*:\1:p" "$BUILD_PROPS_FILE" | head -n 1; }

EG_VERSION="$(prop ExifGlassVersion)"
[[ -n "$EG_VERSION" ]] || { echo "Error: could not read ExifGlassVersion from $BUILD_PROPS_FILE" >&2; exit 1; }
EG_RELEASE_TYPE="$(prop ExifGlassReleaseType)"
REL_LABEL="$EG_VERSION"
[[ -n "$EG_RELEASE_TYPE" ]] && REL_LABEL="$EG_VERSION-$EG_RELEASE_TYPE"

RELEASE_TAG="${RELEASE_TAG:-$REL_LABEL}"
TARBALL_NAME="ExifGlass_${REL_LABEL}_linux-x64.tar.gz"
BUNDLE_NAME="ExifGlass_${REL_LABEL}_linux-x64.flatpak"
TARBALL_PATH="$OUT_DIR/$TARBALL_NAME"
BUNDLE_PATH="$OUT_DIR/$BUNDLE_NAME"
RELEASE_URL="https://github.com/d2phap/ExifGlass/releases/download/${RELEASE_TAG}/${TARBALL_NAME}"

if [[ "${SKIP_PUBLISH:-0}" == "1" ]]; then
	echo "==> SKIP_PUBLISH=1: reusing $PUBLISH_DIR (local iteration only)"
else
	bash "$SOURCE_DIR/scripts/publish-aot.sh" linux-x64
fi
[[ -x "$PUBLISH_DIR/ExifGlass" ]] || { echo "Error: no published build at $PUBLISH_DIR" >&2; exit 1; }

# the runtime has no Perl, so only the PAR-packed ExifTool runs in the sandbox
if ! head -c 4 "$PUBLISH_DIR/exiftool" 2>/dev/null | grep -q 'ELF'; then
	echo "Error: $PUBLISH_DIR/exiftool isn't the self-contained binary; build it first: bash __assets/linux/build-exiftool.sh x64" >&2
	exit 1
fi
# /app is read-only: a portable marker would keep the settings for one run only (D19)
[[ ! -e "$PUBLISH_DIR/.egportable" ]] || { echo "Error: .egportable must never ship in the Flatpak" >&2; exit 1; }

# the metainfo's newest release is what Flathub and the software centres show
TOP_RELEASE="$(sed -n 's:.*<release version="\([^"]*\)".*:\1:p' "$FLATPAK_DIR/$APP_ID.metainfo.xml" | head -n 1)"
[[ "$TOP_RELEASE" == "$REL_LABEL" ]] || echo "WARNING: the metainfo's newest <release> is $TOP_RELEASE, not $REL_LABEL (release checklist)"

echo "==> Staging the payload"
STAGE_DIR="$WORK_DIR/stage"
rm -rf "$WORK_DIR"
mkdir -p "$STAGE_DIR/ExifGlass" "$OUT_DIR"
( cd "$PUBLISH_DIR" && cp -a . "$STAGE_DIR/ExifGlass/" )
find "$STAGE_DIR/ExifGlass" -type f \( -name "*.dbg" -o -name "*.pdb" \) -delete

echo "==> Creating $TARBALL_NAME"
rm -f "$TARBALL_PATH"
tar -czf "$TARBALL_PATH" -C "$STAGE_DIR" ExifGlass
SHA256="$(sha256sum "$TARBALL_PATH" | cut -d' ' -f1)"

echo "==> Pointing the manifest at the release (url + sha256)"
sed -i -E "s#^( *)url: https://github.com/d2phap/ExifGlass/releases/download/.*#\1url: ${RELEASE_URL}#" "$MANIFEST"
sed -i -E "s#^( *)sha256: [0-9a-f]{64}#\1sha256: ${SHA256}#" "$MANIFEST"

RUNTIME_VER="$(sed -n "s/^runtime-version: *['\"]\?\([0-9.]*\).*/\1/p" "$MANIFEST" | head -n1)"
RUNTIME_VER="${RUNTIME_VER:-25.08}"
BUNDLE_BUILT=0
PUBKEY_FILE=""

if ! command -v flatpak-builder >/dev/null 2>&1; then
	echo "==> flatpak-builder not found: no bundle (sudo apt install flatpak-builder)"
elif ! { flatpak info "org.freedesktop.Platform//$RUNTIME_VER" >/dev/null 2>&1 && flatpak info "org.freedesktop.Sdk//$RUNTIME_VER" >/dev/null 2>&1; }; then
	echo "==> Runtime/SDK $RUNTIME_VER not installed: no bundle"
	echo "    flatpak install -y flathub org.freedesktop.Platform//$RUNTIME_VER org.freedesktop.Sdk//$RUNTIME_VER"
else
	echo "==> Building the Flatpak (bundle + user install)"
	LOCAL_DIR="$WORK_DIR/local"
	REPO_DIR="$WORK_DIR/repo"
	mkdir -p "$LOCAL_DIR"
	cp "$FLATPAK_DIR/$APP_ID.desktop" "$FLATPAK_DIR/$APP_ID.metainfo.xml" "$FLATPAK_DIR/$APP_ID.svg" "$FLATPAK_DIR/$APP_ID.png" "$LOCAL_DIR/"
	cp "$TARBALL_PATH" "$LOCAL_DIR/app.tar.gz"

	# the same manifest on the local tarball: url becomes path, sha256 goes
	sed -E -e "s#^( *)url: .*#\1path: app.tar.gz#" -e "/^ *sha256: [0-9a-f]{64}/d" "$MANIFEST" > "$LOCAL_DIR/$APP_ID.yaml"

	# the commit is signed and the public key embedded, or the bundle's origin remote has nothing to verify against
	GPG_SIGN_ARGS=()
	GPG_BUNDLE_ARGS=()
	if [[ -z "$GPG_KEY" ]]; then
		echo "==> GPG_KEY empty: an unsigned bundle"
	elif ! gpg --list-secret-keys "$GPG_KEY" >/dev/null 2>&1; then
		echo "WARNING: no secret GPG key '$GPG_KEY' in the keyring: an unsigned bundle (gpg --quick-generate-key \"$GPG_KEY\" default default never makes one)" >&2
	else
		PUBKEY_FILE="$WORK_DIR/$APP_ID.pubkey.gpg"
		gpg --export "$GPG_KEY" > "$PUBKEY_FILE"
		GPG_SIGN_ARGS=(--gpg-sign="$GPG_KEY")
		GPG_BUNDLE_ARGS=(--gpg-sign="$GPG_KEY" --gpg-keys="$PUBKEY_FILE")
	fi

	# --disable-cache: the module is a tarball of a constant name, so the cache would ship the previous build under the new version
	flatpak-builder --state-dir="$STATE_DIR" --user --install --force-clean --disable-cache \
		--repo="$REPO_DIR" ${GPG_SIGN_ARGS[@]+"${GPG_SIGN_ARGS[@]}"} "$WORK_DIR/build" "$LOCAL_DIR/$APP_ID.yaml"

	rm -f "$BUNDLE_PATH"
	flatpak build-bundle ${GPG_BUNDLE_ARGS[@]+"${GPG_BUNDLE_ARGS[@]}"} --runtime-repo=https://dl.flathub.org/repo/flathub.flatpakrepo \
		"$REPO_DIR" "$BUNDLE_PATH" "$APP_ID"
	BUNDLE_BUILT=1
fi

echo ""
echo "Done."
echo "  Tarball (Flathub source) : $TARBALL_PATH"
echo "  sha256                   : $SHA256"
echo "  Manifest url             : $RELEASE_URL"
if [[ "$BUNDLE_BUILT" == "1" ]]; then
	echo "  Bundle (direct install)  : $BUNDLE_PATH"
	echo "  sha256                   : $(sha256sum "$BUNDLE_PATH" | cut -d' ' -f1)"
	if [[ -n "$PUBKEY_FILE" ]]; then echo "  GPG key                  : $GPG_KEY (publish its fingerprint: gpg --fingerprint $GPG_KEY)"; fi
	echo "  Try it                   : flatpak run $APP_ID [file]"
	# a double-clicked bundle installs system-wide and shadows this user install
	if flatpak --system info "$APP_ID" >/dev/null 2>&1; then
		echo "WARNING: a system-wide install runs instead of this one: flatpak uninstall --system $APP_ID"
	fi
fi
