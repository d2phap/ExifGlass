#!/usr/bin/env bash
#
# Stages ExifGlass.app from a published osx-arm64 build, unsigned: pack-mac-dmg.sh and pack-mac-pkg-appstore.sh each sign a copy.
#   pack-mac-app.sh                        publish/osx-arm64      -> bundle/osx-arm64/ExifGlass.app (the DMG's)
#   pack-mac-app.sh --channel MacAppStore  publish/osx-arm64-mas  -> bundle/osx-arm64-mas/ExifGlass.app (the PKG's; EG_MAS build)
# It publishes a fresh NativeAOT build first (scripts/publish-aot.sh): the version is compiled in. SKIP_PUBLISH=1 reuses the last
# one, for local iteration only.

set -euo pipefail

CHANNEL="Direct"
while [[ $# -gt 0 ]]; do
	case "$1" in
		--channel) CHANNEL="$2"; shift 2 ;;
		*) echo "Usage: $0 [--channel Direct|MacAppStore]" >&2; exit 2 ;;
	esac
done
case "$CHANNEL" in
	Direct) SUFFIX="" ;;
	MacAppStore) SUFFIX="-mas" ;;
	*) echo "Error: unknown channel $CHANNEL" >&2; exit 2 ;;
esac

SOURCE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
MAC_ASSETS="$SOURCE_DIR/__assets/mac"
PUBLISH_DIR="$SOURCE_DIR/__artifacts/publish/osx-arm64$SUFFIX"
APP_DIR="$SOURCE_DIR/__artifacts/bundle/osx-arm64$SUFFIX/ExifGlass.app"
CONTENTS_DIR="$APP_DIR/Contents"
BUILD_PROPS_FILE="$SOURCE_DIR/Directory.Build.props"

prop() { sed -n "s:.*<$1>\(.*\)</$1>.*:\1:p" "$BUILD_PROPS_FILE" | head -n 1; }

EG_VERSION="$(prop ExifGlassVersion)"
[[ -n "$EG_VERSION" ]] || { echo "Error: could not read ExifGlassVersion from $BUILD_PROPS_FILE" >&2; exit 1; }

# CFBundleShortVersionString takes up to 3 integers; CFBundleVersion is the build number, which must rise for every App Store upload
EG_SHORT_VERSION="$(prop ExifGlassBundleShortVersion)"
[[ -n "$EG_SHORT_VERSION" ]] || EG_SHORT_VERSION="${EG_VERSION%.*}"
EG_BUILD="$(prop ExifGlassBundleBuild)"
[[ -n "$EG_BUILD" ]] || EG_BUILD="${EG_VERSION##*.}"

if [[ "${SKIP_PUBLISH:-0}" == "1" ]]; then
	echo "==> SKIP_PUBLISH=1: reusing $PUBLISH_DIR (local iteration only)"
else
	bash "$SOURCE_DIR/scripts/publish-aot.sh" osx-arm64 --channel "$CHANNEL"
fi
[[ -x "$PUBLISH_DIR/ExifGlass" ]] || { echo "Error: no published build at $PUBLISH_DIR" >&2; exit 1; }
[[ -f "$PUBLISH_DIR/exiftool" ]] || { echo "Error: no exiftool in $PUBLISH_DIR: drop ExifTool's Perl distribution into __assets/exiftool/unix (its README)" >&2; exit 1; }

# a bundle a sudo run left behind can't be removed by the user's rm
if [[ -d "$APP_DIR" ]]; then
	if ! rm -rf "$APP_DIR" 2>/dev/null || [[ -d "$APP_DIR" ]]; then
		echo "Error: could not remove $APP_DIR (owned by root?). Run: sudo rm -rf \"$APP_DIR\", then this script without sudo." >&2
		exit 1
	fi
fi

echo "==> Staging ExifGlass.app $EG_SHORT_VERSION ($EG_BUILD), $CHANNEL"
mkdir -p "$CONTENTS_DIR/MacOS" "$CONTENTS_DIR/Resources"
cp -R "$PUBLISH_DIR/." "$CONTENTS_DIR/MacOS/"
find "$CONTENTS_DIR" \( -name ".DS_Store" -o -name "._*" -o -name "*.pdb" \) -delete
find "$CONTENTS_DIR" -name "*.dSYM" -prune -exec rm -rf {} +

# codesign rejects anything but Mach-O in Contents/MacOS ("code object is not signed at all"): ExifTool and its lib, its config, the
# licence texts move to Resources and are linked back, so AppContext.BaseDirectory still finds them; ExifTool follows its own link to lib
for item in "$CONTENTS_DIR/MacOS/"*; do
	name="$(basename "$item")"
	if [[ -f "$item" ]] && file -b "$item" | grep -q '^Mach-O'; then continue; fi

	mv "$item" "$CONTENTS_DIR/Resources/$name"
	ln -s "../Resources/$name" "$item"
done

cp "$MAC_ASSETS/logo.icns" "$CONTENTS_DIR/Resources/logo.icns"
cp "$MAC_ASSETS/Info.plist" "$CONTENTS_DIR/Info.plist"
/usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString $EG_SHORT_VERSION" -c "Set :CFBundleVersion $EG_BUILD" "$CONTENTS_DIR/Info.plist"
plutil -lint "$CONTENTS_DIR/Info.plist" >/dev/null

chmod +x "$CONTENTS_DIR/MacOS/ExifGlass" "$CONTENTS_DIR/Resources/exiftool"
echo "Created bundle: $APP_DIR"
