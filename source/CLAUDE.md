# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

ExifGlass is an EXIF-metadata viewer that wraps the command-line **ExifTool** (exiftool.org). It runs standalone (CLI arg, drag-drop, file picker) and plugs into both **ImageGlass 10** (via `ImageGlass.SDK`) and **ImageGlass 9** (via the Windows-only `ImageGlass.Tools`) as an integrated tool that live-updates as the user navigates images.

This directory (`source/`) is a ground-up rewrite on **.NET 10 / Avalonia 12**, built **NativeAOT, Windows-first**. It follows the ImageGlass multi-project layout: a **cross-platform `net10.0` library (`ExifGlass.Lib`) holds all logic + UI**, and a thin **per-platform executable head** references it. The Windows head (`ExifGlass.Win32`, `net10.0-windows`, ships `ExifGlass.exe`), the Linux head (`ExifGlass.Linux`, `net10.0`, ships `ExifGlass`), and the macOS head (`ExifGlass.Mac`, `net10.0`, RID `osx-arm64`, ships `ExifGlass`) all sit beside each other, each referencing the same library. The solution is `ExifGlass.slnx`.

> **Source hygiene:** never reference any prior/"v1"/"old version" in source, comments, or commit messages — write everything as the target design. The repo root contains a `v1/` folder (the previous shipping app) and a v1-era `README.md`; treat both as out of scope for this rewrite and do not import their patterns.

## Commands

All commands run from this `source/` directory.

```bash
dotnet build ExifGlass.slnx -c Debug            # build everything
dotnet test  ExifGlass.slnx -c Debug            # run all xUnit tests (ExifGlass.Lib.Test)
dotnet test  ExifGlass.slnx --filter "FullyQualifiedName~UpdateServiceTests"   # one test class
dotnet test  ExifGlass.slnx --filter "FullyQualifiedName~CheckAsync_Skips_WhenWithinThrottleWindow"  # one test
```

Analyzer / style check (IntelliSense-level messages don't fail a normal build):

```bash
dotnet build ExifGlass.slnx -p:EnforceCodeStyleInBuild=true
dotnet format ExifGlass.slnx --verify-no-changes --severity info   # lists IDE/CA/MVVMTK suggestions
```

**AOT publish — the key correctness gate; re-run after any nontrivial change.** On Windows the ILC link step shells out to bare `vswhere.exe`, so prepend the VS Installer dir to `PATH` first (PowerShell), and kill any running instance so the exe copy isn't locked:

```powershell
Get-Process ExifGlass,exiftool -ErrorAction SilentlyContinue | Stop-Process -Force
$env:PATH = "C:\Program Files (x86)\Microsoft Visual Studio\Installer;" + $env:PATH
dotnet publish ExifGlass.Win32/ExifGlass.Win32.csproj -r win-x64 -c Release
# -> ExifGlass.Win32/bin/Release/net10.0-windows/win-x64/publish/ExifGlass.exe  (~33 MB)
```

A clean publish shows **only** the expected ProDataGrid baseline (`IL2104` + `IL3053` on `Avalonia.Controls.DataGrid.dll`). Any other IL warning — and **any `IL3050`** — is a regression to fix, not suppress.

The **Linux head** publishes the same way but NativeAOT can't cross-compile, so it must run on a Linux host (or CI container) — from Windows the ILC link step fails with *"Cross-OS native compilation is not supported."* (the managed compile + trim analysis still run first, so `dotnet build` on Windows validates the code):

```bash
dotnet publish ExifGlass.Linux/ExifGlass.Linux.csproj -r linux-x64 -c Release
# -> ExifGlass.Linux/bin/Release/net10.0/linux-x64/publish/ExifGlass
```

Run standalone: `ExifGlass.exe "<image>"`. Force a theme for one run without persisting: `ExifGlass.exe -p:Theme=Dark "<image>"` (see CLI overrides below). Force-kill leaves the `exiftool -stay_open` child orphaned — clean up with `Get-Process ExifGlass,exiftool | Stop-Process -Force`.

## Architecture (the big picture)

Five projects (mirroring the ImageGlass `Lib` + platform-head layout), split so all logic stays testable without a running UI and the Windows-only integration is quarantined to the executable head:

- **`ExifGlass.Lib`** (`net10.0`, `IsAotCompatible=true`, **cross-platform**) — everything shared: all pure logic (ExifTool invocation/parsing, exports, config load/merge, update check, path resolution, version compare) **and** the Avalonia UI (`App`, Views, ViewModels, Services, `Composition`, the `Integration` seam for standalone + ImageGlass 10 hosts, Styles, brand icon assets). The logic files keep their `ExifGlass.Core.*` namespaces (the former Core project was folded in here); the UI uses `ExifGlass.*`. Assembly name `ExifGlass.Lib` (so XAML resources are `avares://ExifGlass.Lib/…`); root namespace `ExifGlass`.
- **`ExifGlass.Win32`** — the **Windows executable head** (`net10.0-windows`, `AssemblyName=ExifGlass` → `ExifGlass.exe`, `PublishAot`, `TrimMode=full`). Holds only `Program.cs`, the ImageGlass 9 host, `app.manifest`, the app icon, and (from `__assets`, see below) the bundled ExifTool Windows build — the `exiftool.exe` launcher + its `exiftool_files\` runtime. It is the **only** project allowed to reference the Windows-only `ImageGlass.Tools` package.
- **`ExifGlass.Linux`** — the **Linux executable head** (`net10.0`, `AssemblyName=ExifGlass` → `ExifGlass`, `PublishAot`, `TrimMode=full`, RID `linux-x64`). Holds `Program.cs` and (from `__assets`) its ExifTool bundle — a self-contained binary compiled by the `build-exiftool-*` VS Code task via PAR::Packer where possible (no system Perl needed), else the Perl `exiftool` script + `lib/` as fallback (runs on the target's Perl via the `#!/usr/bin/env perl` shebang; the resolver's PATH fallback covers a distro-installed ExifTool otherwise). It supports standalone + ImageGlass 10 (SDK) only: it sets no `SourceHostFactory`, so the library's default host selection applies, and it does **not** reference `ImageGlass.Tools` (ImageGlass 9 is Windows-only). `Program.Main` best-effort-sets the bundled script's Unix executable bit at startup, since a publish produced on a non-Unix host can't carry it.
- **`ExifGlass.Mac`** — the **macOS executable head** (`net10.0`, `AssemblyName=ExifGlass` → `ExifGlass`, `PublishAot`, `TrimMode=full`, RID `osx-arm64`). Holds `Program.cs` and (from `__assets`) its ExifTool bundle — the Perl `exiftool` script + `lib/`, run on the **system Perl**. It deliberately does **not** use a PAR-compiled binary like Linux: `pp` staples an archive past the Mach-O's `__LINKEDIT`, which `codesign` rejects, so it can't be notarized (this matches ExifTool's own official macOS distribution). Like Linux it supports standalone + ImageGlass 10 only (no `SourceHostFactory`, no `ImageGlass.Tools`) and omits the Linux X11 dark-mode probe (macOS resolves the theme synchronously). `Program.Main` best-effort-sets the script's Unix executable bit. `pack-app.sh` relocates the script + `lib/` into `Contents/Resources/` (codesign rejects non-Mach-O under `Contents/MacOS/`) and symlinks `exiftool` back so the resolver finds it.
- **`ExifGlass.Lib.Test`** — xUnit over the library (references `ExifGlass.Lib`).

**Bundled ExifTool lives in one shared place.** The **`source/__assets/exiftool/`** folder holds the official [exiftool.org](https://exiftool.org) release artifacts, dropped in unmodified (don't hand-curate/patch — a version bump is just a re-drop): `win/` is the Windows compiled build (`exiftool.exe`, renamed from the release's `exiftool(-k).exe`, plus its `exiftool_files/` Strawberry-Perl runtime + `lib/`); `unix/` is the Perl distribution (`exiftool` script + `lib/`) shared by Linux and macOS (Linux compiles it into a self-contained binary; macOS ships the script as-is). **`win/` and `unix/` are git-ignored** (large + re-obtainable); only the shared `LICENSE` is tracked, so a fresh clone must re-populate them (Windows: the `_64.zip`, rename the exe → `exiftool.exe`; Unix: the `Image-ExifTool` tarball's `exiftool` + `lib/`). The csprojs guard every ExifTool item on `Exists(...)`, so a clone without the assets still builds — ExifTool then resolves from PATH at runtime.

**Build/publish scripts live in `source/__assets/<platform>/`** (`win/` = PowerShell `.ps1`, `linux/` + `mac/` = bash `.sh`), each taking the target arch (`x64`; `arm64` for the Windows and macOS scripts) and self-locating the `source/` dir; the VS Code tasks in the repo-root `.vscode/tasks.json` are thin wrappers that invoke them. `mac/` adds `build.sh`, `publish.sh`, `pack-app.sh` (assembles `ExifGlass.app`), and `pack-dmg.sh` (Developer ID sign + notarize into a DMG for GitHub distribution), plus `Info.plist`, the `ExifGlass.entitlements` file, and `logo.icns`. There is **no** `build-exiftool` step on macOS — it bundles the Perl script directly.

To ship a **self-contained `exiftool`** (Perl embedded via PAR::Packer, so end users need no system Perl), compile it from `unix/` with the VS Code **`build-exiftool-<rid>`** task (or `__assets/linux/build-exiftool.sh <arch> [--force]` directly) — **Linux only** (macOS deliberately skips this: a PAR binary can't be codesigned/notarized, so the macOS head bundles the Perl script + `lib/` and runs on the system Perl). It caches per-RID at `unix/build/<rid>/exiftool`; `ExifGlass.Linux.csproj` then bundles that binary if it exists (decided at evaluation time by `Exists`), else falls back to the Perl script + `lib/` (needs system Perl at runtime). There is deliberately **no MSBuild compile step** — the `build-linux-*` / `publish-linux-*` VS Code tasks `dependsOn` the matching `build-exiftool-*` task (which is idempotent: skips if already built), so building the Linux head compiles ExifTool first. See `.vscode/tasks.json` (repo root). (`.gitattributes` forces `*.sh` to LF so the shell scripts run on Linux despite `core.autocrlf`.)

**Central build management** (same as ImageGlass): `Directory.Packages.props` pins every NuGet version (`ManagePackageVersionsCentrally`; csprojs carry versionless `<PackageReference>`s). `Directory.Build.props` single-sources the app version (`<ExifGlassVersion>`) + product identity; `Directory.Build.targets` propagates it to `Version`/`FileVersion`/`InformationalVersion` for every project. `Helpers/AppInfo` reads the *library's* embedded informational version, so it must stay a clean numeric version (no build-metadata suffix) or the update-check comparison breaks.

**Manual composition root, no DI container.** `Composition/AppServices.cs` wires the object graph with plain `new` (fastest startup, zero reflection/trim warnings). `App.OnFrameworkInitializationCompleted` is the bootstrap: parse args → load settings + apply CLI overrides → apply theme → create `MainWindow` + VM → restore window bounds → pick the source host → wire it → `host.Start()`.

**One file-loading seam.** Every entry mode funnels into `MainWindowViewModel.LoadFileAsync(path)` via `Integration/IImageSourceHost`:
- `StandaloneSourceHost` — emits the initial CLI file. (Drag-drop and the picker call `LoadFileAsync` directly.)
- `ImageGlassSourceHost : ToolBase` — the ImageGlass 10 SDK pipe client; `--pipe` on the command line selects it. `OnPhotoChanged` is the live-nav trigger.
- `ImageGlass9SourceHost` (in the Windows head) — wraps `ImageGlass.Tools.ImageGlassTool`; `--ig-tool-pipe-code=<code>` selects it. `IMAGE_LOADING` is the live-nav trigger.

**Host selection is a factory hook, not a hard reference.** The cross-platform `App` (in `ExifGlass.Lib`) cannot name the Windows-only `ImageGlass9SourceHost`, so it exposes `static App.SourceHostFactory` (`Func<StartupOptions, string[], IImageSourceHost?>`). The Windows `Program.Main` sets it to return an `ImageGlass9SourceHost` for `AppMode.ImageGlass9`; returning `null` falls back to the library's built-in standalone / ImageGlass-10 selection. `Helpers/CommandLine.Parse` maps `--pipe` → `ImageGlass`, `--ig-tool-pipe-code=` → `ImageGlass9`.

`LoadFileAsync` owns a **CTS swap**: each load cancels the previous token, so during rapid navigation the newest file always wins and superseded reads are silently discarded (`OperationCanceledException` swallowed).

**ExifTool access.** `Services/ExifToolService` is the seam (`IExifToolService`); the full read pipeline is documented in **How EXIF metadata loading works** below.

**Settings.** `ISettingsService` holds a mutable `AppConfig`; layered load is defaults → JSON file → CLI `-p:Key=Value` overrides. Config path is per-OS (`%LOCALAPPDATA%\ExifGlass\exifglass.config.json` on Windows) via `Helpers/AppPaths`. Persisted **only** on window close (`SaveOnClose`) and after an update check (stamps `LastUpdateCheck`).

## How EXIF metadata loading works

Every navigation — CLI file, drag-drop, picker, or an ImageGlass pipe event — funnels into `MainWindowViewModel.LoadFileAsync(path)`, which owns the **CTS swap** (each load cancels the previous token so the newest file always wins; superseded reads throw `OperationCanceledException`, which is swallowed). It shows the command preview immediately, sets `IsLoading`, then awaits `IExifToolService.ReadAsync`. On success the grid is replaced wholesale; on failure the **last good grid is kept** and the error surfaces in a dismissible banner.

The read itself, in `Services/ExifToolService.ReadAsync`:

1. **Resolve the executable** via `ExifToolPathResolver`: Settings-override → bundled ExifTool (`exiftool.exe` on Windows, a compiled `exiftool` on Linux, the `exiftool` Perl script on macOS) → PATH (cross-platform).
2. **ANSI-path workaround (Windows-only).** ExifTool can't open paths with codepoints above the ANSI range, so `PlatformInfo.NeedsAnsiPathWorkaround` files are copied to an ASCII temp path (`AppPaths.TempDir`) and read from there; the temp copy is tracked and swept by `CleanupTempFiles`. The real `File Name` value is remapped back onto the result so the grid shows the original name.
3. **Build the argv** through `Helpers/ExifToolCommand.BuildArgs` — the *single* source of truth shared with the footer preview (`BuildPreview`) so a run and its displayed command can't drift. Base flags are `-fast -G -t -m -q -H` (tab-delimited, family-0 group, hex tag id, fast/quiet), then any user `ExifToolArguments` (tokenized honoring double-quotes), then `-charset UTF8`, then the file path.
4. **Execute on the persistent daemon.** `ExifToolDaemon` is one long-lived `exiftool -stay_open True -@ -` process — feeding argument sets over stdin and reading to a sentinel removes the per-read interpreter-startup cost, which is what keeps live navigation fast. Access is serialized (one command at a time via a `SemaphoreSlim`); each command is numbered (`-execute<seq>`) and its stdout is read until `{ready<seq>}`, while stderr is framed the same way with `-echo4` + `{igerr<seq>}`. A 30 s per-command timeout, a broken pipe, or a stall kills and transparently restarts the process, retrying once. Raw `Process` piping keeps it AOT-safe.
5. **Parse** stdout with `Helpers/ExifToolOutputParser.Parse`: each row is `Group\tTagId\tTagName\tValue` split at most 4 ways (embedded tabs fold into the value); a line that doesn't split into 4 fields is treated as a **continuation** of the previous value (values can contain embedded newlines). Rows are 1-indexed into `ExifTagItem`.
6. **Shape the result** as `ExifReadResult(tags, preview, success, error)`. The daemon has no per-command exit code, so **empty output ⇒ failure** — the message is the trimmed stderr, or "No metadata was found for this file." when stderr is also empty.

Related paths that do **not** use the daemon:
- **Binary extraction** (`ExtractBinaryTagAsync`) is one-shot via `IProcessRunner`/CliWrap, using `-b -w!` to write the tag to a temp dir then moving it to the chosen destination. A row offers this only when `ExifTagItem.CanExtractBinary` is true (ExifTool's value contains `", use -b option to extract"`).
- **Validation** (`ValidateAsync`) runs `-ver` one-shot for the startup self-check; `ExifToolOutputParser.LooksValid` then confirms a parsed row carries a hex tag id, catching a silent ExifTool output-format change.

The parsed `ExifTagItem` rows feed the reflection-free grid (grouping/sorting described under the AOT invariants below).

## AOT is the through-line — preserve these invariants

NativeAOT forbids runtime codegen and trims unused members, so every choice avoids reflection where a compiled/static path exists. When changing code, keep to these or the native binary breaks silently:

- **Compiled XAML + compiled bindings only** (`EnableAvaloniaXamlCompilation`, `AvaloniaUseCompiledBindingsByDefault`). Windows set `x:DataType`. No `ReflectionBinding`, no runtime `AvaloniaXamlLoader.Load(string)`.
- **`System.Text.Json` source-gen only** — serialize/deserialize through `AppJsonContext` (e.g. `AppJsonContext.Default.UpdateInfo`) and the SDK's `ToolJsonContext`. Never the reflection `JsonSerializer` overloads. Every new persisted/feed type needs a `[JsonSerializable]` entry.
- **Grid is reflection-free**: grouping via `Integration/TagGroupDescription` (a `DataGridGroupDescription`, not the path-based one); header-click sorting handled in `MainWindow.OnSorting` with `DataGridSortDescription.FromComparer` + explicit `Comparer<ExifTagItem>` (never `FromPath`). The row model `ExifTagItem` is `[DynamicallyAccessedMembers(...)]`-annotated so the grid's `Type.GetProperties()` survives trimming.
- **No reflection DI** (no `Microsoft.Extensions.DependencyInjection`/`Ioc`), **no `Microsoft.Extensions.Configuration`** (hand-written layered load), **no reflection for cell copy or config-key mapping** (explicit `switch` / `nameof` comparisons).
- **Expected IL warnings** are pinned to the exact site with `[UnconditionalSuppressMessage("Trimming","IL2026", Justification=...)]` (the DataGrid/`DataGridCollectionView` usage in `MainWindow` and `MainWindowViewModel.BuildGroupedView`) — never a project-wide `NoWarn`. **Never suppress `IL3050`**; it means an expression-based API slipped in.

## ImageGlass 10 SDK integration specifics

`ImageGlassSourceHost` subclasses `ImageGlass.SDK.Tools.ToolBase`:
- The SDK's `OnXxx` hooks are `protected internal virtual`; overriding them from this assembly must use **`protected override`** (not `protected internal` — the `internal` part is inaccessible across assemblies). `ToolId` is `public abstract` → `public override` and must equal the `igconfig.json` entry (`"Tool_ExifGlass"`).
- SDK callbacks fire on the pipe read-loop thread, never the UI thread — **every** `FileRequested`/`CloseRequested` raise marshals via `Dispatcher.UIThread.Post`. `OnPhotoChanged` is a synchronous `void`: never await inline, never let it throw. `RunAsync` blocks in the pipe loop, so `Start()` runs it on a background `Task`.
- The pipe says *which file & when*; ExifTool remains the source of truth (always run the local read on the reported path).
- The pipe can be verified without ImageGlass by driving a mock `NamedPipeServerStream` host.

## Conventions

- **Every `.cs` file starts with the GPLv3 header block** (see any existing file); new files must include it.
- **XML doc comments are always multi-line** — never `/// <summary>text</summary>` on one line; break `<summary>` across three lines.
- MVVM uses CommunityToolkit source generators: `[ObservableProperty]` on **partial properties** (`public partial T X { get; set; }`), `[RelayCommand]`. Keep it AOT-safe — generators only, never `Ioc`.
- App version comes from `AssemblyInformationalVersion` via `Helpers/AppInfo` (embedded metadata; cross-platform + single-file/AOT-safe). Never use `Assembly.Location`/`FileVersionInfo` — empty/absent under single-file/AOT and on Linux/macOS. Set the product version through `<ExifGlassVersion>` in `Directory.Build.props` (propagated to all projects by `Directory.Build.targets`) — never per-project.
- **Central package management:** add/upgrade NuGet versions in `Directory.Packages.props`; `<PackageReference>` in a csproj must stay versionless.

## Verifying UI/behavior changes

`dotnet test` covers the logic, but the grid, dialogs, live navigation, and update flow only manifest in the running app. Launch it (Debug exe at `ExifGlass.Win32/bin/Debug/net10.0-windows/ExifGlass.exe`) and screenshot. Notes:
- CLI overrides (`-p:Theme=Dark`, `-p:CheckForUpdates=false`, `-p:WindowWidth=900`, …) are in-memory only; config persists solely on window close, so **force-kill** after screenshotting to avoid mutating the user's real config.
- The NativeAOT window renders on a GPU surface — GDI `CopyFromScreen` returns black; use `PrintWindow(hwnd, hdc, 2)`.
- The persistent daemon means a plain force-kill can orphan `exiftool`; sweep both processes afterward.
