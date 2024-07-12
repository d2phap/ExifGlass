/*
ExifGlass - Standalone Exif tool for ImageGlass
Copyright (C) 2023-2024 DUONG DIEU PHAP
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
using Avalonia.Input;
using Avalonia.Interactivity;
using ExifGlass.ExifTools;
using System.Threading.Tasks;

namespace ExifGlass;

public partial class SettingsWindow : StyledWindow
{
    /// <summary>
    /// Gets the result of <see cref="SettingsWindow"/>.
    /// </summary>
    public SettingsResult Result { get; private set; } = SettingsResult.Cancel;

    public SettingsWindow()
    {
        InitializeComponent();

        BtnSelectExecutable.Click += BtnSelectExecutable_Click;
        BtnOK.Click += BtnOK_Click;
        BtnCancel.Click += BtnCancel_Click;

        TxtExecutable.TextChanged += ExifToolConfig_Changed;
        TxtArguments.TextChanged += ExifToolConfig_Changed;
    }


    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // Loads user settings
        CmbTheme.SelectedIndex = (int)Config.ThemeMode;
        ChkTopMost.IsChecked = Topmost = Config.EnableWindowTopMost;
        TxtExecutable.Text = Config.ExifToolExecutable;
        TxtArguments.Text = Config.ExifToolArguments;
        Result = SettingsResult.Cancel;
    }


    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Escape
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }


    // Control events
    #region Control events

    private void ExifToolConfig_Changed(object? sender, TextChangedEventArgs e)
    {
        var exiftoolPath = string.IsNullOrWhiteSpace(TxtExecutable.Text)
            ? ExifTool.DefaultExifToolPath
            : TxtExecutable.Text.Trim();

        TxtPreview.Text = $"{exiftoolPath} {ExifTool.DefaultCommands} {TxtArguments.Text?.Trim()} \"C:\\path\\to\\photo.jpg\"";
    }


    private void BtnSelectExecutable_Click(object? sender, RoutedEventArgs e)
    {
        _ = OpenFilePickerAsync();
    }


    private async Task OpenFilePickerAsync()
    {
        var filePicker = await StorageProvider.OpenFilePickerAsync(new()
        {
            AllowMultiple = false,
            FileTypeFilter =
            [
                new("ExifTool's binary file")
                {
                    Patterns = ["*.exe"]
                },
            ],
        });
        if (filePicker == null || filePicker.Count == 0) return;

        TxtExecutable.Text = $"\"{filePicker[0].Path.LocalPath}\"";
    }

    private void BtnOK_Click(object? sender, RoutedEventArgs e)
    {
        Config.ThemeMode = (ThemeMode)CmbTheme.SelectedIndex;
        Config.EnableWindowTopMost = ChkTopMost.IsChecked ?? false;
        Config.ExifToolExecutable = (TxtExecutable.Text ?? "").Trim();
        Config.ExifToolArguments = (TxtArguments.Text ?? "").Trim();

        // apply Theme mode
        Config.ApplyThemeMode(Config.ThemeMode);

        Result = SettingsResult.OK;
        Close();
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        Result = SettingsResult.Cancel;
        Close();
    }

    #endregion // Control events


}


public enum SettingsResult
{
    OK,
    Cancel,
}