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
using ExifGlass.Core.Common;
using ExifGlass.Core.Platform;
using ExifGlass.Win32.Common;

namespace ExifGlass;

internal static class Program
{
    #region Entry point

    // No Avalonia or SynchronizationContext-reliant code before the app builder starts.
    [STAThread]
    public static int Main(string[] args)
    {
        AppInfo.BuildInfo = new AppBuildInfo();
        PlatformServices.Shell = new Win32ShellProvider();

        if (AppBootstrap.IsSelfTest(args)) return AppBootstrap.RunSelfTest();

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    #endregion // Entry point


    #region Avalonia

    // Also used by the XAML previewer.
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
#if DEBUG
        .LogToTrace()
        .WithDeveloperTools()
#endif
        .UseWin32()
        .UseSkia()
        .UseHarfBuzz();

    #endregion // Avalonia
}
