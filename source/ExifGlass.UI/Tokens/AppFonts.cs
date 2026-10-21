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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace ExifGlass.UI.Tokens;

/// <summary>
/// The UI fonts (plan 5.3): Segoe UI Variable Text on Windows, Inter 1 pt smaller on macOS and Linux, and the system's mono font.
/// </summary>
public static class AppFonts
{
    #region Fields and constants

    public const string SEGOE_UI_VARIABLE = "Segoe UI Variable Text";

    public const string INTER = "fonts:Inter#Inter";

    // no bundled mono (owner, 2026-10-21): the first installed one wins
    private static readonly string[] WINDOWS_MONO = ["Cascadia Mono", "Consolas"];
    private static readonly string[] MAC_MONO = ["Menlo", "Monaco"];
    private static readonly string[] LINUX_MONO = ["DejaVu Sans Mono", "Noto Sans Mono", "Liberation Mono", "Ubuntu Mono"];

    private static string? _mono;

    #endregion // Fields and constants


    #region Properties

    /// <summary>
    /// Gets the UI family on this OS.
    /// </summary>
    public static string Ui => OperatingSystem.IsWindows() ? SEGOE_UI_VARIABLE : INTER;


    /// <summary>
    /// Gets the installed mono family; the generic one when none of ours is there.
    /// </summary>
    public static string Mono => _mono ??= ResolveMono();


    /// <summary>
    /// Gets the point offset the -1 pt coercion applies on this OS (D4).
    /// </summary>
    public static double SizeOffset => OperatingSystem.IsWindows() ? 0 : -1;

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Preloads the Windows font weights and, off Windows, makes Inter the default family, before any text is measured.
    /// </summary>
    public static AppBuilder WithAppFonts(this AppBuilder builder)
    {
        builder.ConfigureFonts(fontManager =>
        {
            if (OperatingSystem.IsWindows())
            {
                LoadWeights(fontManager, SEGOE_UI_VARIABLE);
                LoadWeights(fontManager, WINDOWS_MONO[0]);
            }
        });

        return OperatingSystem.IsWindows() ? builder : builder.With(new FontManagerOptions { DefaultFamilyName = INTER });
    }


    /// <summary>
    /// Takes 1 pt off every font size on macOS and Linux, once (D4); mono size tokens add it back (S3).
    /// </summary>
    public static AppBuilder WithPlatformFontSizes(this AppBuilder builder) => SizeOffset == 0 ? builder : builder.WithAdjustedFontSizes(SizeOffset);


    /// <summary>
    /// Adds points (negative to shrink) to every font size set on a control, a style or an inline; inherited sizes are adjusted once, never twice.
    /// </summary>
    public static AppBuilder WithAdjustedFontSizes(this AppBuilder builder, double points)
    {
        var size = TextElement.FontSizeProperty;
        StyledPropertyMetadata<double> Metadata() => new(size.GetDefaultValue(typeof(Control)), coerce: (_, value) => Math.Max(1, value + points));

        // controls carry the attached size, inlines their own; TextElement owns it, so its metadata can't be overridden
        size.OverrideMetadata<Control>(Metadata());
        size.OverrideMetadata<Inline>(Metadata());

        return builder;
    }


    /// <summary>
    /// Points the FontMono token at the installed mono family.
    /// </summary>
    public static void ApplyMono(IResourceDictionary resources) => resources[TokenKeys.FONT_MONO] = new FontFamily(Mono);

    #endregion // Public methods


    #region Private methods

    private static string ResolveMono()
    {
        var wanted = OperatingSystem.IsWindows() ? WINDOWS_MONO : OperatingSystem.IsMacOS() ? MAC_MONO : LINUX_MONO;
        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var family in FontManager.Current.SystemFonts) installed.Add(family.Name);

        foreach (var name in wanted)
        {
            if (installed.Contains(name)) return name;
        }

        return "monospace";
    }


    // every Segoe UI Variable instance reports weight 400, so a weight first asked for after another reuses it; asked for up front, each gets its own
    private static void LoadWeights(FontManager fontManager, string family)
    {
        foreach (var weight in (ReadOnlySpan<FontWeight>)[FontWeight.Normal, FontWeight.Medium, FontWeight.SemiBold])
        {
            fontManager.TryGetGlyphTypeface(new Typeface(new FontFamily(family), FontStyle.Normal, weight), out _);
        }
    }

    #endregion // Private methods
}
