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
namespace ExifGlass.Core.ExifTool;

/// <summary>
/// The ExifTool that ships next to the app executable.
/// </summary>
public static class BundledExifTool
{
    #region Constants

    private const UnixFileMode EXECUTE_BITS = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    #endregion // Constants


    #region Public methods

    /// <summary>
    /// Returns the bundled ExifTool path, or null when the app ships without one.
    /// </summary>
    public static string? FindPath(string? baseDirectory = null)
    {
        var fileName = OperatingSystem.IsWindows() ? "exiftool.exe" : "exiftool";
        var path = Path.Combine(baseDirectory ?? AppContext.BaseDirectory, fileName);

        return File.Exists(path) ? path : null;
    }


    /// <summary>
    /// Restores the script's executable bit, which a publish from Windows drops.
    /// </summary>
    public static void EnsureExecutable(string? baseDirectory = null)
    {
        if (OperatingSystem.IsWindows() || FindPath(baseDirectory) is not { } path) return;

        try
        {
            var mode = File.GetUnixFileMode(path);
            if ((mode & EXECUTE_BITS) != EXECUTE_BITS)
            {
                File.SetUnixFileMode(path, mode | EXECUTE_BITS);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A read-only install keeps its mode; the self-test reports a script that can't run.
        }
    }

    #endregion // Public methods
}
