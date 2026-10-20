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
/// The parts of an ExifTool JSON key printed with -G0:1[:3]:4.
/// </summary>
public readonly record struct TagKey(string Group0, string Group1, string? Group3, int Copy, string Name)
{
    #region Properties

    /// <summary>
    /// The key the table read prints for the same tag: no family 3, family 1 dropped when it equals family 0.
    /// </summary>
    public string TableKey => Copy > 0
        ? (Group1 == Group0 ? $"{Group0}:Copy{Copy}:{Name}" : $"{Group0}:{Group1}:Copy{Copy}:{Name}")
        : (Group1 == Group0 ? $"{Group0}:{Name}" : $"{Group0}:{Group1}:{Name}");


    /// <summary>
    /// The family-1 group and name, the form ExifTool takes for a write: "ExifIFD:FNumber".
    /// </summary>
    public string WriteName => $"{Group1}:{Name}";

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Splits a key such as "EXIF:ExifIFD:Copy1:Name", "PNG:Main:Prompt" or "JUMBF:CBOR:Doc1-2:Actions".
    /// </summary>
    public static TagKey Parse(ReadOnlySpan<char> key, StringPool? pool = null)
    {
        string group0 = "", group1 = "", name;
        string? group3 = null;
        var copy = 0;
        var part = 0;

        while (true)
        {
            var colon = key.IndexOf(':');
            if (colon < 0)
            {
                name = Get(key, pool);
                break;
            }

            var segment = key[..colon];
            key = key[(colon + 1)..];

            if (part == 0) group0 = Get(segment, pool);
            else if (IsCopy(segment, out var n)) copy = n;
            else if (IsFamily3(segment)) group3 = Get(segment, pool);
            else group1 = Get(segment, pool);

            part++;
        }

        // ExifTool drops family 1 when it equals family 0 (S9)
        if (group1.Length == 0) group1 = group0;

        return new TagKey(group0, group1, group3, copy, name);
    }


    /// <summary>
    /// True for a family-3 document part: "Main" or "Doc1", "Doc1-2-1".
    /// </summary>
    public static bool IsFamily3(ReadOnlySpan<char> segment)
    {
        if (segment.SequenceEqual("Main")) return true;
        if (segment.Length < 4 || !segment.StartsWith("Doc")) return false;

        foreach (var c in segment[3..])
        {
            if (!char.IsAsciiDigit(c) && c != '-') return false;
        }

        return char.IsAsciiDigit(segment[3]);
    }

    #endregion // Public methods


    #region Private methods

    private static bool IsCopy(ReadOnlySpan<char> segment, out int copy)
    {
        copy = 0;
        return segment.Length > 4 && segment.StartsWith("Copy") && int.TryParse(segment[4..], out copy) && copy > 0;
    }


    private static string Get(ReadOnlySpan<char> s, StringPool? pool) => pool is null ? s.ToString() : pool.Get(s);

    #endregion // Private methods
}
