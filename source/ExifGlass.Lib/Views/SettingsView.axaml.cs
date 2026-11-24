/*
ExifGlass - AI & EXIF metadata viewer
Copyright (C) 2023 - 2027 DUONG DIEU PHAP
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
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ExifGlass.Core.Localization;
using ExifGlass.UI.Controls;
using ExifGlass.ViewModels;
using System.ComponentModel;

namespace ExifGlass.Views;

public partial class SettingsView : UserControl
{
    #region Fields and constants

    private SettingsViewModel? _viewModel;
    private bool _syncing;

    #endregion // Fields and constants


    #region Constructors

    public SettingsView()
    {
        InitializeComponent();
        if (LanguageMenuButton.Flyout is PopupFlyoutBase flyout) MenuMotion.Attach(flyout);

        ArgumentField.NoteProvider = SettingsViewModel.NoteFor;
        ArgumentField.SuggestionHint = SettingsViewModel.HintFor;
        ArgumentField.Tokens.CollectionChanged += (_, _) =>
        {
            if (_syncing || _viewModel is null) return;

            _syncing = true;
            _viewModel.Arguments = [.. ArgumentField.Tokens];
            _syncing = false;
        };
    }

    #endregion // Constructors


    #region Protected methods

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _viewModel.ToastRequested -= ViewModel_ToastRequested;
        }

        _viewModel = DataContext as SettingsViewModel;
        if (_viewModel is null) return;

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _viewModel.ToastRequested += ViewModel_ToastRequested;
        ArgumentField.Suggestions = [.. _viewModel.Suggestions];
        ShowArguments();
    }

    #endregion // Protected methods


    #region Private methods

    private void ShowArguments()
    {
        if (_viewModel is null) return;

        _syncing = true;
        ArgumentField.SetTokens(_viewModel.Arguments);
        _syncing = false;
    }

    #endregion // Private methods


    #region Event handlers

    // Reset replaces the arguments from the view model's side
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.Arguments) && !_syncing) ShowArguments();
    }


    private void ViewModel_ToastRequested(object? sender, SettingsToast toast) => Toasts.Show(toast.Title, toast.Subtitle, toast.Kind);


    private static FilePickerFileType LanguagePacks => new(Lang.Current[LangId.Picker_LanguagePack]) { Patterns = ["*" + Lang.FILE_EXTENSION] };


    private async void InstallLanguageItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Lang.Current[LangId.Settings_LanguageInstall],
            AllowMultiple = true,
            FileTypeFilter = [LanguagePacks],
        });

        await _viewModel.InstallLanguagesAsync([.. files.Select(f => f.TryGetLocalPath()).OfType<string>()]);
    }


    private async void ExportLanguageItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Lang.Current[LangId.Settings_LanguageExport],
            SuggestedFileName = _viewModel.ExportFileName,
            FileTypeChoices = [LanguagePacks],
        });
        if (file is null) return;

        try
        {
            await using var stream = await file.OpenWriteAsync();
            _viewModel.ExportLanguage(stream, file.Name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Toasts.Show(Lang.Current[LangId.Toast_ExportFailed], ex.Message, ToastKind.Danger);
        }
    }


    private async void BrowseButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;

        FilePickerFileType[] types = OperatingSystem.IsWindows()
            ? [new FilePickerFileType(Lang.Current[LangId.Picker_Programs]) { Patterns = ["*.exe"] }, new FilePickerFileType(Lang.Current[LangId.Picker_AllFiles]) { Patterns = ["*"] }]
            : [new FilePickerFileType(Lang.Current[LangId.Picker_AllFiles]) { Patterns = ["*"] }];

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Lang.Current[LangId.Picker_ExifToolTitle],
            AllowMultiple = false,
            FileTypeFilter = types,
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path) _viewModel.ExifToolPath = path;
    }

    #endregion // Event handlers
}
