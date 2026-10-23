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
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using ExifGlass.UI.Tokens;
using Xunit;

namespace ExifGlass.UI.Tests;

public class TokensTests
{
    #region Tests

    [AvaloniaTheory]
    [InlineData("Light", "#F3F3F3", "#FFFFFF")]
    [InlineData("Dark", "#202020", "#282E31")]
    public void SurfacesFollowTheThemeVariant(string variant, string window, string panel)
    {
        var theme = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;

        Assert.Equal(Color.Parse(window), BrushColor(TokenKeys.BG_WINDOW_BRUSH, theme));
        Assert.Equal(Color.Parse(panel), BrushColor(TokenKeys.BG_PANEL_BRUSH, theme));
    }


    [AvaloniaFact]
    public void TheUiFontResolves()
    {
        Assert.True(Application.Current!.TryGetResource(TokenKeys.FONT_UI, null, out var font));
        Assert.IsType<FontFamily>(font);
    }


    [AvaloniaFact]
    public void ARoleSetsTheRampUnderAValueSetOnTheElementAndGivesItBack()
    {
        var plain = new TextBlock();
        var text = new TextBlock { FontWeight = FontWeight.Bold };

        Typography.SetRole(text, TextRole.Meta);
        Assert.Equal(11.5, text.FontSize);
        Assert.Equal(16, text.LineHeight);
        Assert.Equal(FontWeight.Bold, text.FontWeight);

        Typography.SetRole(text, TextRole.MonoBadge);
        Assert.Equal(10.5 - AppFonts.SizeOffset, text.FontSize);
        Assert.True(double.IsNaN(text.LineHeight));
        Assert.Equal(AppFonts.Mono, text.FontFamily.Name);
        Assert.NotNull(text.FontFeatures);

        Typography.SetRole(text, TextRole.None);
        Assert.Equal(plain.FontSize, text.FontSize);
        Assert.Equal(plain.FontFamily, text.FontFamily);
        Assert.Null(text.FontFeatures);
        Assert.Equal(FontWeight.Bold, text.FontWeight);
    }

    #endregion // Tests


    #region Private methods

    private static Color BrushColor(string key, ThemeVariant theme)
    {
        Assert.True(Application.Current!.TryGetResource(key, theme, out var value), $"{key} is missing for {theme}");
        return Assert.IsType<SolidColorBrush>(value).Color;
    }

    #endregion // Private methods
}
