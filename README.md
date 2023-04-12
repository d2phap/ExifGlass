ExifGlass - EXIF metadata viewing tool
===

ExifGlass is an EXIF metadata viewing tool, designed to work seamlessly with [ImageGlass 9](https://imageglass.org), but can also be used as a standalone software on your computer. To use ExifGlass, you need to have [ExifTool by Phil Harvey](https://exiftool.org) installed on your system.

With ExifGlass, you can easily access a comprehensive overview of the technical details associated with your images, including camera settings, location data, and more. This tool is particularly useful for professional photographers or anyone interested in the technical aspects of digital photography. Whether you use ExifGlass as a standalone software or in conjunction with ImageGlass, it provides a convenient and efficient way to view and manage the metadata associated with your images.

You can download ExifGlass for free. To support the development of ExifGlass and gain access to future updates, consider purchasing it from the [Microsoft Store](https://www.microsoft.com/store/productId/9MX8S9HZ57W8).

[![ExifGlass on Microsoft Store](https://user-images.githubusercontent.com/3154213/231506294-1baee922-3283-48a4-ba70-25662a4a90db.svg)](https://www.microsoft.com/store/productId/9MX8S9HZ57W8)

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
| Fast startup time with native code | ❌ | ✅ |
| .NET 7 independency | ❌ | ✅ |
| Seamless auto-update | ❌ | ✅ |
| Launch ExifGlass with protocol `exifglass:` | ❌ | ✅ |
| Launch ExifGlass with command `exifglass` | ❌ | ✅ |

## Configure ExifGlass
If you want to configure ExifGlass to work with your system, follow these steps:
1. Download [ExifTool by Phil Harvey](https://exiftool.org). You can also use the [ExifTool Windows Installer](https://oliverbetz.de/pages/Artikel/ExifTool-for-Windows#toc-3) if you prefer, which will automatically register the executable file of ExifTool on your system. ExifGlass can detect it automatically.
2. If you downloaded the ZIP file, extract its contents into a folder.
3. Open ExifGlass Settings (press <kbd>Ctrl+,</kbd>) and go to "Executable path". Locate the `exiftool(-k).exe` file that you extracted in step 2.

## Integrate ExifGlass into [ImageGlass 9](https://imageglass.org)
Follow these steps to add ExifGlass as an external tool to **ImageGlass 9 beta 4**:
1. Open `igconfig.json` file with a text editor such as NotePad or VS Code.
2. Ensure that ImageGlass app is not running.
3. In the `Tools` section of the `igconfig.json` file, add the following code:
```js
// in igconfig.json
"Tools": [
  {
    "ToolId": "Tool_ExifGlass", // a unique ID
    "ToolName": "ExifGlass - Exif metadata viewer", // name of the tool
    "Executable": "path\\to\\ExifGlass.exe", // or "exifglass" for ExifGlass Store
    "Argument": "<file>",
    "CanToggle": true
  }
]
```
Note that if you have installed [ExifGlass Store](https://www.microsoft.com/store/productId/9MX8S9HZ57W8), you can use "exifglass" for the `Executable` field.

4. To assign hotkeys to the ExifGlass tool, add the following code:
```js
// in igconfig.json
"MenuHotkeys": {
  "Tool_ExifGlass": ["X", "Ctrl+E"] // press X or Ctrl+E to open/close ExifGlass tool
}
```
5. Save the file, and you're done! Now you can enjoy using ExifGlass as an external tool with ImageGlass 9 beta 4.

## License
ExifGlass is free for both personal and commercial use, except the Store version. It is released under the terms of [GPLv3](https://github.com/d2phap/ExifGlass/blob/main/LICENSE).

