/*
ExifGlass - AI & EXIF metadata viewer
Copyright (C) 2023 - 2026 DUONG DIEU PHAP
Project homepage: https://github.com/d2phap/ExifGlass

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using ExifGlass.Core.Export;
using ExifGlass.Core.Settings;
using ExifGlass.Services;
using ExifGlass.UI.Controls;
using ExifGlass.UI.Windows;
using ExifGlass.ViewModels;

namespace ExifGlass.Windows;

/// <summary>
/// The main window (P4): native title bar, the DS 2 layout, placement restored (O15), no single instance (D16).
/// </summary>
public partial class MainWindow : Window, IShellView
{
    #region Fields and constants

    /// <summary>
    /// The first-run size (D15), clamped to the work area.
    /// </summary>
    public static readonly Size FirstSize = new(1240, 820);

    private static readonly FilePickerFileType ImageFiles = new(AppText.PICKER_IMAGES)
    {
        Patterns =
        [
            "*.jpg", "*.jpeg", "*.jpe", "*.jfif", "*.png", "*.apng", "*.webp", "*.gif", "*.bmp", "*.tif", "*.tiff", "*.heic", "*.heif", "*.hif",
            "*.avif", "*.jxl", "*.psd", "*.exr", "*.dng", "*.cr2", "*.cr3", "*.crw", "*.nef", "*.nrw", "*.arw", "*.srf", "*.sr2", "*.raf",
            "*.orf", "*.rw2", "*.pef", "*.srw", "*.x3f", "*.3fr", "*.iiq", "*.erf", "*.mos", "*.mrw", "*.rwl",
        ],
        AppleUniformTypeIdentifiers = ["public.image"],
    };

    private static readonly FilePickerFileType AllFiles = new(AppText.PICKER_ALL) { Patterns = ["*"] };

    private readonly IWindowPlacementStore _placements;
    private SettingsWindow? _settingsWindow;
    private AboutWindow? _aboutWindow;
    private bool _closeConfirmed;

    #endregion // Fields and constants


    #region Constructors

    /// <summary>
    /// For the XAML previewer: no ExifTool and placements kept in memory.
    /// </summary>
    public MainWindow() : this(new MainViewModel(new SettingsStore(Path.GetTempPath()), new NoReader()), new MemoryPlacementStore())
    {
    }


    public MainWindow(MainViewModel viewModel, IWindowPlacementStore placements)
    {
        _placements = placements;
        InitializeComponent();
        AppWindow.Prepare(this);

        DataContext = viewModel;
        viewModel.Shell = this;

        WindowPlacement.Attach(this, AppSettings.WINDOW_MAIN, FirstSize, placements);
        InitializeLayout();
        InitializeInput();
    }

    #endregion // Constructors


    #region Properties

    public MainViewModel ViewModel => (MainViewModel)DataContext!;

    #endregion // Properties


    #region IShellView

    public async Task<string?> PickFileAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = AppText.PICKER_TITLE,
            AllowMultiple = false,
            FileTypeFilter = [AllFiles, ImageFiles],
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }


    public void ShowToast(string title, string? subtitle = null, ToastKind kind = ToastKind.Success) => Toasts.Show(title, subtitle, kind);


    public void FocusSearch() => Toolbar.FocusSearch();


    public void ShowSettings() => ShowChild(ref _settingsWindow, () => new SettingsWindow(_placements, this));


    public void ShowAbout() => ShowChild(ref _aboutWindow, () => new AboutWindow(_placements, this));


    public async Task CopyTextAsync(string text)
    {
        if (Clipboard is { } clipboard) await clipboard.SetTextAsync(text);
    }


    public Task<SaveTarget?> PickSaveFileAsync(string suggestedName, ExportFormat format)
    {
        var extension = Exporter.Extension(format);
        return PickSaveFileAsync(suggestedName, extension, extension.ToUpperInvariant());
    }


    public async Task<SaveTarget?> PickSaveFileAsync(string suggestedName, string extension, string typeName)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            ShowOverwritePrompt = true,
            FileTypeChoices = [new FilePickerFileType(typeName) { Patterns = ["*." + extension] }],
        });

        return file is null ? null : new SaveTarget(await file.OpenWriteAsync(), file.Name);
    }


    public Task<DialogAnswer> AskAsync(DialogRequest request) => Dialogs.ShowAsync(request);


    public async Task OpenUriAsync(string uri)
    {
        if (Uri.TryCreate(uri, UriKind.Absolute, out var target) && target.Scheme == Uri.UriSchemeHttps) await Launcher.LaunchUriAsync(target);
    }

    #endregion // IShellView


    #region Protected methods

    // D8: with edits staged the window asks before it closes; a shutdown of the OS isn't held up by a dialog
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel || _closeConfirmed || !ViewModel.HasPending || e.CloseReason == WindowCloseReason.OSShutdown) return;

        e.Cancel = true;
        _ = ConfirmCloseAsync();
    }

    #endregion // Protected methods


    #region Private methods

    private async Task ConfirmCloseAsync()
    {
        if (!await ViewModel.ConfirmLeaveAsync()) return;

        _closeConfirmed = true;
        Close();
    }


    // one of each child window: a second request brings the open one forward
    private void ShowChild<T>(ref T? window, Func<T> create) where T : OwnedWindow
    {
        if (window is { IsVisible: true })
        {
            window.Activate();
            return;
        }

        var created = window = create();
        created.Closed += (_, _) =>
        {
            if (ReferenceEquals(_settingsWindow, created)) _settingsWindow = null;
            if (ReferenceEquals(_aboutWindow, created)) _aboutWindow = null;
        };
        created.Show(this);
    }

    #endregion // Private methods


    #region Nested types

    private sealed class NoReader : IMetadataReader
    {
        public Task<Core.Metadata.MetadataDocument> ReadAsync(string path, CancellationToken cancellationToken) =>
            Task.FromException<Core.Metadata.MetadataDocument>(new ExifToolUnavailableException(AppText.EXIFTOOL_MISSING_TITLE));

        public Task<Core.Ai.AiGenerationInfo?> ReadAiAsync(string path, CancellationToken cancellationToken) => Task.FromResult<Core.Ai.AiGenerationInfo?>(null);
    }

    #endregion // Nested types
}
