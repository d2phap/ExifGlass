# ExifGlass Flatpak

Files used to build and distribute ExifGlass as a Flatpak (linux-x64 only).

| File | Purpose |
|---|---|
| `io.github.d2phap.exifglass.yaml` | Flatpak manifest (submitted to Flathub). |
| `io.github.d2phap.exifglass.metainfo.xml` | AppStream metadata (required by Flathub). |
| `io.github.d2phap.exifglass.desktop` | Desktop launcher entry. |
| `io.github.d2phap.exifglass.svg` / `.png` | Icons, generated from `__assets/__app/logo.*` by the pack script. |

Build script: [`../pack-flatpak.sh`](../pack-flatpak.sh) (VS Code task: `pack-linux-x64-flatpak`).

The manifest installs the prebuilt `publish-linux-x64` binary instead of compiling
from source. Building .NET 10 AOT (trimming + SkiaSharp/HarfBuzz native interop)
offline on Flathub's builders isn't practical, so the manifest downloads a release
tarball with a pinned sha256. The bundle also embeds the self-contained `exiftool`
(Perl compiled in via PAR::Packer) because the freedesktop runtime has no system Perl.

## Build

```bash
# one-time
sudo apt install flatpak-builder
flatpak install -y flathub org.freedesktop.Platform//25.08 org.freedesktop.Sdk//25.08

# then, from the source/ directory (or via the "pack-linux-x64-flatpak" VS Code task,
# which compiles the self-contained ExifTool first):
bash __assets/linux/build-exiftool.sh x64     # once, or when bumping ExifTool
bash __assets/linux/pack-flatpak.sh
```

The pack script re-publishes a fresh AOT build every run, so you don't need to run
`publish-linux-x64` beforehand.

Outputs to `__artifacts/bundle/`:

- `ExifGlass_<version>_linux_x64.tar.gz` — payload the Flathub manifest points at.
- `ExifGlass_<version>_linux-x64.flatpak` — single-file bundle for direct install.

The script also installs the build for your user, so you can test it:

```bash
flatpak run io.github.d2phap.exifglass ~/Pictures/some-image.jpg
```

## Distribute on GitHub Releases

Self-hosted, no review, available immediately. Upload both output files to the
release matching the tag (default `<ExifGlassVersion>`; override with `RELEASE_TAG=<tag>`).
Users install the bundle directly:

```bash
flatpak install --user ExifGlass_<version>_linux-x64.flatpak
```

To sign the bundle, generate a key once and pass it via `GPG_KEY` (the VS Code task
prompts for it):

```bash
gpg --quick-generate-key "ExifGlass Release Signing" default default never
GPG_KEY="<your-key-id-or-email>" bash __assets/linux/pack-flatpak.sh
```

Signing is optional for a single-file bundle; users can install it either way.

## Submit to Flathub

1. The tarball must be reachable at the manifest's `url`, so cut the GitHub release
   first. The script already wrote the matching `url` + `sha256`.
2. Edit `io.github.d2phap.exifglass.metainfo.xml` so each `<screenshot>` URL points
   at a real HTTPS image. Flathub rejects submissions whose screenshots don't load.
3. The license is declared as `GPL-3.0-or-later` (GPLv3).
4. Fork [`flathub/flathub`](https://github.com/flathub/flathub), branch
   `io.github.d2phap.exifglass`, add the manifest + metadata files from this folder,
   and open a PR against `new-pr`. Process:
   <https://docs.flathub.org/docs/for-app-authors/submission>.
5. The `io.github.d2phap.*` id is verified through your GitHub account (`d2phap`),
   so no domain ownership is needed. After acceptance you get the
   `flathub/io.github.d2phap.exifglass` repo; future releases bump the version,
   `url`, `sha256`, and the `<release>` entry.

## Sandbox permissions

`finish-args` grants X11 (`--socket=x11` + `--share=ipc`), GPU (`--device=dri`),
network (`--share=network`, for the update check), and full filesystem
(`--filesystem=host`, for opening images and exporting metadata / extracted binary
tags anywhere). Flathub reviewers may ask to narrow `--filesystem=host` to
`--filesystem=home`.

`--filesystem=host` (or `home`) also covers ImageGlass 10's tool socket: ImageGlass
passes `--pipe ~/.cache/ImageGlass/tools/ig_<hex>`, an absolute path in the real
home. The narrowest grant that keeps the ImageGlass integration is
`--filesystem=~/.cache/ImageGlass/tools:ro`; never the `xdg-cache/ImageGlass/tools`
form, which follows the host's `XDG_CACHE_HOME` while ImageGlass always uses
`~/.cache`. Without either, ExifGlass opens, can't connect, and ImageGlass ends it
10 s later.

## ExifTool / libcrypt note

The bundled `exiftool` is a PAR::Packer binary with Perl embedded; its `libperl`
needs `libcrypt.so.1` (symbol `crypt@XCRYPT_2.0`). The `org.freedesktop` runtime
ships that library only as `libcrypt.so.2`, so the manifest symlinks
`/app/lib/libcrypt.so.1 -> libcrypt.so.2` and sets `LD_LIBRARY_PATH=/app/lib`. The
soname is the only difference — `libcrypt.so.2` provides the `crypt@XCRYPT_2.0`
symbol Perl imports, so no rebuild of libxcrypt is required.
