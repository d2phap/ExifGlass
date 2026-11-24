# Linux packaging

`linux-x64` only. Three artifacts (plan §3), in `__artifacts/bundle/`, each from a fresh `scripts/publish-aot.sh linux-x64`:

| Task | Script | Output |
|---|---|---|
| `pack-linux-x64-flatpak` | `pack-linux-flatpak.sh` | `ExifGlass_<label>_linux-x64.tar.gz` (the source Flathub's manifest points at) and `ExifGlass_<label>_linux-x64.flatpak` (direct install) |
| `pack-linux-x64-appimage` | `pack-linux-appimage.sh` | `ExifGlass_<label>_linux-x64.AppImage` and its `.zsync` |
| `build-exiftool-linux-x64` | `build-exiftool.sh x64` | the PAR-packed ExifTool both packages ship (once per ExifTool version) |

`pack-linux-x64-all` runs both packers. Each task prompts for a GPG key (blank builds unsigned).

- **ExifTool:** neither package may rely on a Perl: the Flatpak runtime has none, and an AppImage runs on any distro. `build-exiftool.sh` packs ExifTool and Perl into one binary (`__assets/exiftool/unix/build/linux-x64/exiftool`), which `Directory.Build.targets` prefers; the Flatpak packer refuses the plain script, the AppImage packer warns. Build it on an old glibc: see [glibc floor](#glibc-floor).
- **One description:** the desktop entry, the metainfo and the icons live in `flatpak/` and serve both packages. The icons are rendered from `__assets/__app/logo.svg` by `tools/gen-app-icons.cs`. The newest `<release>` tracks `ExifGlassVersion`: both packers insert it (`eg_sync_metainfo_release` in `_common.sh`) with today's date, `type="development"` when `ExifGlassReleaseType` is set, keeping the older entries. Hand-write the entry first if you want real release notes in it — a matching label is left alone.
- **Channel:** `ChannelPolicy.Detect` reads `FLATPAK_ID` (no update check, no download row in About) and `APPIMAGE` (update check on, as the DMG and the ZIP). `ExifGlass --selftest` prints it.

## glibc floor

Both packages run on the **host's** glibc (the Flatpak tarball is also a direct download), and
NativeAOT links against the *build* machine's glibc. A plain publish from Zorin 18 needed
`GLIBC_2.38` (`fmod`/`fmodf`) and would not start on Ubuntu 22.04 or older — [#21].

`linux-x64-sysroot.sh` fixes it, after ImageGlass's packer:

- **`EG_GLIBC_MAX` = 2.29** is the floor both packers enforce: **Ubuntu 20.04+, Mint 20+, Debian 11+,
  Fedora 30+, RHEL 9+**. `eg_check_glibc_floor` fails the pack and names the offending symbols.
  `GLIBC_MAX=<ver>` overrides it for debugging only.
- **glibc 2.28 distros are out of scope**: RHEL/Alma/Rocky 8, Debian 10 and Fedora 29 are one
  version below the floor, verified failing with `libm.so.6: version GLIBC_2.29 not found`. The 2.29
  comes entirely from `exp`/`log`/`log2`/`pow` in the Ubuntu 20.04 sysroot's libm; pinning a glibc
  2.28 sysroot instead (Debian 10, RHEL 8) would reach them, at the cost of an EOL deb source.
- **`publish-aot.sh linux-x64`** links against a pinned **Ubuntu 20.04 (glibc 2.31)** sysroot,
  assembled from sha256-checked `.deb`s cached in `__artifacts/tools/`. That lands the binary at
  exactly 2.29 — `exp`, `log`, `log2` and `pow` are the only symbols reaching it — so the floor
  sits below the sysroot's own glibc. `EG_NO_SYSROOT=1` skips it for fast local iteration, never
  for a release.
- **`LinkNative`'s only inputs are the object files**, so a `SysRoot` change alone never relinks;
  `eg_force_native_relink` deletes the previously linked binary first.
- **ExifTool is the exception**, and the sysroot cannot reach any of it. `pp` reuses the prebuilt
  `parl`, so the loader carries the glibc of whatever host `PAR::Packer` was installed on, and the
  zip it appends holds the **host's XS modules** (`POSIX.so`, `Time::Piece`, `MIME::Base64` …),
  which the loader extracts and `dlopen`s at run time. Those are the higher floor in practice: a
  Zorin 18 build needed `GLIBC_2.38` (`__isoc23_strtol`, `fmod`) from inside the archive, where the
  loader's own 2.34 hid them. `eg_check_glibc_floor` unzips and scans them, listed as
  `exiftool!<module>`. Build the whole thing once per ExifTool version on a glibc ≤ 2.29 host, so
  that its perl and XS modules come from there too; AlmaLinux 8 (glibc 2.28) is still maintained:

  ```bash
  __assets/linux/build-exiftool.sh x64 --container
  ```

  That re-runs the script inside `almalinux:8` (override with `EG_EXIFTOOL_IMAGE`), installing Perl
  and PAR::Packer there, so the loader and the XS modules both come from glibc 2.28. It needs
  `podman` or `docker` (`sudo apt install podman`; rootless podman writes the output as you rather
  than as root). Without `--container` the script builds against the host Perl, which is fine for
  development but not for a release.

  `build-exiftool.sh` prints the floor it produced and warns when it is too high; the packers then
  refuse that binary. `unzip` is needed for the in-archive scan — without it the check says so and
  passes the modules unchecked.
- **Check the real thing**, not just the symbols — the packers' floor is a static read, so run the
  built image on the oldest target too:

  ```bash
  podman run --rm -v "$PWD:/b:z" -w /b -e APPIMAGE_EXTRACT_AND_RUN=1 \
    docker.io/library/ubuntu:20.04 ./ExifGlass_<label>_linux-x64.AppImage --selftest
  ```

  From `__artifacts/bundle/`. All six selftest lines should read PASS, including the bundled
  ExifTool.

[#21]: https://github.com/d2phap/ExifGlass/issues/21

## AppImage

- **Payload:** under `usr/lib/exifglass`, run by a minimal `AppRun` that exports nothing (every host program the app starts would inherit it) and never clears `APPIMAGE`.
- **Update information:** `gh-releases-zsync|d2phap|ExifGlass|latest|ExifGlass_*_linux-x64.AppImage.zsync` (`latest-all` for a pre-release), so AppImageUpdate, Gear Lever and AppMan can update it; upload the `.zsync` with the image. The app itself only tells about updates. `NO_UPDATE_INFO=1` leaves it out.
- **glibc floor:** enforced on every run, since an AppImage runs on the host's glibc. See [glibc floor](#glibc-floor).
- **No system libraries:** the packer fails if `libGL`, `libEGL`, `libX11`, `libxcb`, `libfontconfig` or `libfreetype` ever lands in the payload: they must be the host's.
- **appimagetool** is downloaded once to `__artifacts/tools` (AppImage/appimagetool, continuous; pin it with `APPIMAGETOOL_SHA256`), or set `APPIMAGETOOL`.
- **ImageGlass 10** runs `flatpak run io.github.d2phap.exifglass <file>` by default; AppImage users set the tool's path to the image.
- **Signing:** `GPG_KEY=<key id>` embeds a GPG signature (an X.509 code-signing certificate can't be used); publish the key's fingerprint and the printed SHA-256.

## Flatpak

See [`flatpak/README.md`](flatpak/README.md).
