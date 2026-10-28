# Privacy information
This page may be updated without notice. It covers ExifGlass 3 in every package: the Microsoft Store and Mac App Store versions, Flathub, and the downloads from GitHub Releases (MSIX, ZIP, DMG and AppImage).

### Your files stay on your computer
ExifGlass reads and writes metadata with ExifTool, which runs on your computer. The files you open, their metadata, the AI prompts, workflows and other generation data inside them, and every change you make never leave your computer. ExifGlass looks nothing up online: model names and hashes, for example, are shown as they are and can be copied, but are never sent anywhere. ExifGlass collects no personal information, keeps no usage statistics and has no account.

### The update check
The GitHub downloads (MSIX, ZIP, DMG and AppImage) check for a new version at most once every five days, and when you click "Check now" in About. The check downloads a small file, `update.json`, from GitHub. The request carries only the app's name and version in its User-Agent header; GitHub sees your IP address as it does for any download. You can turn the check off in Settings.

The Microsoft Store, Mac App Store and Flathub versions never check: the store or Flathub delivers their updates.

### Links you open
Some buttons open a page in your browser: the project's GitHub page, the Microsoft Store page, a third-party licence, and "Open in OpenStreetMap", which sends the photo's GPS coordinates to openstreetmap.org to show the place on a map. Nothing is sent until you click one of them, and each site's own privacy policy applies from there.

### ImageGlass
When ImageGlass starts ExifGlass as a tool, the two apps talk through a local pipe on your computer: ImageGlass tells ExifGlass which photo it shows. Nothing goes over the network.

### Settings and recovery
ExifGlass keeps its settings, and the unsaved changes it keeps for you when ImageGlass closes it, in a file in your user profile (or next to `ExifGlass.exe` in the portable ZIP). The Mac App Store version keeps the copies and backups it needs inside its own container.
