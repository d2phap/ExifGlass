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
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using System.Globalization;

namespace ExifGlass.UI.Tokens;

/// <summary>
/// The app's accent (Appendix A item 84): the tokens' blue, or another colour from which the fill, the text on it, the readable text per theme, the tints and the accent shadows are made.
/// </summary>
public static class AccentPalette
{
    #region Fields and constants

    public static readonly Color DefaultAccent = Color.Parse("#225EDB");

    // TextPrimary of the light theme: text and icons on an accent too light for white, as Windows does on a gold accent
    private static readonly Color DarkOnAccent = Color.Parse("#212529");

    // WCAG AA for text: white on the fill, and the accent text on each theme's surfaces
    private const double TEXT_CONTRAST = 4.5;
    private const double LIGHTNESS_STEP = 0.01;

    // the Accent alphas Tokens.axaml defines, in percent
    private static readonly int[] Tints = [0, 8, 10, 12, 30, 35];

    // the light theme's dimmest surface and the dark theme's brightest, where accent text has the least contrast
    private static readonly Color LightSurfaceFallback = Color.Parse("#F3F3F3");
    private static readonly Color DarkSurfaceFallback = Color.Parse("#343A40");

    #endregion // Fields and constants


    #region Events

    /// <summary>
    /// Occurs after the accent resources change; text that resolves its brushes once builds again.
    /// </summary>
    public static event EventHandler? Changed;

    #endregion // Events


    #region Public methods

    /// <summary>
    /// Puts an accent's resources over the tokens in the app's resources; null or the default blue takes the tokens' own back.
    /// </summary>
    public static void Apply(IResourceDictionary resources, Color? accent)
    {
        var light = ThemeDictionary(resources, ThemeVariant.Light);
        var dark = ThemeDictionary(resources, ThemeVariant.Dark);

        if (accent is not { } chosen || Opaque(chosen) == DefaultAccent)
        {
            foreach (var key in SharedKeys()) resources.Remove(key);
            foreach (var key in TextKeys()) light.Remove(key);
            foreach (var key in TextKeys()) dark.Remove(key);
            Changed?.Invoke(null, EventArgs.Empty);
            return;
        }

        var fill = Fill(chosen);
        SetColor(resources, TokenKeys.ACCENT_COLOR, TokenKeys.ACCENT_BRUSH, fill);
        SetColor(resources, TokenKeys.ON_ACCENT_COLOR, TokenKeys.ON_ACCENT_BRUSH, OnColor(fill));
        foreach (var tint in Tints)
        {
            var name = "AccentA" + tint.ToString("00", CultureInfo.InvariantCulture);
            SetColor(resources, name + "Color", name + "Brush", WithAlpha(fill, tint));
        }

        resources[TokenKeys.SHADOW_SELECTED_CARD] = new BoxShadows(new BoxShadow { Spread = 3, Color = WithAlpha(fill, 8) });
        resources[TokenKeys.SHADOW_FOCUS_POINT] = new BoxShadows(new BoxShadow { Spread = 4, Color = WithAlpha(fill, 12) });

        SetColor(light, TokenKeys.ACCENT_TEXT_COLOR, TokenKeys.ACCENT_TEXT_BRUSH, Readable(chosen, Surface(resources, ThemeVariant.Light, LightSurfaceFallback), lighter: false));
        SetColor(dark, TokenKeys.ACCENT_TEXT_COLOR, TokenKeys.ACCENT_TEXT_BRUSH, Readable(chosen, Surface(resources, ThemeVariant.Dark, DarkSurfaceFallback), lighter: true));
        Changed?.Invoke(null, EventArgs.Empty);
    }


    /// <summary>
    /// The accent as a fill: as it is when white or dark text reads on it, else darkened until white does.
    /// </summary>
    public static Color Fill(Color accent)
    {
        var c = Opaque(accent);
        return Contrast(c, Colors.White) >= TEXT_CONTRAST || Contrast(c, DarkOnAccent) >= TEXT_CONTRAST ? c : Readable(c, Colors.White, lighter: false);
    }


    /// <summary>
    /// The text and icons on a fill (OnAccent): white while it reads, the dark text on a light accent.
    /// </summary>
    public static Color OnColor(Color fill) => Contrast(Opaque(fill), Colors.White) >= TEXT_CONTRAST ? Colors.White : DarkOnAccent;


    /// <summary>
    /// The WCAG contrast ratio of two opaque colours, 1 to 21.
    /// </summary>
    public static double Contrast(Color a, Color b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    #endregion // Public methods


    #region Private methods

    // the colour, lightened or darkened in HSL until it reads on the background; it keeps its hue and saturation
    private static Color Readable(Color color, Color background, bool lighter)
    {
        var c = Opaque(color);
        if (Contrast(c, background) >= TEXT_CONTRAST) return c;

        var hsl = c.ToHsl();
        for (var l = hsl.L; l is >= 0 and <= 1; l += lighter ? LIGHTNESS_STEP : -LIGHTNESS_STEP)
        {
            var candidate = new HslColor(1, hsl.H, hsl.S, l).ToRgb();
            if (Contrast(candidate, background) >= TEXT_CONTRAST) return candidate;
        }

        return lighter ? Colors.White : Colors.Black;
    }


    private static double Luminance(Color c)
    {
        static double Channel(byte v)
        {
            var s = v / 255.0;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }


    private static Color Opaque(Color c) => Color.FromRgb(c.R, c.G, c.B);


    // Tokens.axaml rounds n% of 255 to even
    private static Color WithAlpha(Color c, int percent) => Color.FromArgb((byte)Math.Round(255 * percent / 100.0), c.R, c.G, c.B);


    private static void SetColor(IResourceDictionary dictionary, string colorKey, string brushKey, Color color)
    {
        dictionary[colorKey] = color;
        dictionary[brushKey] = new SolidColorBrush(color);
    }


    private static Color Surface(IResourceDictionary resources, ThemeVariant variant, Color fallback) =>
        resources.TryGetResource(variant == ThemeVariant.Light ? TokenKeys.BG_WINDOW_COLOR : TokenKeys.BG_ELEVATED_COLOR, variant, out var value) && value is Color color ? color : fallback;


    // the app's own theme dictionaries come before the merged tokens' in a lookup
    private static IResourceDictionary ThemeDictionary(IResourceDictionary resources, ThemeVariant variant)
    {
        if (resources.ThemeDictionaries.TryGetValue(variant, out var provider) && provider is IResourceDictionary existing) return existing;

        var created = new ResourceDictionary();
        resources.ThemeDictionaries[variant] = created;
        return created;
    }


    private static IEnumerable<string> SharedKeys()
    {
        yield return TokenKeys.ACCENT_COLOR;
        yield return TokenKeys.ACCENT_BRUSH;
        yield return TokenKeys.ON_ACCENT_COLOR;
        yield return TokenKeys.ON_ACCENT_BRUSH;
        foreach (var tint in Tints)
        {
            var name = "AccentA" + tint.ToString("00", CultureInfo.InvariantCulture);
            yield return name + "Color";
            yield return name + "Brush";
        }

        yield return TokenKeys.SHADOW_SELECTED_CARD;
        yield return TokenKeys.SHADOW_FOCUS_POINT;
    }


    private static IEnumerable<string> TextKeys() => [TokenKeys.ACCENT_TEXT_COLOR, TokenKeys.ACCENT_TEXT_BRUSH];

    #endregion // Private methods
}
