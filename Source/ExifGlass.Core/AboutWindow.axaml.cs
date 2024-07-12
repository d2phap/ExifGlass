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
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ExifGlass;

public partial class AboutWindow : StyledWindow
{
    public AboutWindow()
    {
        InitializeComponent();

        BtnClose.Click += BtnClose_Click;
        BtnCheckForUpdate.Click += BtnCheckForUpdate_Click;
        BtnExifGlassStore.Click += BtnExifGlassStore_Click;

        TblVersion.Text = Config.AppVersion.ToString();
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

    private void BtnExifGlassStore_Click(object? sender, RoutedEventArgs e)
    {
        Config.OpenExifGlassMsStore();
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
