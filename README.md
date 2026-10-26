ExifGlass - EXIF Metadata Viewing Tool
===

ExifGlass is a cross-platform EXIF metadata viewing tool, designed to work seamlessly with [ImageGlass - A Fast, Seamless Photo Viewer](https://imageglass.org), but can also be used as a standalone software on your computer. To use ExifGlass, you need to have [ExifTool by Phil Harvey](https://exiftool.org) installed on your system.

With ExifGlass, you can easily access a comprehensive overview of the technical details associated with your images, including camera settings, location data, and more. This tool is particularly useful for professional photographers or anyone interested in the technical aspects of digital photography. Whether you use ExifGlass as a standalone software or in conjunction with ImageGlass, it provides a convenient and efficient way to view and manage the metadata associated with your images.

You can download ExifGlass for free. To support the development of ExifGlass and gain access to future updates, consider purchasing it from the [Microsoft Store](https://apps.microsoft.com/detail/9MX8S9HZ57W8?launch=true&cid=github_readme&mode=full).

[![ExifGlass on Microsoft Store](https://github.com/d2phap/ExifGlass/assets/3154213/ed878a58-cec3-4f74-ac56-c215477f6c61)](https://apps.microsoft.com/detail/9MX8S9HZ57W8?launch=true&cid=github_readme&mode=full)

<a href="https://github.com/d2phap/ExifGlass/releases">
  <img src="https://img.shields.io/github/downloads/d2phap/exifglass/total?color=%23ed604c&label=total%20downloads&style=for-the-badge" /></a>
  
<a href="https://github.com/d2phap/ExifGlass/releases">
  <img src="https://img.shields.io/github/downloads/d2phap/exifglass/latest/total?color=%23ed604c&label=latest%20version&style=for-the-badge" />
</a>


<br/>
<img src="https://raw.githubusercontent.com/d2phap/ExifGlass/refs/heads/main/source/__assets/linux/screenshots/eg_flatpak_2.png" />


## ExifGlass features
| Feature | Free version | [ExifGlass Store](https://apps.microsoft.com/detail/9MX8S9HZ57W8?launch=true&cid=github_readme&mode=full) | 
| -- | -- | -- |
| Reads EXIF metadata | ✅ | ✅ |
| Seamlessly works with [ImageGlass](https://imageglass.org) | ✅ | ✅ |
| Drag-n-drop file to view metadata | ✅ | ✅ |
| Copy metadata | ✅ | ✅ |
| Export as Text, JSON, CSV | ✅ | ✅ |
| Custom ExifTool's command-line arguments | ✅ | ✅ |
| .NET self-contained | ✅ | ✅ |


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
- .NET 10.0 and Visual Studio 2022
- Add [ImageGlass.SDK](https://www.nuget.org/packages/ImageGlass.SDK) package (for ImageGlass 10).
- Add [ImageGlass.Tools](https://www.nuget.org/packages/ImageGlass.Tools) package (for ImageGlass 9).


## License
ExifGlass is free for both personal and commercial use, except the Store version. It is released under the terms of [GPLv3](https://github.com/d2phap/ExifGlass/blob/main/LICENSE).

