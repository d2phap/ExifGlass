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
using Avalonia.Media;
using Avalonia.Threading;
using ExifGlass.Core.Settings;
using ExifGlass.UI.Controls;
using ExifGlass.UI.Tokens;

namespace ExifGlass.Services;

/// <summary>
/// One accent the Settings row offers.
/// </summary>
public sealed record AccentPreset(string Id, string Label, Color Color);


/// <summary>
/// The app's accent (Appendix A item 84): the OS accent, followed live, or a preset; applied at once, as the theme is.
/// </summary>
public static class AccentService
{
    #region Fields and constants

    /// <summary>
    /// Gets the presets after System, hues 25 degrees apart or more around the wheel; Blue is the tokens' own, Yellow carries dark text.
    /// </summary>
    public static IReadOnlyList<AccentPreset> Presets { get; } =
    [
        new("Blue", AppText.ACCENT_BLUE, AccentPalette.DefaultAccent),
        new("Purple", AppText.ACCENT_PURPLE, Color.Parse("#7C3AED")),
        new("Pink", AppText.ACCENT_PINK, Color.Parse("#C2185B")),
        new("Orange", AppText.ACCENT_ORANGE, Color.Parse("#C2410C")),
        new("Yellow", AppText.ACCENT_YELLOW, Color.Parse("#FFB900")),
        new("Green", AppText.ACCENT_GREEN, Color.Parse("#15803D")),
        new("Graphite", AppText.ACCENT_GRAPHITE, Color.Parse("#4B5563")),
    ];

    #endregion // Fields and constants


    #region Events

    /// <summary>
    /// Occurs on the UI thread when the OS accent changes, so the System swatch shows the new one.
    /// </summary>
    public static event EventHandler? SystemAccentChanged;

    #endregion // Events


    #region Public methods

    /// <summary>
    /// The OS accent, or null where the platform reports none.
    /// </summary>
    public static Color? SystemAccent() => Application.Current?.PlatformSettings?.GetColorValues().AccentColor1 is { A: > 0 } accent ? accent : null;


    /// <summary>
    /// The colour an accent setting stands for; null keeps the tokens' blue (no OS accent, or an unknown value).
    /// </summary>
    public static Color? Resolve(string? accent)
    {
        var value = accent?.Trim() ?? string.Empty;
        if (value.Equals(AppSettings.ACCENT_SYSTEM, StringComparison.OrdinalIgnoreCase)) return SystemAccent();

        foreach (var preset in Presets)
        {
            if (value.Equals(preset.Id, StringComparison.OrdinalIgnoreCase)) return preset.Color;
        }

        return value.StartsWith('#') && Color.TryParse(value, out var custom) ? custom : SystemAccent();
    }


    /// <summary>
    /// Switches the accent at once.
    /// </summary>
    public static void Apply(string? accent)
    {
        if (Application.Current is { } app) AccentPalette.Apply(app.Resources, Resolve(accent));
    }


    /// <summary>
    /// Re-applies System whenever the OS accent changes.
    /// </summary>
    public static void Follow(Func<string> current)
    {
        if (Application.Current?.PlatformSettings is not { } platform) return;

        platform.ColorValuesChanged += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (current().Equals(AppSettings.ACCENT_SYSTEM, StringComparison.OrdinalIgnoreCase)) Apply(AppSettings.ACCENT_SYSTEM);
            SystemAccentChanged?.Invoke(null, EventArgs.Empty);
        });
    }


    /// <summary>
    /// The Settings row's swatches: System first, in the OS accent as the app would fill with it, then the presets.
    /// </summary>
    public static IReadOnlyList<SwatchItem> Swatches()
    {
        var list = new List<SwatchItem>(Presets.Count + 1)
        {
            new(AppText.ACCENT_SYSTEM, AccentPalette.Fill(SystemAccent() ?? AccentPalette.DefaultAccent), IconId.DarkTheme),
        };

        foreach (var preset in Presets) list.Add(new SwatchItem(preset.Label, preset.Color));
        return list;
    }


    /// <summary>
    /// A setting's place in Swatches; System for a value no swatch shows.
    /// </summary>
    public static int IndexOf(string? accent)
    {
        for (var i = 0; i < Presets.Count; i++)
        {
            if (string.Equals(accent?.Trim(), Presets[i].Id, StringComparison.OrdinalIgnoreCase)) return i + 1;
        }

        return 0;
    }


    public static string IdAt(int index) => index >= 1 && index <= Presets.Count ? Presets[index - 1].Id : AppSettings.ACCENT_SYSTEM;

    #endregion // Public methods
}
