# Linux packaging

`linux-x64` only. Three artifacts (plan §3), in `__artifacts/bundle/`, each from a fresh `scripts/publish-aot.sh linux-x64`:

| Task | Script | Output |
|---|---|---|
| `pack-linux-x64-flatpak` | `pack-linux-flatpak.sh` | `ExifGlass_<label>_linux-x64.tar.gz` (the source Flathub's manifest points at) and `ExifGlass_<label>_linux-x64.flatpak` (direct install) |
| `pack-linux-x64-appimage` | `pack-linux-appimage.sh` | `ExifGlass_<label>_linux-x64.AppImage` and its `.zsync` |
| `build-exiftool-linux-x64` | `build-exiftool.sh x64` | the PAR-packed ExifTool both packages ship (once per ExifTool version) |

`pack-linux-x64-all` runs both packers. Each task prompts for a GPG key (blank builds unsigned).

- **ExifTool:** neither package may rely on a Perl: the Flatpak runtime has none, and an AppImage runs on any distro. `build-exiftool.sh` packs ExifTool and Perl into one binary (`__assets/exiftool/unix/build/linux-x64/exiftool`), which `Directory.Build.targets` prefers; the Flatpak packer refuses the plain script, the AppImage packer warns.
- **One description:** the desktop entry, the metainfo and the icons live in `flatpak/` and serve both packages. The icons are rendered from `__assets/__app/logo.svg` by `tools/gen-app-icons.cs`. Add a `<release>` to the metainfo for every version (the Flatpak packer warns when its newest isn't this label).
- **Channel:** `ChannelPolicy.Detect` reads `FLATPAK_ID` (no update check, no download row in About) and `APPIMAGE` (update check on, as the DMG and the ZIP). `ExifGlass --selftest` prints it.

## AppImage

- **Payload:** under `usr/lib/exifglass`, run by a minimal `AppRun` that exports nothing (every host program the app starts would inherit it) and never clears `APPIMAGE`.
- **Update information:** `gh-releases-zsync|d2phap|ExifGlass|latest|ExifGlass_*_linux-x64.AppImage.zsync` (`latest-all` for a pre-release), so AppImageUpdate, Gear Lever and AppMan can update it; upload the `.zsync` with the image. The app itself only tells about updates. `NO_UPDATE_INFO=1` leaves it out.
- **glibc floor:** printed on every run, since an AppImage runs on the host's glibc. Build on the oldest distro you support.
- **No system libraries:** the packer fails if `libGL`, `libEGL`, `libX11`, `libxcb`, `libfontconfig` or `libfreetype` ever lands in the payload: they must be the host's.
- **appimagetool** is downloaded once to `__artifacts/tools` (AppImage/appimagetool, continuous; pin it with `APPIMAGETOOL_SHA256`), or set `APPIMAGETOOL`.
- **ImageGlass 10** runs `flatpak run io.github.d2phap.exifglass <file>` by default; AppImage users set the tool's path to the image.
- **Signing:** `GPG_KEY=<key id>` embeds a GPG signature (an X.509 code-signing certificate can't be used); publish the key's fingerprint and the printed SHA-256.

## Flatpak

See [`flatpak/README.md`](flatpak/README.md).
