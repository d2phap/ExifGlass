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

namespace ExifGlass.Win32.Common;

internal sealed class Win32ShellProvider : IShellProvider
{
    #region Fields and constants

    private readonly Lazy<string?> _packageFamilyName = new(ReadPackageFamilyName);

    #endregion // Fields and constants


    #region Properties

    public bool AnimatesOwnedWindows => !User32.SystemParametersInfoW(User32.SPI_GETCLIENTAREAANIMATION, 0, out var on, 0) || on != 0;

    public string? PackageFamilyName => _packageFamilyName.Value;

    #endregion // Properties


    #region Public methods

    public bool TrySetMicaBackdrop(nint windowHandle)
    {
        if (windowHandle == 0 || Environment.OSVersion.Version.Build < DwmApi.BUILD_SYSTEM_BACKDROP) return false;

        var type = DwmApi.DWMSBT_MAINWINDOW;
        return DwmApi.DwmSetWindowAttribute(windowHandle, DwmApi.DWMWA_SYSTEMBACKDROP_TYPE, ref type, sizeof(int)) >= 0;
    }


    public bool IsTopmost(nint windowHandle) => windowHandle != 0 && (User32.GetWindowLongPtrW(windowHandle, User32.GWL_EXSTYLE) & User32.WS_EX_TOPMOST) != 0;

    #endregion // Public methods


    #region Private methods

    private static unsafe string? ReadPackageFamilyName()
    {
        uint length = 0;
        if (Kernel32.GetCurrentPackageFamilyName(ref length, null) != Kernel32.ERROR_INSUFFICIENT_BUFFER || length == 0) return null;

        var buffer = new char[length];
        fixed (char* p = buffer)
        {
            if (Kernel32.GetCurrentPackageFamilyName(ref length, p) != Kernel32.ERROR_SUCCESS) return null;
        }

        // the length counts the terminating null
        return new string(buffer, 0, (int)Math.Max(0, length - 1));
    }

    #endregion // Private methods
}
