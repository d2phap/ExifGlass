ExifGlass - EXIF metadata viewing tool
===

ExifGlass is an EXIF metadata viewing tool, designed to work seamlessly with [ImageGlass 9](https://imageglass.org), but can also be used as a standalone software on your computer. To use ExifGlass, you need to have [ExifTool by Phil Harvey](https://exiftool.org) installed on your system.

With ExifGlass, you can easily access a comprehensive overview of the technical details associated with your images, including camera settings, location data, and more. This tool is particularly useful for professional photographers or anyone interested in the technical aspects of digital photography. Whether you use ExifGlass as a standalone software or in conjunction with ImageGlass, it provides a convenient and efficient way to view and manage the metadata associated with your images.

You can download ExifGlass for free. To support the development of ExifGlass and gain access to future updates, consider purchasing it from the [Microsoft Store](https://www.microsoft.com/store/productId/9MX8S9HZ57W8).

[![ExifGlass on Microsoft Store](https://user-images.githubusercontent.com/3154213/231506294-1baee922-3283-48a4-ba70-25662a4a90db.svg)](https://www.microsoft.com/store/productId/9MX8S9HZ57W8)


<a href="https://github.com/d2phap/ExifGlass/releases">
  <img src="https://img.shields.io/github/downloads/d2phap/exifglass/total?color=%23ed604c&label=total%20downloads&style=for-the-badge" /></a>
  
<a href="https://github.com/d2phap/ExifGlass/releases">
  <img src="https://img.shields.io/github/downloads/d2phap/exifglass/latest/total?color=%23ed604c&label=latest%20version&style=for-the-badge" /></a>



<img src="https://raw.githubusercontent.com/d2phap/ExifGlass/main/Screenshots/main.png" width="600" />

## ExifGlass features
| Feature | Free version | [ExifGlass Store](https://www.microsoft.com/store/productId/9MX8S9HZ57W8) | 
| -- | -- | -- |
| Reads EXIF metadata | ✅ | ✅ |
| Seamlessly works with [ImageGlass 9](https://imageglass.org) | ✅ | ✅ |
| Drag-n-drop file to view metadata | ✅ | ✅ |
| Copy metadata | ✅ | ✅ |
| Export as Text, JSON, CSV | ✅ | ✅ |
| Custom ExifTool's command-line arguments | ✅ | ✅ |
| .NET 8 self-contained | ❌ | ✅ |
| Seamless auto-update | ❌ | ✅ |
| Launch ExifGlass with protocol `exifglass:` | ❌ | ✅ |
| Launch ExifGlass with command `exifglass` | ❌ | ✅ |

## Configure ExifGlass
If you want to configure ExifGlass to work with your system, follow these steps:
1. Download [ExifTool by Phil Harvey](https://exiftool.org). You can also use the [ExifTool Windows Installer](https://oliverbetz.de/pages/Artikel/ExifTool-for-Windows#toc-3) if you prefer, which will automatically register the executable file of ExifTool on your system. ExifGlass can detect it automatically.
2. If you downloaded the ZIP file, extract its contents into a folder, rename `exiftool(-k).exe` to `exiftool.exe`.
3. Open ExifGlass Settings (press <kbd>Ctrl+,</kbd>) and go to "Executable path". Locate the `exiftool.exe` file that you extracted in step 2.

## Integrate ExifGlass into [ImageGlass 9](https://imageglass.org)
Please refer to [ImageGlass Docs / ImageGlass tools](https://imageglass.org/docs/imageglass-tools#add-your-tool-to-imageglass).

> [!TIP]
> Note that if you have installed [ExifGlass Store](https://www.microsoft.com/store/productId/9MX8S9HZ57W8), you can just use `exifglass` for the `Executable` field.


## Build ExifGlass from source code
- .NET 8.0 and Visual Studio 2022
- Add [ImageGlass.Tools](https://www.nuget.org/packages/ImageGlass.Tools) package.

## License
ExifGlass is free for both personal and commercial use, except the Store version. It is released under the terms of [GPLv3](https://github.com/d2phap/ExifGlass/blob/main/LICENSE).

