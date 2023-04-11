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
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace ExifGlass
{
    public partial class App : Application
    {
        public static readonly UpdateService Updater = new();


        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // load user configs
                Config.Load();
                _ = CheckAndRunAutoUpdateAsync();

                if (Current != null)
                {
                    // load Theme mode
                    var themeVariant = ThemeVariant.Default;
                    if (Config.ThemeMode == ThemeMode.Dark)
                    {
                        themeVariant = ThemeVariant.Dark;
                    }
                    else if (Config.ThemeMode == ThemeMode.Light)
                    {
                        themeVariant = ThemeVariant.Light;
                    }

                    Current.RequestedThemeVariant = themeVariant;
                }


                desktop.MainWindow = new MainWindow()
                {
                    Position = new PixelPoint(Config.WindowPositionX, Config.WindowPositionY),
                    Width = Config.WindowWidth,
                    Height = Config.WindowHeight,
                    WindowState = Config.WindowState,
                    Topmost = Config.EnableWindowTopMost,
                };
                desktop.ShutdownRequested += Desktop_ShutdownRequested;
            }

            base.OnFrameworkInitializationCompleted();
        }


        private void Desktop_ShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
        {
            _ = Config.SaveAsync();
        }


        /// <summary>
        /// Checks for new update.
        /// </summary>
        public static async Task CheckAndRunAutoUpdateAsync()
        {
            var shouldCheckForUpdate = false;

            if (Config.AutoUpdate != "0")
            {
                if (DateTime.TryParseExact(
                    Config.AutoUpdate,
                    Config.DATETIME_FORMAT,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var lastUpdate))
                {
                    // Check for update every 5 days
                    if (DateTime.UtcNow.Subtract(lastUpdate).TotalDays > 5)
                    {
                        shouldCheckForUpdate = true;
                    }
                }
                else
                {
                    shouldCheckForUpdate = true;
                }
            }


            if (shouldCheckForUpdate == true)
            {
                await Updater.GetUpdatesAsync();

                // save last update
                Config.AutoUpdate = DateTime.UtcNow.ToString(Config.DATETIME_FORMAT);

                // show update window
                var win = new UpdateWindow()
                {
                    WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen,
                };
                win.Show();
            }
        }


    }
}