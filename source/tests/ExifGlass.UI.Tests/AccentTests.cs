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
using Avalonia.Threading;
using ExifGlass.Core.Settings;
using ExifGlass.Services;
using ExifGlass.UI.Tokens;
using Xunit;

namespace ExifGlass.UI.Tests;

/// <summary>
/// Appendix A item 84: the accent's derived colours read on both themes, a control follows a change at once, and the default blue keeps the tokens.
/// </summary>
public class AccentTests
{
    #region Tests

    [AvaloniaTheory]
    [InlineData("#6F42C1")]
    [InlineData("#198754")]
    [InlineData("#FFD400")]
    [InlineData("#00B7C3")]
    [InlineData("#F7630C")]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    public void AnyAccentReadsOnBothThemesAndUnderItsText(string hex)
    {
        var app = Application.Current!;
        try
        {
            AccentPalette.Apply(app.Resources, Color.Parse(hex));

            var fill = Resource(app, TokenKeys.ACCENT_COLOR, ThemeVariant.Light);
            var onFill = Resource(app, TokenKeys.ON_ACCENT_COLOR, ThemeVariant.Light);
            Assert.True(AccentPalette.Contrast(fill, onFill) >= 4.5, $"{onFill} on fill {fill}");
            Assert.Equal(fill, Resource(app, TokenKeys.ACCENT_COLOR, ThemeVariant.Dark));
            Assert.Equal(Color.FromArgb(0x59, fill.R, fill.G, fill.B), Resource(app, TokenKeys.ACCENT_A35_COLOR, ThemeVariant.Light));

            var lightText = Resource(app, TokenKeys.ACCENT_TEXT_COLOR, ThemeVariant.Light);
            var darkText = Resource(app, TokenKeys.ACCENT_TEXT_COLOR, ThemeVariant.Dark);
            Assert.True(AccentPalette.Contrast(lightText, Resource(app, TokenKeys.BG_WINDOW_COLOR, ThemeVariant.Light)) >= 4.5, $"light text {lightText}");
            Assert.True(AccentPalette.Contrast(darkText, Resource(app, TokenKeys.BG_ELEVATED_COLOR, ThemeVariant.Dark)) >= 4.5, $"dark text {darkText}");
        }
        finally
        {
            AccentPalette.Apply(app.Resources, null);
        }
    }


    [AvaloniaFact]
    public void TheDefaultBlueIsTheTokensOwn()
    {
        var app = Application.Current!;
        AccentPalette.Apply(app.Resources, Color.Parse("#6F42C1"));
        AccentPalette.Apply(app.Resources, AccentPalette.DefaultAccent);

        Assert.False(app.Resources.ContainsKey(TokenKeys.ACCENT_BRUSH));
        Assert.Equal(Color.Parse("#225EDB"), Resource(app, TokenKeys.ACCENT_COLOR, ThemeVariant.Light));
        Assert.Equal(Colors.White, Resource(app, TokenKeys.ON_ACCENT_COLOR, ThemeVariant.Light));
        Assert.Equal(Color.Parse("#225EDB"), Resource(app, TokenKeys.ACCENT_TEXT_COLOR, ThemeVariant.Light));
        Assert.Equal(Color.Parse("#6EA8FE"), Resource(app, TokenKeys.ACCENT_TEXT_COLOR, ThemeVariant.Dark));
    }


    [AvaloniaFact]
    public void AControlFollowsTheAccentAtOnce()
    {
        var app = Application.Current!;
        var border = new Border { Width = 20, Height = 20 };
        border[!Border.BackgroundProperty] = border.GetResourceObservable(TokenKeys.ACCENT_BRUSH).ToBinding();
        var window = new Window { Width = 100, Height = 100, Content = border };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(AccentPalette.DefaultAccent, ((ISolidColorBrush)border.Background!).Color);

            AccentPalette.Apply(app.Resources, Color.Parse("#087990"));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(Color.Parse("#087990"), ((ISolidColorBrush)border.Background!).Color);
        }
        finally
        {
            AccentPalette.Apply(app.Resources, null);
            window.Close();
        }
    }


    [AvaloniaFact]
    public void TheSettingNamesAPresetAColourOrTheSystem()
    {
        Assert.Equal(Color.Parse("#7C3AED"), AccentService.Resolve("purple"));
        Assert.Equal(Color.Parse("#FF8800"), AccentService.Resolve("#FF8800"));
        Assert.Equal(AccentService.SystemAccent(), AccentService.Resolve(AppSettings.ACCENT_SYSTEM));
        Assert.Equal(AccentService.SystemAccent(), AccentService.Resolve("no such colour"));

        // every preset fills as it is: white text reads on each, dark text on Yellow
        Assert.All(AccentService.Presets, p => Assert.Equal(p.Color, AccentPalette.Fill(p.Color)));
        Assert.All(AccentService.Presets, p => Assert.Equal(p.Id == "Yellow" ? Color.Parse("#212529") : Colors.White, AccentPalette.OnColor(p.Color)));

        var swatches = AccentService.Swatches();
        Assert.Equal(AccentService.Presets.Count + 1, swatches.Count);
        Assert.NotNull(swatches[0].Icon);
        for (var i = 0; i < swatches.Count; i++) Assert.Equal(i, AccentService.IndexOf(AccentService.IdAt(i)));
        Assert.Equal(0, AccentService.IndexOf("#FF8800"));
    }

    #endregion // Tests


    #region Private methods

    private static Color Resource(Application app, string key, ThemeVariant variant) =>
        app.TryGetResource(key, variant, out var value) && value is Color color ? color : throw new InvalidOperationException($"{key} not found");

    #endregion // Private methods
}
