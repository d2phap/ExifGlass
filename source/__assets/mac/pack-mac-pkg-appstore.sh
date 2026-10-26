#!/usr/bin/env bash
#
# Builds the Mac App Store package (plan 3, D7, S7): a sandboxed copy of bundle/osx-arm64-mas/ExifGlass.app (the EG_MAS build), its own
# Perl for ExifTool, signed for distribution, in a signed installer PKG.
# Run after pack-mac-app.sh --channel MacAppStore and build-perl.sh. Output: __artifacts/bundle/ExifGlass_<label>_mac-arm64-appstore.pkg
#
# One-time setup (developer.apple.com, App Store Connect):
#   1. An "Apple Distribution" (or "3rd Party Mac Developer Application") and a "Mac Installer Distribution" certificate in the login
#      keychain; the app is signed with whichever of them the profile was made with (App Store Connect refuses any other, 90284).
#   2. A "Mac App Store Connect" provisioning profile for com.duongdieuphap.exifglass, saved as
#      __artifacts/store/ExifGlass_AppStore.provisionprofile (git-ignored), or pointed to by PROVISIONING_PROFILE.
#   3. The app record in App Store Connect with that bundle ID.
#
# Overrides: SIGN_IDENTITY, INSTALLER_IDENTITY, PROVISIONING_PROFILE, TEAM_ID.
# VALIDATE=1 with API_KEY and API_ISSUER (an App Store Connect API key in ~/.appstoreconnect/private_keys) asks App Store Connect to
# validate the PKG; upload it with Transporter, or: xcrun altool --upload-package <pkg> -t macos --apiKey <key id> --apiIssuer <issuer id>

set -euo pipefail

TEAM_ID="${TEAM_ID:-7DV5HBKZ58}"
BUNDLE_ID="com.duongdieuphap.exifglass"

SOURCE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
MAC_ASSETS="$SOURCE_DIR/__assets/mac"
SOURCE_APP="$SOURCE_DIR/__artifacts/bundle/osx-arm64-mas/ExifGlass.app"
WORK_DIR="$SOURCE_DIR/__artifacts/staging/mac-pkg"
APP_DIR="$WORK_DIR/ExifGlass.app"
OUTPUT_DIR="$SOURCE_DIR/__artifacts/bundle"
BUILD_PROPS_FILE="$SOURCE_DIR/Directory.Build.props"
PROVISIONING_PROFILE="${PROVISIONING_PROFILE:-$SOURCE_DIR/__artifacts/store/ExifGlass_AppStore.provisionprofile}"
PERL_TREE="$SOURCE_DIR/__artifacts/perl/osx-arm64/perl"
trap 'rm -rf "$WORK_DIR"' EXIT

prop() { sed -n "s:.*<$1>\(.*\)</$1>.*:\1:p" "$BUILD_PROPS_FILE" | head -n 1; }

# the first identity of the team whose name starts with one of the given prefixes
find_identity() {
	local policy="$1"; shift
	local prefix line
	for prefix in "$@"; do
		line="$(security find-identity -v $policy | grep -F "\"$prefix" | grep -F "($TEAM_ID)" | head -n 1 || true)"
		if [[ -n "$line" ]]; then echo "$line" | sed -E 's/.*"(.*)".*/\1/'; return; fi
	done
}

[[ -d "$SOURCE_APP" ]] || { echo "Error: $SOURCE_APP not found; run pack-mac-app.sh --channel MacAppStore first." >&2; exit 1; }
INSTALLER_IDENTITY="${INSTALLER_IDENTITY:-$(find_identity "" "3rd Party Mac Developer Installer:" "Mac Installer Distribution:")}"
[[ -n "$INSTALLER_IDENTITY" ]] || { echo "Error: no Mac Installer Distribution certificate of team $TEAM_ID in the keychain (or set INSTALLER_IDENTITY)." >&2; exit 1; }
[[ -f "$PROVISIONING_PROFILE" ]] || { echo "Error: provisioning profile not found: $PROVISIONING_PROFILE (see the top of this script)." >&2; exit 1; }
[[ -x "$PERL_TREE/bin/perl" ]] || { echo "Error: no bundled Perl at $PERL_TREE; run __assets/mac/build-perl.sh first." >&2; exit 1; }

EG_VERSION="$(prop ExifGlassVersion)"
EG_RELEASE_TYPE="$(prop ExifGlassReleaseType)"
REL_LABEL="$EG_VERSION"
[[ -n "$EG_RELEASE_TYPE" ]] && REL_LABEL="$EG_VERSION-$EG_RELEASE_TYPE"
PKG_PATH="$OUTPUT_DIR/ExifGlass_${REL_LABEL}_mac-arm64-appstore.pkg"

rm -rf "$WORK_DIR"
mkdir -p "$WORK_DIR" "$OUTPUT_DIR"

# the profile's identifier must be this app's, and the app's entitlements carry it
security cms -D -i "$PROVISIONING_PROFILE" > "$WORK_DIR/profile.plist"
APP_IDENTIFIER="$(/usr/libexec/PlistBuddy -c "Print :Entitlements:com.apple.application-identifier" "$WORK_DIR/profile.plist")"
if [[ "$APP_IDENTIFIER" != "$TEAM_ID.$BUNDLE_ID" ]]; then
	echo "Error: the profile is for $APP_IDENTIFIER, not $TEAM_ID.$BUNDLE_ID" >&2
	exit 1
fi

# the keychain identity whose certificate the profile holds, by SHA-1: two certificates can share a name
if [[ -z "${SIGN_IDENTITY:-}" ]]; then
	i=0
	while /usr/libexec/PlistBuddy -c "Print :DeveloperCertificates:$i" "$WORK_DIR/profile.plist" > "$WORK_DIR/cert.der" 2>/dev/null; do
		sha1="$(openssl x509 -inform der -in "$WORK_DIR/cert.der" -noout -fingerprint -sha1 | sed 's/.*=//; s/://g')"
		line="$(security find-identity -v -p codesigning | grep -F " $sha1 " | head -n 1 || true)"
		if [[ -n "$line" ]]; then
			SIGN_IDENTITY="$sha1"
			SIGN_NAME="$(echo "$line" | sed -E 's/.*"(.*)".*/\1/')"
			break
		fi
		i=$((i + 1))
	done
	rm -f "$WORK_DIR/cert.der"
	[[ -n "${SIGN_IDENTITY:-}" ]] || { echo "Error: none of the profile's certificates is in the keychain with its private key; make the profile again with one that is (or set SIGN_IDENTITY)." >&2; exit 1; }
fi

echo "==> ExifGlass $REL_LABEL (arm64) for the Mac App Store"
echo "    App identity       : ${SIGN_NAME:-$SIGN_IDENTITY} ($SIGN_IDENTITY)"
echo "    Installer identity : $INSTALLER_IDENTITY"
echo "    Profile            : $PROVISIONING_PROFILE ($APP_IDENTIFIER)"

ditto "$SOURCE_APP" "$APP_DIR"
cp "$PROVISIONING_PROFILE" "$APP_DIR/Contents/embedded.provisionprofile"

# ExifTool's own Perl (plan 3, S7): App Review takes /usr/bin/perl for a deprecated technology (guideline 2.4.5).
# In Contents/Resources: Contents/Frameworks takes only signed code, and Perl's lib mixes .pm files with its XS bundles (S7 V6 is Apple's split layout)
echo "==> Bundling Perl"
PERL_DIR="$APP_DIR/Contents/Resources/perl"
mkdir -p "$PERL_DIR/bin"
cp "$PERL_TREE/bin/perl" "$PERL_DIR/bin/perl"
ditto "$PERL_TREE/lib" "$PERL_DIR/lib"
# make install leaves the modules read-only, which xattr and codesign can't write
chmod -R u+w "$PERL_DIR"
find "$PERL_DIR/lib" \( -name "*.h" -o -name "*.pod" \) -delete
echo "    $(du -sh "$PERL_DIR" | cut -f1), ExifTool $("$PERL_DIR/bin/perl" "$APP_DIR/Contents/Resources/exiftool" -ver)"

# App Store Connect's static scan rejects this string, which once failed CPython builds (S7)
flagged="$(grep -rlF "itms-services" "$PERL_DIR" "$APP_DIR/Contents/Resources/lib" 2>/dev/null || true)"
if [[ -n "$flagged" ]]; then
	echo "Error: file(s) carry the string App Store Connect rejects (itms-services):" >&2
	echo "$flagged" | sed 's:^:       :' >&2
	exit 1
fi

cp "$MAC_ASSETS/ExifGlass.mas.entitlements" "$WORK_DIR/app.entitlements"
/usr/libexec/PlistBuddy -c "Add :com.apple.application-identifier string $APP_IDENTIFIER" "$WORK_DIR/app.entitlements"
/usr/libexec/PlistBuddy -c "Add :com.apple.developer.team-identifier string $TEAM_ID" "$WORK_DIR/app.entitlements"

strays="$(find "$APP_DIR/Contents/MacOS" -mindepth 1 -maxdepth 1 ! -type l ! -name "*.dylib" ! -name "ExifGlass")"
if [[ -n "$strays" ]]; then
	echo "Error: non-code item(s) in Contents/MacOS; pack-mac-app.sh moves them to Resources:" >&2
	echo "$strays" | sed 's:^:       :' >&2
	exit 1
fi

# App Store Connect refuses a file carrying com.apple.quarantine (91109), which a profile downloaded with a browser has
xattr -cr "$APP_DIR"
quarantined="$(xattr -lr "$APP_DIR" 2>/dev/null | grep -F com.apple.quarantine || true)"
if [[ -n "$quarantined" ]]; then
	echo "Error: file(s) still carry com.apple.quarantine:" >&2
	echo "$quarantined" | sed 's:^:       :' >&2
	exit 1
fi

echo "==> Signing"
while IFS= read -r -d '' lib; do
	codesign --force --timestamp --options runtime --sign "$SIGN_IDENTITY" "$lib"
done < <(find "$APP_DIR/Contents/MacOS" -type f -name "*.dylib" -print0)
# Perl's XS bundles load into a Perl of the same team (library validation); the Perl itself runs in the app's sandbox
while IFS= read -r -d '' lib; do
	if file -b "$lib" | grep -q '^Mach-O'; then codesign --force --timestamp --options runtime --sign "$SIGN_IDENTITY" "$lib"; fi
done < <(find "$PERL_DIR/lib" -type f -name "*.bundle" -print0)
codesign --force --timestamp --options runtime --identifier "$BUNDLE_ID.perl" \
	--entitlements "$MAC_ASSETS/ExifGlass.perl.entitlements" --sign "$SIGN_IDENTITY" "$PERL_DIR/bin/perl"
codesign --force --timestamp --options runtime --entitlements "$WORK_DIR/app.entitlements" --sign "$SIGN_IDENTITY" "$APP_DIR"
codesign --verify --deep --strict --verbose=2 "$APP_DIR"
# App Store Connect refuses any Mach-O file not signed by the team (90284), wherever it sits
# (output compared whole: under pipefail, grep -q's early exit fails codesign with SIGPIPE)
unsigned="$(find "$APP_DIR" -type f -print0 | xargs -0 file | grep ': *Mach-O' | cut -d: -f1 | while read -r f; do
	info="$(codesign -dv "$f" 2>&1 || true)"
	[[ "$info" == *"TeamIdentifier=$TEAM_ID"* ]] || echo "$f"
done)"
if [[ -n "$unsigned" ]]; then
	echo "Error: Mach-O file(s) not signed by team $TEAM_ID:" >&2
	echo "$unsigned" | sed 's:^:       :' >&2
	exit 1
fi
codesign -d --entitlements - "$APP_DIR" 2>/dev/null | sed 's:^:    :'

echo "==> Building the PKG"
rm -f "$PKG_PATH"
productbuild --component "$APP_DIR" /Applications --sign "$INSTALLER_IDENTITY" "$PKG_PATH"
pkgutil --check-signature "$PKG_PATH"

if [[ "${VALIDATE:-0}" == "1" ]]; then
	[[ -n "${API_KEY:-}" && -n "${API_ISSUER:-}" ]] || { echo "Error: VALIDATE=1 needs API_KEY and API_ISSUER." >&2; exit 1; }
	echo "==> Validating with App Store Connect"
	xcrun altool --validate-app -f "$PKG_PATH" -t macos --apiKey "$API_KEY" --apiIssuer "$API_ISSUER"
fi

echo ""
echo "Done: $PKG_PATH"
echo "  sha256: $(shasum -a 256 "$PKG_PATH" | cut -d' ' -f1)"
echo "  Next  : upload it with Transporter; the build number is ExifGlassBundleBuild, which must rise for every upload."
