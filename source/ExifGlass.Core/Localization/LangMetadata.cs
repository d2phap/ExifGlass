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
namespace ExifGlass.Core.Localization;

/// <summary>
/// A language pack's <c>_Metadata</c>: which language it is, who translated it, and the app version it was made for.
/// </summary>
public sealed class LangMetadata
{
    #region Properties

    /// <summary>
    /// The language tag, e.g. <c>vi-VN</c>.
    /// </summary>
    public string Code { get; set; } = "en-US";

    public string EnglishName { get; set; } = "English";

    /// <summary>
    /// The name in the language itself, e.g. <c>Tiếng Việt</c>; the Settings list shows it first.
    /// </summary>
    public string LocalName { get; set; } = "English";

    /// <summary>
    /// The people who wrote the translation, in the order they want to be credited.
    /// </summary>
    public List<string> Contributors { get; set; } = ["Duong Dieu Phap"];

    /// <summary>
    /// The ExifGlass version the pack was made for; older packs miss too many keys to be listed.
    /// </summary>
    public float MinVersion { get; set; } = Lang.SPEC_VERSION;

    #endregion // Properties
}
