#!/usr/bin/env bash
#
# Signs a copy of bundle/osx-arm64/ExifGlass.app with the Developer ID, packs it in a DMG, notarizes and staples it (plan 3).
# Run after pack-mac-app.sh. Output: __artifacts/bundle/ExifGlass_<label>_mac-arm64.dmg
#
# One-time setup: the Developer ID Application certificate in the login keychain, and a notarytool profile (the account's credentials,
# so ImageGlass's serves every app):
#   xcrun notarytool store-credentials "imageglass-notary" --apple-id "you@example.com" --team-id "7DV5HBKZ58" --password "<app-specific password>"
#
# Overrides: SIGN_IDENTITY, NOTARY_PROFILE.

set -euo pipefail

SIGN_IDENTITY="${SIGN_IDENTITY:-Developer ID Application: Phap Duong (7DV5HBKZ58)}"
NOTARY_PROFILE="${NOTARY_PROFILE:-imageglass-notary}"

SOURCE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SOURCE_APP="$SOURCE_DIR/__artifacts/bundle/osx-arm64/ExifGlass.app"
WORK_DIR="$SOURCE_DIR/__artifacts/staging/mac-dmg"
APP_DIR="$WORK_DIR/ExifGlass.app"
DMG_STAGING_DIR="$WORK_DIR/dmg"
OUTPUT_DIR="$SOURCE_DIR/__artifacts/bundle"
BUILD_PROPS_FILE="$SOURCE_DIR/Directory.Build.props"
trap 'rm -rf "$WORK_DIR"' EXIT

prop() { sed -n "s:.*<$1>\(.*\)</$1>.*:\1:p" "$BUILD_PROPS_FILE" | head -n 1; }

[[ -d "$SOURCE_APP" ]] || { echo "Error: $SOURCE_APP not found; run pack-mac-app.sh first." >&2; exit 1; }
if ! security find-identity -v -p codesigning | grep -qF "$SIGN_IDENTITY"; then
	echo "Error: signing identity not in the keychain: $SIGN_IDENTITY" >&2
	exit 1
fi
# notarytool's own reason: a missing profile and an unaccepted agreement (HTTP 403) need different fixes
if ! notary_out="$(xcrun notarytool history --keychain-profile "$NOTARY_PROFILE" 2>&1)"; then
	echo "Error: notarytool check failed for keychain profile '$NOTARY_PROFILE':" >&2
	echo "$notary_out" | sed 's:^:       :' >&2
	echo "       No such profile? Create it with xcrun notarytool store-credentials (see the top of this script)." >&2
	echo "       HTTP 403? The Account Holder must accept the latest agreement at https://developer.apple.com/account" >&2
	exit 1
fi

EG_VERSION="$(prop ExifGlassVersion)"
EG_RELEASE_TYPE="$(prop ExifGlassReleaseType)"
REL_LABEL="$EG_VERSION"
[[ -n "$EG_RELEASE_TYPE" ]] && REL_LABEL="$EG_VERSION-$EG_RELEASE_TYPE"
DMG_PATH="$OUTPUT_DIR/ExifGlass_${REL_LABEL}_mac-arm64.dmg"

echo "==> ExifGlass $REL_LABEL (arm64) as a notarized DMG"
echo "    Identity : $SIGN_IDENTITY"
echo "    Profile  : $NOTARY_PROFILE"

rm -rf "$WORK_DIR"
mkdir -p "$WORK_DIR" "$OUTPUT_DIR"
ditto "$SOURCE_APP" "$APP_DIR"

# a real file or folder in Contents/MacOS that isn't Mach-O fails the bundle's signature with an unhelpful message
strays="$(find "$APP_DIR/Contents/MacOS" -mindepth 1 -maxdepth 1 ! -type l ! -name "*.dylib" ! -name "ExifGlass")"
if [[ -n "$strays" ]]; then
	echo "Error: non-code item(s) in Contents/MacOS; pack-mac-app.sh moves them to Resources:" >&2
	echo "$strays" | sed 's:^:       :' >&2
	exit 1
fi

# inside-out, hardened runtime, no entitlements: NativeAOT needs none, every dylib carries our Team ID, and ExifTool runs on /usr/bin/perl
echo "==> Signing"
while IFS= read -r -d '' lib; do
	codesign --force --timestamp --options runtime --sign "$SIGN_IDENTITY" "$lib"
done < <(find "$APP_DIR/Contents/MacOS" -type f -name "*.dylib" -print0)
codesign --force --timestamp --options runtime --sign "$SIGN_IDENTITY" "$APP_DIR"
codesign --verify --deep --strict --verbose=2 "$APP_DIR"

echo "==> Building the DMG"
mkdir -p "$DMG_STAGING_DIR"
cp -R "$APP_DIR" "$DMG_STAGING_DIR/"
ln -s /Applications "$DMG_STAGING_DIR/Applications"
rm -f "$DMG_PATH"
hdiutil create -volname "ExifGlass $REL_LABEL" -srcfolder "$DMG_STAGING_DIR" -fs HFS+ -format UDZO -ov "$DMG_PATH"
codesign --force --timestamp --sign "$SIGN_IDENTITY" "$DMG_PATH"

echo "==> Notarizing (a few minutes)"
xcrun notarytool submit "$DMG_PATH" --keychain-profile "$NOTARY_PROFILE" --wait
xcrun stapler staple "$DMG_PATH"
xcrun stapler validate "$DMG_PATH"
spctl --assess --type open --context context:primary-signature --verbose=2 "$DMG_PATH" || true

echo ""
echo "Done: $DMG_PATH"
echo "  sha256: $(shasum -a 256 "$DMG_PATH" | cut -d' ' -f1)"
