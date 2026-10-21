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
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ExifGlass.UI;
using ExifGlass.Windows;

namespace ExifGlass;

public partial class App : Application
{
    #region Public methods

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        DesignSystem.Initialize(this);
    }


    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (AppBootstrap.ReadTheme(desktop.Args) is { } theme)
            {
                RequestedThemeVariant = theme;
            }

#if DEBUG
            if (AppBootstrap.IsGallery(desktop.Args))
            {
                desktop.MainWindow = new UI.Gallery.GalleryWindow();
                base.OnFrameworkInitializationCompleted();
                return;
            }
#endif

            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    #endregion // Public methods
}
