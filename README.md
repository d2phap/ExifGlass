ExifGlass - AI & EXIF metadata viewer
===

ExifGlass shows the metadata inside your images, AI generations first. Open a picture made with Stable Diffusion, ComfyUI, InvokeAI, NovelAI, Fooocus, Midjourney and many more, and ExifGlass shows its prompt, negative prompt, models, LoRAs, sampler settings and seed, a summary of its workflow, and the provenance it carries. Every other tag ExifTool reads sits in one searchable table: camera settings, lens, exposure, GPS location, timestamps and more.

ExifGlass works on its own or beside [ImageGlass](https://imageglass.org), where it follows the photo you browse. It runs on Windows, macOS and Linux, and comes with [ExifTool by Phil Harvey](https://exiftool.org) built in, so there is nothing else to install.

ExifGlass is free and open source. To support its development and get updates through the store, consider buying it from the [Microsoft Store](https://apps.microsoft.com/detail/9MX8S9HZ57W8?launch=true&cid=github_readme&mode=full).

[![ExifGlass on Microsoft Store](https://github.com/d2phap/ExifGlass/assets/3154213/ed878a58-cec3-4f74-ac56-c215477f6c61)](https://apps.microsoft.com/detail/9MX8S9HZ57W8?launch=true&cid=github_readme&mode=full)

<a href="https://github.com/d2phap/ExifGlass/releases">
  <img src="https://img.shields.io/github/downloads/d2phap/exifglass/total?color=%23ed604c&label=total%20downloads&style=for-the-badge" /></a>

<a href="https://github.com/d2phap/ExifGlass/releases">
  <img src="https://img.shields.io/github/downloads/d2phap/exifglass/latest/total?color=%23ed604c&label=latest%20version&style=for-the-badge" />
</a>


<br/>
<img src="source/__assets/linux/screenshots/eg_flatpak_1.png" alt="ExifGlass showing the prompt, models and parameters of an AI image" />


## Features

### AI generation data
- The prompt, negative prompt, models, LoRAs and embeddings, sampler, scheduler, steps, CFG, seed and size, read from AUTOMATIC1111 and Forge, ComfyUI, InvokeAI, NovelAI, Fooocus, SwarmUI, Draw Things, Easy Diffusion, Tensor.Art and Midjourney files.
- ComfyUI workflows summarised (nodes, samplers, models) and saved as a `.json` file that loads back into ComfyUI.
- Provenance: C2PA Content Credentials (shown as not verified, because ExifGlass checks no signature), the IPTC digital source type, AI labels and AI-training signals.
- An "AI generation" group at the top of the table, each field linked to the tag it was read from.
- Copy the generation parameters as AUTOMATIC1111 text, or export them as text or JSON.

### Every tag
- One table for every tag ExifTool reads, grouped, with tag IDs: search, sort, hide groups, and quick filters for dates and times, GPS, converted values, prompts, models and provenance.
- An inspector for the selected tag, with the preview, the exposure, and the camera, lens, time and place.
- Copy a value, a row or a selection; export the rows in view as CSV, plain text or ExifTool-style JSON.

### Editing
- Edit, add and delete tags; remove the GPS data or the AI generation data before you share a file, or strip all metadata. Changes wait until you save, and ExifTool keeps the original file as a backup.
- The command bar shows the exact ExifTool command for the read, or for the save while changes wait, ready to copy.

### Private by design
- Everything stays on your computer: prompts and metadata are never sent anywhere, and nothing is looked up online. The only network request is the update check, which you can turn off (see [PRIVACY.md](PRIVACY.md)).


## Download

| System | Package | Notes |
| -- | -- | -- |
| Windows 10 and 11 (x64, Arm64) | [Microsoft Store](https://apps.microsoft.com/detail/9MX8S9HZ57W8?launch=true&cid=github_readme&mode=full) | Paid, supports development; the Store keeps it up to date |
| | `.msix` from [Releases](https://github.com/d2phap/ExifGlass/releases) | Signed installer; ExifGlass checks for updates |
| | Portable `.zip` from [Releases](https://github.com/d2phap/ExifGlass/releases) | Unzip and run `ExifGlass.exe`; the settings stay in that folder |
| macOS 14 or later (Apple silicon) | `.dmg` from [Releases](https://github.com/d2phap/ExifGlass/releases) | Notarized; drag ExifGlass to Applications |
| | Mac App Store | Paid, supports development; it can't connect to ImageGlass, because the App Sandbox doesn't allow it |
| Linux (x64) | [Flathub](https://flathub.org/apps/io.github.d2phap.exifglass) | |
| | `.AppImage` from [Releases](https://github.com/d2phap/ExifGlass/releases) | Make it executable and run it |


## Keyboard

| Keys | Action |
| -- | -- |
| <kbd>Ctrl</kbd> + <kbd>O</kbd> | Open a file (or drop one on the window) |
| <kbd>Ctrl</kbd> + <kbd>K</kbd> | Search tags |
| <kbd>Ctrl</kbd> + <kbd>A</kbd>, <kbd>Ctrl</kbd> + <kbd>C</kbd> | Select every row in view, copy the selection |
| <kbd>Ctrl</kbd> + <kbd>,</kbd> | Settings |
| <kbd>F1</kbd> | About |
| <kbd>Esc</kbd> | Step back: cancel an edit, close a menu or panel, clear the search, then the selection, then close the window |

On macOS, <kbd>Cmd</kbd> takes the place of <kbd>Ctrl</kbd>.


## Use ExifGlass with ImageGlass
ExifGlass follows the photo ImageGlass shows: as you browse in ImageGlass, the metadata table, the AI card and the inspector follow each photo. It works with ImageGlass 10 on Windows, macOS and Linux, and with ImageGlass 9 on Windows. The Mac App Store version can't connect to ImageGlass, because the App Sandbox doesn't allow it.

### ImageGlass 10
ImageGlass 10 comes with an ExifGlass tool already set up (ID `Tool_ExifGlass`, hotkey <kbd>X</kbd>). Press <kbd>X</kbd> to open ExifGlass beside the photo; it closes when ImageGlass closes. If the tool was removed, or ExifGlass is installed somewhere else, open Settings > Tools in ImageGlass, click "Add" and fill in:

| | Windows | macOS | Linux (Flatpak) | Linux (AppImage) |
| -- | -- | -- | -- | -- |
| ID | `Tool_ExifGlass` | `Tool_ExifGlass` | `Tool_ExifGlass` | `Tool_ExifGlass` |
| Integrated with ImageGlass.SDK | checked | checked | checked | checked |
| Executable | `exifglass` (the Store and MSIX versions add it to PATH; for the ZIP, the full path of `ExifGlass.exe`) | `/Applications/ExifGlass.app` | `flatpak` | the full path of the `.AppImage` file |
| Argument | `<file>` | `<file>` | `run io.github.d2phap.exifglass <file>` | `<file>` |
| Hotkey | <kbd>X</kbd> | <kbd>X</kbd> | <kbd>X</kbd> | <kbd>X</kbd> |

ImageGlass must start ExifGlass itself: a script or launcher in front of it breaks the connection on Windows.

> [!NOTE]
> If ImageGlass closes while ExifGlass has unsaved changes, ExifGlass keeps them and offers to restore them the next time it starts.

<img width="800" alt="image" src="https://github.com/user-attachments/assets/28c26471-1858-4928-885c-1b1b1cd34793" />

### ImageGlass 9 (Windows)
Add ExifGlass as an integrated tool, as [ImageGlass Docs / ImageGlass tools](https://imageglass.org/docs/imageglass-tools#add-your-tool-to-imageglass) describes: the executable is `ExifGlass.exe` (or `exifglass`) and the argument `<file>`. ExifGlass closes when you turn the tool off or close ImageGlass.

### Flatpak permissions
The ExifGlass Flatpak reaches ImageGlass 10 through its `--filesystem=host` permission. If you narrow its permissions (with Flatseal, for example), keep at least `~/.cache/ImageGlass/tools:ro`. The `xdg-cache/ImageGlass/tools` form doesn't work when `XDG_CACHE_HOME` is moved, because ImageGlass always uses `~/.cache`.



## Build ExifGlass from source code
- The [.NET 11 SDK](https://dotnet.microsoft.com/download) named in `source/global.json`.
- ExifTool dropped into `source/__assets/exiftool` (git-ignored; its [README](source/__assets/exiftool/README.md) says where each build goes).
- On Windows, Visual Studio's C++ build tools for the Native AOT publish.

From `source/`, `scripts/test.ps1` runs every test project and `scripts/publish-aot.ps1` publishes a Native AOT build to `__artifacts/publish/<rid>` (`.sh` on macOS and Linux). Each platform's packaging is in `__assets/win`, `__assets/mac` and `__assets/linux`.


## License
ExifGlass is free software, released under the terms of [GPLv3](https://github.com/d2phap/ExifGlass/blob/main/LICENSE). The store versions are paid builds of the same code that support its development. ExifTool and the other components ExifGlass ships with keep their own licences, listed in `THIRD-PARTY-NOTICES.txt` in every package and in About.
