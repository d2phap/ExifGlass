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

namespace ExifGlass;

public partial class UpdateWindow : Window
{
    public UpdateWindow()
    {
        InitializeComponent();

        GotFocus += AboutWindow_GotFocus;
        LostFocus += AboutWindow_LostFocus;

        BtnReadMore.Click += BtnReadMore_Click;
        BtnDownload.Click += BtnDownload_Click;
        BtnClose.Click += BtnClose_Click;
        BtnExifGlassStore.Click += BtnExifGlassStore_Click;


        if (App.Updater.HasNewUpdate)
        {
            TxtHeading.Text = "A new version is available!";
        }
        else
        {
            TxtHeading.Text = "You're using the latest version!";
        }

        TxtCurrentVersion.Text = "Current version: " + Config.AppVersion.ToString();
        TxtNewVersion.Text = "Latest version: " + App.Updater.CurrentReleaseInfo?.Version.ToString();
        TxtPublishedDate.Text = "Published date: " + App.Updater.CurrentReleaseInfo?.PublishedDate.ToString();

        TxtReleaseTitle.Text = App.Updater.CurrentReleaseInfo?.Title;
        TxtReleaseDescription.Text = App.Updater.CurrentReleaseInfo?.Description;
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


    private void BtnExifGlassStore_Click(object? sender, RoutedEventArgs e)
    {
        Config.OpenExifGlassMsStore();
    }


    private void BtnReadMore_Click(object? sender, RoutedEventArgs e)
    {
        Config.OpenUrl(App.Updater.CurrentReleaseInfo?.ChangelogUrl.ToString(), "app_update");
    }


    private void BtnDownload_Click(object? sender, RoutedEventArgs e)
    {
        Config.OpenUrl(App.Updater.CurrentReleaseInfo?.DownloadUrl.ToString(), "app_update");
    }


    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion // Control events


}