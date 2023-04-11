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
using Avalonia.Styling;
using System;

namespace ExifGlass;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        GotFocus += AboutWindow_GotFocus;
        LostFocus += AboutWindow_LostFocus;

        BtnClose.Click += BtnClose_Click;
        BtnCheckForUpdate.Click += BtnCheckForUpdate_Click;

        TblVersion.Text = Config.AppVersion.ToString();
    }


    // Control events
    #region Control events

    private void AboutWindow_GotFocus(object? sender, GotFocusEventArgs e)
    {
        this.SetDynamicResource(BackgroundProperty, "SystemAltMediumHighColor");
    }


    private void AboutWindow_LostFocus(object? sender, RoutedEventArgs e)
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


    private void BtnCheckForUpdate_Click(object? sender, RoutedEventArgs e)
    {
        _ = App.CheckForUpdateAsync(true);
    }


    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion // Control events

}
