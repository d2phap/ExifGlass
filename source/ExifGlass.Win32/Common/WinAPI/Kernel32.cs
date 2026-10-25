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
using System.Runtime.InteropServices;

namespace ExifGlass.Win32.Common.WinAPI;

internal static partial class Kernel32
{
    #region Constants

    public const int ERROR_SUCCESS = 0;
    public const int ERROR_INSUFFICIENT_BUFFER = 122;

    #endregion // Constants


    #region Native methods

    // APPMODEL_ERROR_NO_PACKAGE (15700) when the process has no package identity
    [LibraryImport("kernel32.dll")]
    public static unsafe partial int GetCurrentPackageFamilyName(ref uint length, char* familyName);

    #endregion // Native methods
}
