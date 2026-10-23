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
namespace ExifGlass.Core.Platform;

/// <summary>
/// Window features that only the operating system provides; each app head brings its own.
/// </summary>
public interface IShellProvider
{
    #region Properties

    /// <summary>
    /// Gets whether the OS animates owned windows itself as they open and close (S4: DWM does unless Animation effects are off).
    /// </summary>
    bool AnimatesOwnedWindows => false;

    #endregion // Properties


    #region Methods

    /// <summary>
    /// Gives the window the native Mica backdrop; false when the OS can't.
    /// </summary>
    bool TrySetMicaBackdrop(nint windowHandle);

    #endregion // Methods
}
