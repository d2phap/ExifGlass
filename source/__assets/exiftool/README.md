# Bundled ExifTool

ExifGlass wraps the command-line [ExifTool](https://exiftool.org) by Phil Harvey. Each
platform head bundles its own ExifTool build; this folder is the one shared place those
builds are dropped in.

Both subfolders are **git-ignored** (large + freely re-obtainable) — only the shared
[`LICENSE`](LICENSE) is tracked. A fresh clone therefore has neither, so re-populate them
from an [exiftool.org](https://exiftool.org) release before publishing (`Directory.Build.targets`
guards every ExifTool item on `Exists(...)`, so a clone *without* the assets still builds —
but the app then has no ExifTool and `--selftest` fails its `exiftool` check).

| Folder  | Used by         | Contents                                                                |
|---------|-----------------|-------------------------------------------------------------------------|
| `win/`  | Windows head    | The compiled Windows build: `exiftool.exe` + its `exiftool_files/` runtime |
| `unix/` | Linux + macOS   | The Perl distribution: the `exiftool` script + `lib/`                    |

> **Drop in, don't patch.** The release artifacts go in unmodified — a version bump is
> just a re-drop, not a hand-curated merge. Currently bundled: **ExifTool 13.59**.

## Getting the source

All downloads come from the official [exiftool.org](https://exiftool.org) home page.

### Windows — `win/`

1. Download the **Windows Executable** zip, e.g. `exiftool-13.59_64.zip` (the `_64` = 64-bit).
2. Unzip it. It contains `exiftool(-k).exe` and an `exiftool_files/` folder.
3. Rename `exiftool(-k).exe` → **`exiftool.exe`** (dropping `(-k)` makes it non-interactive —
   it won't pause for a keypress).
4. Copy `exiftool.exe` and the whole `exiftool_files/` folder into `win/`.

Result:

```
win/
  exiftool.exe
  exiftool_files/          # bundled Strawberry-Perl runtime + lib/
```

No compilation step — the exe is already self-contained.

### Linux + macOS — `unix/`

1. Download the **macOS/Unix** tarball, e.g. `Image-ExifTool-13.59.tar.gz`.
2. Extract it. You get an `Image-ExifTool-<ver>/` folder containing the `exiftool` Perl
   script, `lib/`, and supporting files.
3. Copy its contents into `unix/`. At minimum you need the `exiftool` script and `lib/`.

Result (trimmed to the essentials):

```
unix/
  exiftool                 # the Perl script
  lib/                     # Image/ + File/ modules
```

## Building per platform

### Windows

Nothing to build — the bundled `exiftool.exe` runs as-is. The Windows head
bundles `win/exiftool.exe` + `exiftool_files/` verbatim.

### macOS

Nothing to compile — the macOS head ships the **`unix/exiftool` Perl script + `lib/`**
as-is and runs it on the **system Perl** (macOS always has Perl).

> It deliberately does *not* use a PAR-compiled binary like Linux: `pp` staples its
> archive past the Mach-O's `__LINKEDIT`, which `codesign` rejects — so a PAR binary
> can't be notarized. This matches ExifTool's own official
> macOS distribution. `pack-app.sh` relocates the script + `lib/` into
> `Contents/Resources/` (codesign rejects non-Mach-O under `Contents/MacOS/`) and
> symlinks `exiftool` back so the resolver finds it.

### Linux

To ship a **self-contained** `exiftool` (Perl embedded via PAR::Packer, so end users need
no system Perl), compile it from `unix/` with
[`../linux/build-exiftool.sh`](../linux/build-exiftool.sh) (or the VS Code
`build-exiftool-linux-<arch>` task):

```bash
# From source/ — Linux host only (pp can't cross-compile).
./__assets/linux/build-exiftool.sh x64            # add --force to rebuild
```

Prerequisites (Debian/Ubuntu — `libperl-dev` supplies the Perl headers `pp` links against):

```bash
sudo apt-get install -y perl cpanminus build-essential libperl-dev
sudo cpanm --notest PAR::Packer Archive::Zip Compress::Zlib Digest::SHA \
                    IO::Compress::Bzip2 Time::Piece IO::String
```

The output is cached per-RID at `unix/build/linux-<arch>/exiftool`. The Linux head
bundles that binary **if it exists**; otherwise it falls back to bundling the raw
`unix/exiftool` script + `lib/` (which then needs system Perl at runtime). The VS Code
`publish-aot-linux-x64` and `pack-linux-x64-flatpak` tasks run `build-exiftool-linux-x64`
first.

> `pp` bundles the *host* Perl + native libs, so it only builds for the OS/arch it runs on
> — build on a Linux runner (or WSL), per target architecture. Do **not** `strip` the
> result: PAR appends its archive as a trailing ELF overlay and `strip` discards it,
> silently truncating the binary into a broken PAR loader.

## Runtime resolution

The app runs the ExifTool next to its executable (`BundledExifTool`): `exiftool.exe`
on Windows, the compiled `exiftool` on Linux (or the Perl script fallback), and the
`exiftool` Perl script on macOS.
