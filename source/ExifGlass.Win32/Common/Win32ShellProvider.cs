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
using ExifGlass.Core.Platform;
using ExifGlass.Win32.Common.WinAPI;
using Microsoft.Win32;

namespace ExifGlass.Win32.Common;

internal sealed class Win32ShellProvider : IShellProvider
{
    #region Properties

    public bool AnimatesOwnedWindows => !User32.SystemParametersInfoW(User32.SPI_GETCLIENTAREAANIMATION, 0, out var on, 0) || on != 0;

    #endregion // Properties


    #region Public methods

    public bool TrySetMicaBackdrop(nint windowHandle)
    {
        if (windowHandle == 0 || Environment.OSVersion.Version.Build < DwmApi.BUILD_SYSTEM_BACKDROP || !TransparencyEffectsOn()) return false;

        var type = DwmApi.DWMSBT_MAINWINDOW;
        return DwmApi.DwmSetWindowAttribute(windowHandle, DwmApi.DWMWA_SYSTEMBACKDROP_TYPE, ref type, sizeof(int)) >= 0;
    }


    public void ClearMicaBackdrop(nint windowHandle)
    {
        if (windowHandle == 0 || Environment.OSVersion.Version.Build < DwmApi.BUILD_SYSTEM_BACKDROP) return;

        var type = DwmApi.DWMSBT_NONE;
        _ = DwmApi.DwmSetWindowAttribute(windowHandle, DwmApi.DWMWA_SYSTEMBACKDROP_TYPE, ref type, sizeof(int));
    }

    #endregion // Public methods


    #region Private methods

    // Settings > Personalization > Colors > Transparency effects: off, DWM paints Mica as its own flat colour instead of ours
    private static bool TransparencyEffectsOn()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("EnableTransparency") is not int value || value != 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return true;
        }
    }

    #endregion // Public methods
}
