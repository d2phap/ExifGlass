/*
ExifGlass - AI & EXIF metadata viewer
Copyright (C) 2023 - 2027 DUONG DIEU PHAP
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
using ExifGlass.Core.Localization;
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

    private static readonly FrozenDictionary<string, LangId> DESCRIPTIONS = new Dictionary<string, LangId>(StringComparer.Ordinal)
    {
        [AI_GROUP] = LangId.Group_Ai,
        ["ExifTool"] = LangId.Group_ExifTool,
        ["File"] = LangId.Group_File,
        ["EXIF"] = LangId.Group_Exif,
        ["MakerNotes"] = LangId.Group_MakerNotes,
        ["XMP"] = LangId.Group_Xmp,
        ["ICC_Profile"] = LangId.Group_IccProfile,
        ["Composite"] = LangId.Group_Composite,
        ["PNG"] = LangId.Group_Png,
        ["IPTC"] = LangId.Group_Iptc,
        ["JFIF"] = LangId.Group_Jfif,
        ["Photoshop"] = LangId.Group_Photoshop,
        ["QuickTime"] = LangId.Group_QuickTime,
        ["JUMBF"] = LangId.Group_Jumbf,
        ["MPF"] = LangId.Group_Mpf,
        ["RIFF"] = LangId.Group_Riff,
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
            return Lang.Current[LangId.Group_MakerNotesOf, string.Concat(m[..1].ToUpperInvariant(), m[1..].ToLowerInvariant())];
        }

        return DESCRIPTIONS.TryGetValue(group, out var key) ? Lang.Current[key] : string.Empty;
    }

    #endregion // Public methods
}
