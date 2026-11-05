ExifGlass - AI & EXIF metadata viewer
===

ExifGlass is a cross-platform EXIF metadata viewing tool, designed to work seamlessly with [ImageGlass - A Fast, Seamless Photo Viewer](https://imageglass.org), but can also be used as a standalone software on your computer. 

With ExifGlass, you can easily access a comprehensive overview of the technical details associated with your images, including camera settings, location data, and more. This tool is particularly useful for professional photographers or anyone interested in the technical aspects of digital photography. Whether you use ExifGlass as a standalone software or in conjunction with ImageGlass, it provides a convenient and efficient way to view and manage the metadata associated with your images.

For AI images, ExifGlass shows the prompt and settings that tools like Stable Diffusion, ComfyUI and Midjourney save inside the file, so you can see how an image was made and reuse its settings. It tells you when a file is marked as AI-generated, too.

ExifGlass is open source. Buying it here supports its development, and the [Microsoft Store](https://apps.microsoft.com/detail/9MX8S9HZ57W8?launch=true&cid=eg_github_readme&mode=full) keeps it up to date.


[![ExifGlass on Microsoft Store](https://github.com/d2phap/ExifGlass/assets/3154213/ed878a58-cec3-4f74-ac56-c215477f6c61)](https://apps.microsoft.com/detail/9MX8S9HZ57W8?launch=true&cid=eg_github_readme&mode=full)

<a href="https://github.com/d2phap/ExifGlass/releases">
  <img src="https://img.shields.io/github/downloads/d2phap/exifglass/total?color=%23ed604c&label=total%20downloads&style=for-the-badge" /></a>

<a href="https://github.com/d2phap/ExifGlass/releases">
  <img src="https://img.shields.io/github/downloads/d2phap/exifglass/latest/total?color=%23ed604c&label=latest%20version&style=for-the-badge" />
</a>


<br/>
<img src=".github/eg_ai_light.webp" alt="ExifGlass showing the prompt, model and sampler settings of an AI image" />


## Features

### View, edit and export photo metadata
- One table for every tag ExifTool reads, grouped, with tag IDs: search, sort, hide groups, and quick filters for dates and times, GPS, converted values, prompts, models and provenance.
- An inspector for the selected tag, with the preview, the exposure, and the camera, lens, time and place.
- Copy a value, a row or a selection; export the rows in view as CSV, plain text or ExifTool-style JSON.
- Edit, add and delete tags; remove the GPS data or the AI generation data before you share a file, or strip all metadata. Changes wait until you save, and ExifTool keeps the original file as a backup.
- The command bar shows the exact ExifTool command for the read, or for the save while changes wait, ready to copy.

### View AI generation data
- The prompt, negative prompt, models, LoRAs and embeddings, sampler, scheduler, steps, CFG, seed and size, read from AUTOMATIC1111 and Forge, ComfyUI, InvokeAI, NovelAI, Fooocus, SwarmUI, Draw Things, Easy Diffusion, Tensor.Art and Midjourney files.
- ComfyUI workflows summarised (nodes, samplers, models) and saved as a `.json` file that loads back into ComfyUI.
- Provenance: C2PA Content Credentials (shown as not verified, because ExifGlass checks no signature), the IPTC digital source type, AI labels and AI-training signals.
- An "AI generation" group at the top of the table, each field linked to the tag it was read from.
- Copy the generation parameters as AUTOMATIC1111 text, or export them as text or JSON.


## Register ExifGlass as external tool in ImageGlass

### ImageGlass 10
ImageGlass 10 comes with an ExifGlass tool already set up (ID `Tool_ExifGlass`, hotkey <kbd>X</kbd>). Press <kbd>X</kbd> to open ExifGlass beside the photo; it closes when ImageGlass closes. If the tool was removed, or ExifGlass is installed somewhere else, open Settings > Tools in ImageGlass, click "Add" and fill in:

- **ID**: `Tool_ExifGlass`
- **Integrated with [ImageGlass.SDK](https://github.com/ImageGlass/SDK)**: checked
- **Hotkey**: <kbd>X</kbd>
- **Executable** and **Argument**, depending on how you installed ExifGlass:
  - Windows (Store or MSIX): `exifglass`, with the argument `<file>`. These versions add `exifglass` to PATH.
  - Windows (ZIP): the full path of `ExifGlass.exe`, with the argument `<file>`.
  - macOS: `/Applications/ExifGlass.app`, with the argument `<file>`.
  - Linux (AppImage): the full path of the `.AppImage` file, with the argument `<file>`.
  - Linux (Flatpak): `flatpak`, with the argument `run io.github.d2phap.exifglass <file>`.


<img width="800" alt="image" src="https://github.com/user-attachments/assets/28c26471-1858-4928-885c-1b1b1cd34793" />

### ImageGlass 9 (Windows only)
Add ExifGlass as an integrated tool, as [ImageGlass Docs / ImageGlass tools](https://imageglass.org/docs/imageglass-tools#add-your-tool-to-imageglass) describes: the executable is `ExifGlass.exe` (or `exifglass`) and the argument `<file>`. ExifGlass closes when you turn the tool off or close ImageGlass.


## License
ExifGlass is released under the terms of [GPLv3](https://github.com/d2phap/ExifGlass/blob/main/LICENSE). The store versions are paid builds of the same code that support its development.
