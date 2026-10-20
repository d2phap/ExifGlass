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
using System.Collections.Frozen;

namespace ExifGlass.Core.Metadata;

/// <summary>
/// The short description each group header shows on its right.
/// </summary>
public static class GroupInfo
{
    #region Fields and constants

    /// <summary>
    /// The synthetic AI generation group of the table (D22).
    /// </summary>
    public const string AI_GROUP = "AI";

    private static readonly FrozenDictionary<string, string> DESCRIPTIONS = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [AI_GROUP] = "Parsed from the tags below",
        ["ExifTool"] = "Reader",
        ["File"] = "File system",
        ["EXIF"] = "Camera & capture",
        ["MakerNotes"] = "Vendor-specific",
        ["XMP"] = "Adobe XMP",
        ["ICC_Profile"] = "Color profile",
        ["Composite"] = "Derived by ExifTool",
        ["PNG"] = "PNG chunks",
        ["IPTC"] = "Press metadata",
        ["JFIF"] = "JPEG header",
        ["Photoshop"] = "Photoshop resources",
        ["QuickTime"] = "Video container",
        ["JUMBF"] = "C2PA Content Credentials",
        ["MPF"] = "Multi-picture format",
        ["RIFF"] = "WebP and AVI container",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Describes a group; MakerNotes take the camera make ("Fujifilm maker notes").
    /// </summary>
    public static string Describe(string group, string? make = null)
    {
        if (group == "MakerNotes" && !string.IsNullOrWhiteSpace(make))
        {
            var m = make.Trim();
            return string.Concat(m[..1].ToUpperInvariant(), m[1..].ToLowerInvariant(), " maker notes");
        }

        return DESCRIPTIONS.GetValueOrDefault(group, string.Empty);
    }

    #endregion // Public methods
}
