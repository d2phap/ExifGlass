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
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace ExifGlass.UI.Tests;

/// <summary>
/// Source checks over the XAML of the design system and the app (plan 5.1, 5.3, S1, S3).
/// </summary>
public partial class LintTests
{
    #region Fields and constants

    // Segoe UI Variable's ascent + descent per em, the largest content area of the UI fonts (S3)
    private const double CONTENT_AREA = (2210.0 + 514.0) / 2048;

    #endregion // Fields and constants


    #region Tests

    [Fact]
    public void ViewsUseTokensNotColourLiterals()
    {
        var offenders = new List<string>();
        foreach (var file in ViewFiles())
        {
            var text = Comment().Replace(File.ReadAllText(file), string.Empty);
            foreach (Match m in ColourLiteral().Matches(text)) offenders.Add($"{Rel(file)}: {m.Value}");
        }

        Assert.True(offenders.Count == 0, "Colour literals outside Tokens.axaml:\n" + string.Join("\n", offenders));
    }


    [Fact]
    public void LineHeightNeverCutsIntoTheContentArea()
    {
        var offenders = new List<string>();
        foreach (var file in ViewFiles().Append(Path.Combine(Render.SourceDir, "ExifGlass.UI", "Tokens", "Typography.axaml")))
        {
            var text = File.ReadAllText(file);

            // a style that sets both, or an element that does
            foreach (Match block in StyleBlock().Matches(text)) Check(file, block.Value, offenders);
            foreach (Match element in ElementWithLineHeight().Matches(text)) Check(file, element.Value, offenders);
        }

        Assert.True(offenders.Count == 0, "LineHeight below the content area (S3):\n" + string.Join("\n", offenders));
    }


    [Fact]
    public void SquircleBorderPartsAreStyledAsBorder()
    {
        var offenders = new List<string>();
        foreach (var file in ViewFiles())
        {
            foreach (Match m in SquircleSelector().Matches(File.ReadAllText(file))) offenders.Add($"{Rel(file)}: {m.Value}");
        }

        Assert.True(offenders.Count == 0, "SquircleBorder styles as Border; this selector never matches (S1):\n" + string.Join("\n", offenders));
    }


    [AvaloniaFact]
    public void EveryResourceAViewNamesExistsInBothThemes()
    {
        var app = Application.Current!;
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in ViewFiles())
        {
            foreach (Match m in ResourceReference().Matches(File.ReadAllText(file)))
            {
                var key = m.Groups[2].Value;
                if (key.StartsWith("{", StringComparison.Ordinal) || key.Contains(':')) continue;

                foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                {
                    if (!app.TryGetResource(key, theme, out _) && !LocalKey(file, key)) missing.Add($"{Rel(file)}: {key} ({theme})");
                }
            }
        }

        Assert.True(missing.Count == 0, "Missing resources:\n" + string.Join("\n", missing));
    }

    #endregion // Tests


    #region Private methods

    // the design system's themes and every view of the app; the token and icon sources are generated
    private static IEnumerable<string> ViewFiles()
    {
        var source = Render.SourceDir;
        var ui = Directory.GetFiles(Path.Combine(source, "ExifGlass.UI", "Themes"), "*.axaml").Where(f => !f.EndsWith("Icons.axaml", StringComparison.Ordinal));
        var lib = Directory.GetFiles(Path.Combine(source, "ExifGlass.Lib"), "*.axaml", SearchOption.AllDirectories).Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal) && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal));
        return ui.Concat(lib);
    }


    private static void Check(string file, string block, List<string> offenders)
    {
        var lineHeight = Number(block, "LineHeight");
        if (lineHeight is null) return;

        var fontSize = Number(block, "FontSize");
        if (fontSize is null)
        {
            offenders.Add($"{Rel(file)}: LineHeight {lineHeight} without a FontSize beside it");
            return;
        }

        if (lineHeight < fontSize * CONTENT_AREA - 0.01) offenders.Add($"{Rel(file)}: LineHeight {lineHeight} under {fontSize} x {CONTENT_AREA:0.###}");
    }


    private static double? Number(string text, string property)
    {
        // a setter value, an attribute, or the Windows size of an OnPlatform (off Windows the +1 is coerced back)
        var m = Regex.Match(text, $"(?:Property=\"{property}\"\\s+Value=\"([\\d.]+)\"|\\b{property}=\"([\\d.]+)\"|Property=\"{property}\">\\s*<OnPlatform[^>]*\\bWindows=\"([\\d.]+)\")");
        if (!m.Success) return null;

        var value = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;
        return double.Parse(value, CultureInfo.InvariantCulture);
    }


    // a key defined in the same file (a theme's own ControlTheme or converter)
    private static bool LocalKey(string file, string key) => File.ReadAllText(file).Contains($"x:Key=\"{key}\"", StringComparison.Ordinal);


    private static string Rel(string file) => Path.GetRelativePath(Render.SourceDir, file);


    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex Comment();


    [GeneratedRegex(@"(?<![\w&])#(?:[0-9A-Fa-f]{8}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{3})\b")]
    private static partial Regex ColourLiteral();


    [GeneratedRegex(@"<Style\b[^>]*>.*?</Style>", RegexOptions.Singleline)]
    private static partial Regex StyleBlock();


    [GeneratedRegex(@"<\w[\w:.]*\b[^>]*\bLineHeight=""[^""]*""[^>]*>", RegexOptions.Singleline)]
    private static partial Regex ElementWithLineHeight();


    [GeneratedRegex(@"Selector=""[^""]*\|SquircleBorder[^""]*""")]
    private static partial Regex SquircleSelector();


    [GeneratedRegex(@"\{(DynamicResource|StaticResource)\s+([^}\s]+)\}")]
    private static partial Regex ResourceReference();

    #endregion // Private methods
}
