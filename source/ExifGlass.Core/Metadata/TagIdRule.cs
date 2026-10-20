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
namespace ExifGlass.Core.Metadata;

/// <summary>
/// Turns ExifTool's tag ID into the Tag ID column text.
/// </summary>
public static class TagIdRule
{
    #region Fields and constants

    public const string NONE = "—";

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Numeric IDs as upper-case hex ("0x829D"), text IDs as they are ("desc"), "—" for File, Composite and ExifTool.
    /// </summary>
    public static string ToDisplay(string group0, string? id)
    {
        if (string.IsNullOrEmpty(id)) return NONE;
        if (group0 is "File" or "Composite" or "ExifTool") return NONE;

        if (id.Length > 2 && id[0] == '0' && (id[1] == 'x' || id[1] == 'X') && IsHex(id.AsSpan(2)))
        {
            return string.Concat("0x", id.AsSpan(2).ToString().ToUpperInvariant());
        }

        return id;
    }

    #endregion // Public methods


    #region Private methods

    private static bool IsHex(ReadOnlySpan<char> s)
    {
        foreach (var c in s)
        {
            if (!char.IsAsciiHexDigit(c)) return false;
        }

        return true;
    }

    #endregion // Private methods
}
