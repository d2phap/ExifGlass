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
using System.Globalization;

namespace ExifGlass.UI;

/// <summary>
/// The design system's text that is built from parts (plan 5.9): counts, shortcuts and labels; the words themselves are LangId keys.
/// </summary>
public static class UiText
{
    #region Constants

    // names and symbols every language keeps
    public const string STORE_TARGET = "Microsoft Store";
    public const string RELEASES_TARGET = "GitHub Releases";
    public const string COLUMN_INDEX = "#";
    public const string IPTC_OVERLINE = "IPTC";

    #endregion // Constants


    #region Public methods

    /// <summary>
    /// Writes a shortcut as the OS does: "Ctrl+K" on Windows and Linux, Command-K on macOS (DS 6.2).
    /// </summary>
    public static string Shortcut(string key, bool shift = false)
    {
        if (OperatingSystem.IsMacOS()) return (shift ? "⇧" : "") + "⌘" + key;

        return (shift ? "Ctrl+Shift+" : "Ctrl+") + key;
    }


    /// <summary>
    /// "1 tag" or "1,234 tags".
    /// </summary>
    public static string Tags(int count) => count == 1 ? Lang.Current[LangId._TagsOne] : Lang.Current[LangId._Tags, count];


    public static string Characters(int count) => count == 1 ? Lang.Current[LangId._CharactersOne] : Lang.Current[LangId._Characters, count];


    /// <summary>
    /// A workflow's count noun under its number: "1 node", "15 nodes".
    /// </summary>
    public static string Nodes(int n) => n == 1 ? Lang.Current[LangId.Ai_NodeOne] : Lang.Current[LangId.Ai_Nodes];


    public static string SamplerNoun(int n) => n == 1 ? Lang.Current[LangId.Ai_SamplerOne] : Lang.Current[LangId.Ai_Samplers];


    public static string ModelNoun(int n) => n == 1 ? Lang.Current[LangId.Ai_ModelOne] : Lang.Current[LangId.Ai_Models];


    public static string ParsedInto(int n) => n == 1 ? Lang.Current[LangId.Inspector_ParsedIntoOne] : Lang.Current[LangId.Inspector_ParsedInto, n];


    public static string MoreCharacters(int n) => Lang.Current[LangId.Ai_MoreCharacters, Characters(n)];


    public static string CharactersTitle(int n) => Lang.Current[LangId.Ai_CharactersTitle, n];


    public static string ModelsTitle(int n) => Lang.Current[LangId.Ai_ModelsTitle, n];


    public static string MoreCount(int n) => Lang.Current[LangId._MoreCount, n];


    public static string Samplers(int n) => Lang.Current[LangId.Ai_SamplersCount, n];


    public static string ManifestCount(int n) => n == 1 ? Lang.Current[LangId.Ai_ManifestCountOne] : Lang.Current[LangId.Ai_ManifestCount, n];


    public static string AigcCode(string code) => Lang.Current[LangId.Ai_AigcCode, code];


    public static string StylesNegative(string text) => Lang.Current[LangId.Ai_StylesNegative, text];


    public static string SaveWorkflowTip(string app) => Lang.Current[LangId.Ai_SaveWorkflowTip, app];


    public static string CopyWorkflowTip(string size) => Lang.Current[LangId.Ai_CopyWorkflowTip, size];


    public static string CopiedHash(string model) => Lang.Current[LangId.Ai_CopiedHash, model];


    public static string SourceTagsDescription(IReadOnlyList<string> tags) => (tags.Count == 1 ? Lang.Current[LangId._TagsOne] : Lang.Current[LangId._Tags, tags.Count])
        + (tags.Count > 0 ? ": " + string.Join(", ", tags) : string.Empty);


    /// <summary>
    /// Was: {0} for an edited value.
    /// </summary>
    public static string Was(string? value) => string.Format(CultureInfo.InvariantCulture, Lang.Current[LangId.Inspector_Was], value);

    #endregion // Public methods
}
