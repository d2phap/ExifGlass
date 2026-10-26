# Windows packaging

Three artifacts per release (plan §3), each from a fresh NativeAOT publish (`scripts/publish-aot.ps1`), all in `__artifacts/bundle/`:

| Artifact | Script | Identity | Signed |
|---|---|---|---|
| `ExifGlass_<label>_win-<arch>.msix` (sideload, GitHub) | `pack-win-msix.ps1 -Platform <arch> -Sign` | `DuongDieuPhap.ExifGlass`, Publisher = the certificate's exact Subject | our binaries and the package |
| `ExifGlass_<label>_win-msstore.msixbundle` (Microsoft Store, x64 + arm64) | `pack-win-msix.ps1 -MsStore` | `9662DuongDieuPhap.ExifGlass`, `CN=29F1B9EC-D220-4DC3-BEDB-01A9CCA51904` | never: the Store signs it |
| `ExifGlass_<label>_win-<arch>.zip` (portable) | `pack-win-zip.ps1 -Platform <arch> -Sign` | none | our binaries (a ZIP can't be signed) |

`<label>` is `<ExifGlassVersion>[-<ExifGlassReleaseType>]` from `Directory.Build.props`, the GitHub tag too (no `v`).
`pack-win-all.ps1 [-Sign]` publishes x64 and arm64 once and builds all five files, then prints their sizes and SHA-256.

VS Code tasks: `pack-win-x64-msix`, `pack-win-arm64-msix`, `pack-win-msstore-msixbundle`, `pack-win-x64-zip`, `pack-win-arm64-zip`, `pack-win-all`.

## Files

- `_pack-win.ps1`: shared helpers (label and package version, fresh publish with the stale-build check, SDK tools, signing).
- `pack-win-msix.ps1`, `pack-win-zip.ps1`, `pack-win-all.ps1`: the packers.
- `appxmanifest/AppxManifest.xml`: the manifest template; `{{...}}` are filled in at pack time.
- `appxmanifest/Assets/`: the tiles, rendered from `__assets/__app/logo.svg` by `tools/gen-app-icons.cs` (with the `.ico`, the `.icns` and the Linux icon). Run it after a logo change: `cd source/tools && dotnet run gen-app-icons.cs`.

## Prerequisites

- The Windows 10/11 SDK: `makeappx`, `makepri` and `signtool`, found under `Windows Kits\10\bin`.
- The .NET SDK of `global.json`, and the MSVC build tools with ARM64 libraries for the arm64 packages (NativeAOT cross-compiles x64 to arm64 here).
- ExifTool for Windows in `__assets/exiftool/win` (its README).
- For `-Sign`: the code-signing certificate in `CurrentUser\My` or `LocalMachine\My`, picked by `-CertSubject` (default "Duong Dieu Phap"), or `-CertFile <pfx> -CertPassword <pw>`. Without one the package is built unsigned, with a warning.

## Notes

- **Never pack an old publish.** The version is compiled into the exe, so each packer publishes first. `-SkipPublish` reuses `__artifacts/publish/win-<arch>` for local iteration and still refuses one whose `ProductVersion` isn't this label.
- **Package version.** Both MSIX flavours carry `<Major>.<Minor>.<ExifGlassBundleBuild>.0` (`3.0.1.0`): the Store reserves the 4th part. The Store refuses a version not above its last one, so raise `ExifGlassBundleBuild` for every submission. `-PackageVersion` overrides it.
- **Channel.** `ChannelPolicy.Detect` reads the running package: a family name starting `9662DuongDieuPhap` is the Store build (no update check, no Store row in About), any other identity the sideload build, none the ZIP. Change `-MsStoreIdentityName` and that prefix together. `ExifGlass.exe --selftest` prints the channel and the settings folder.
- **Settings.** The MSIX builds use `%LOCALAPPDATA%\ExifGlass`. Since Windows 10 1903 a packaged app's *new* AppData files go to the package's own folder (`%LOCALAPPDATA%\Packages\<family>\LocalCache\Local\ExifGlass`, removed on uninstall), while files that already exist in the real folder are read and written in place ([Microsoft Learn](https://learn.microsoft.com/windows/msix/desktop/desktop-to-uwp-behind-the-scenes#file-system)): a v3 MSIX picks up the settings v2 or a non-portable ZIP left there, and migrates v2's. The ZIP keeps them next to `ExifGlass.exe` because of the `.egportable` marker (D19; `-NoPortable` leaves it out). The packers refuse a marker in an MSIX: its folder is read-only.
- **Capabilities.** `runFullTrust` only. v2's `broadFileSystemAccess` is for the UWP storage APIs, which a full-trust app doesn't use, and it needs a justification at Store review.
- **ImageGlass 10** runs `exifglass <file>` (its `Tool_ExifGlass`): the MSIX declares the `ExifGlass.exe` execution alias; ZIP users add the folder to `PATH` or set the tool's path. The bare alias is one slot per user: with the Store and the sideload package both installed (or v2 from the Store next to a v3 sideload), only one of them answers to `ExifGlass.exe`.
- **File types.** The `<uap:FileType>` list is the same set as the Linux `.desktop` `MimeType` and the macOS `CFBundleDocumentTypes`; keep the three together.
- **Signing.** Only our binaries are signed (`ExifGlass.exe` and the native DLLs next to it); `exiftool.exe` and `exiftool_files` ship as exiftool.org builds them. Signing happens in the staging copy, so a `-SkipPublish` run never signs a file twice. signtool's output goes to the host: in a function's output it would join the return value, and a failed run would read as success (FewerPhotos G8).
- **Trying a package.** A signed `.msix`: `Add-AppxPackage <msix>`, then `Remove-AppxPackage` with its full name. An unsigned one, or the Store bundle, with Developer Mode on: `makeappx unpack /p <msix> /d <folder>` (`makeappx unbundle` for the bundle, then unpack its x64 package), then `Add-AppxPackage -Register <folder>\AppxManifest.xml`. The registered layout keeps its identity, so the Store bundle's x64 package reads as a Store install; on a PC with ExifGlass from the Store, that replaces the Store install, so try it elsewhere. Never sign the Store bundle you upload. Reinstalling the same version with other content fails with `0x80073CFB`: remove it first.
- **Package identity** comes with Start, the alias or `Invoke-CommandInDesktopPackage -PackageFamilyName <family> -AppId EXIFGLASS -Command <exe>`; a registered layout's exe started by its path runs without it (its self-test says `WindowsZip`).
