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
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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


    private void ViewModel_ToastRequested(object? sender, (string Title, string? Subtitle) toast) => Toasts.Show(toast.Title, toast.Subtitle);


    private async void BrowseButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;

        FilePickerFileType[] types = OperatingSystem.IsWindows()
            ? [new FilePickerFileType(AppText.PICKER_PROGRAMS) { Patterns = ["*.exe"] }, new FilePickerFileType(AppText.PICKER_ALL) { Patterns = ["*"] }]
            : [new FilePickerFileType(AppText.PICKER_ALL) { Patterns = ["*"] }];

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = AppText.PICKER_EXIFTOOL,
            AllowMultiple = false,
            FileTypeFilter = types,
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path) _viewModel.ExifToolPath = path;
    }

    #endregion // Event handlers
}
