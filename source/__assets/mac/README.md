# macOS packaging

Apple silicon only (`osx-arm64`), macOS 14 and later (.NET 11's NativeAOT floor). Two artifacts (plan §3), in `__artifacts/bundle/`:

| Task | Script | Output |
|---|---|---|
| `pack-mac-arm64-app` | `pack-mac-app.sh` | `osx-arm64/ExifGlass.app`, unsigned, from a fresh `scripts/publish-aot.sh osx-arm64` |
| `pack-mac-arm64-dmg` | `pack-mac-dmg.sh` | `ExifGlass_<label>_mac-arm64.dmg`: Developer ID, hardened runtime, notarized and stapled (GitHub) |
| `pack-mac-arm64-app-mas` | `pack-mac-app.sh --channel MacAppStore` | `osx-arm64-mas/ExifGlass.app`, unsigned, from the `EG_MAS` build (`publish/osx-arm64-mas`) |
| `pack-mac-arm64-pkg` | `pack-mac-pkg-appstore.sh` | `ExifGlass_<label>_mac-arm64-appstore.pkg`: sandboxed, signed for the Mac App Store |
| `build-perl-mac-arm64` | `build-perl.sh` | `__artifacts/perl/osx-arm64/perl`, the relocatable Perl the PKG carries (once per Perl version) |

`pack-mac-arm64-all` runs the DMG and PKG chains. The DMG and PKG scripts each sign a copy of their staged app.
A Claude session on the MacBook can run them; the keychain and password prompts stay yours.

## The bundle

- `Contents/MacOS` holds only Mach-O files: `ExifGlass` and its dylibs (SkiaSharp, HarfBuzzSharp, Avalonia native). ExifTool (the Perl script and its `lib`), `exifglass.config` and the licence texts move to `Contents/Resources` and are linked back, so `AppContext.BaseDirectory` finds them; ExifTool follows its link to find `lib`.
- `Info.plist`: bundle ID `com.duongdieuphap.exifglass`, `CFBundleShortVersionString` = `ExifGlassBundleShortVersion`, `CFBundleVersion` = `ExifGlassBundleBuild` (which must rise for every App Store upload), `LSMinimumSystemVersion` 14.0, `ITSAppUsesNonExemptEncryption` false, `CFBundleDocumentTypes` as viewer with `Alternate` rank. The executable keeps its name: ImageGlass 10 runs `/Applications/ExifGlass.app/Contents/MacOS/ExifGlass`.
- `logo.icns` is rendered from `__assets/__app/logo.svg` by `tools/gen-app-icons.cs`.

## DMG (Developer ID)

No entitlements: NativeAOT needs none, every dylib is signed with our Team ID so library validation holds, and ExifTool runs on `/usr/bin/perl`. Prerequisites: the Developer ID Application certificate and a notarytool keychain profile (`imageglass-notary` by default; the account's credentials serve every app). Overrides: `SIGN_IDENTITY`, `NOTARY_PROFILE`.

## PKG (Mac App Store, D7, S7)

The `EG_MAS` build compiles out the ImageGlass hosts and the update check, hides the custom ExifTool row, and runs ExifTool only on copies in the container: the sandbox gives ExifTool none of the files the user opens (S7). `SandboxStaging` copies the opened file to `~/Library/Caches/ExifGlass/stage` inside the container for every read; a save writes that copy, moves ExifTool's `FILE_original` to `Backups/<date>/` next to the settings, and writes the result over the user's file in place (the same inode keeps Finder tags, xattrs and the creation date).

- **Entitlements:** `ExifGlass.mas.entitlements` (`app-sandbox`, `files.user-selected.read-write`; the script adds the application and team identifiers from the profile). No network entitlement: the store build checks for no update. No `files.bookmarks.app-scope`: 3.0 keeps no recent files, so it reopens nothing after a relaunch.
- **Perl:** App Review takes `/usr/bin/perl` for a deprecated technology (guideline 2.4.5), so the PKG carries `build-perl.sh`'s Perl in `Contents/Resources/perl`, signed with `ExifGlass.perl.entitlements` (`app-sandbox` + `inherit`, all a helper may have), its XS bundles with the hardened runtime. `ExifToolLocator.BundledPerl` runs the script on it. Whether App Store Connect accepts Mach-O files under `Resources` is S7's open question S4; Apple's split layout (S7 V6) is the fallback.
- **Checks before `productbuild`:** the profile's identifier, the signing certificate picked by the profile's SHA-1, no non-code item in `Contents/MacOS`, no quarantine flag (91109), every Mach-O signed by the team (90284), no `itms-services` string in the Perl tree.
- **Prerequisites:** an Apple Distribution and a Mac Installer Distribution certificate, a Mac App Store Connect provisioning profile in `__artifacts/store/ExifGlass_AppStore.provisionprofile` (v2's is in git history: `git show 73a1bd5^:source/__assets/mac/appstore/ExifGlass_AppStore.provisionprofile`, check it hasn't expired), and the app record in App Store Connect. `VALIDATE=1 API_KEY=<id> API_ISSUER=<id>` asks App Store Connect to validate the PKG; upload it with Transporter.
- A Distribution-signed build never launches on a Mac outside TestFlight. To try the sandbox locally, sign the staged `osx-arm64-mas/ExifGlass.app` ad hoc with `ExifGlass.mas.entitlements` (and the Perl with `ExifGlass.perl.entitlements`) and open it; its container is `~/Library/Containers/com.duongdieuphap.exifglass`.
