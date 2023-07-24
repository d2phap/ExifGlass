/*
ExifGlass - Standalone Exif tool for ImageGlass
Copyright (C) 2023 DUONG DIEU PHAP
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
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using ExifGlass.ExifTools;
using System.Threading.Tasks;

namespace ExifGlass;

public partial class SettingsWindow : Window
{
    /// <summary>
    /// Gets the result of <see cref="SettingsWindow"/>.
    /// </summary>
    public SettingsResult Result { get; private set; } = SettingsResult.Cancel;

    public SettingsWindow()
    {
        InitializeComponent();

        GotFocus += SettingsWindow_GotFocus;
        LostFocus += SettingsWindow_LostFocus;

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
        ChkTopMost.IsChecked = Topmost = Config.EnableWindowTopMost;
        TxtExecutable.Text = Config.ExifToolExecutable;
        TxtArguments.Text = Config.ExifToolArguments;
        Result = SettingsResult.Cancel;
    }


    // Control events
    #region Control events

    private void SettingsWindow_GotFocus(object? sender, GotFocusEventArgs e)
    {
        this.SetDynamicResource(BackgroundProperty, "SystemAltMediumHighColor");
    }


    private void SettingsWindow_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (Application.Current is not Application app) return;

        if (app.ActualThemeVariant == ThemeVariant.Dark)
        {
            Background = new SolidColorBrush(Color.FromRgb(32, 32, 32));
        }
        else
        {
            Background = new SolidColorBrush(Color.FromRgb(243, 243, 243));
        }
    }


    private void ExifToolConfig_Changed(object? sender, TextChangedEventArgs e)
    {
        TxtPreview.Text = $"{TxtExecutable.Text?.Trim()} {ExifTool.DefaultCommands} {TxtArguments.Text?.Trim()} \"C:\\path\\to\\photo.jpg\"";
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
            FileTypeFilter = new FilePickerFileType[]
            {
                new FilePickerFileType("ExifTool's binary file")
                {
                    Patterns = new[] { "*.exe" }
                },
            },
        });
        if (filePicker == null || filePicker.Count == 0) return;

        TxtExecutable.Text = $"\"{filePicker[0].Path.LocalPath}\"";
    }

    private void BtnOK_Click(object? sender, RoutedEventArgs e)
    {
        Config.EnableWindowTopMost = ChkTopMost.IsChecked ?? false;
        Config.ExifToolExecutable = (TxtExecutable.Text ?? "").Trim();
        Config.ExifToolArguments = (TxtArguments.Text ?? "").Trim();

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