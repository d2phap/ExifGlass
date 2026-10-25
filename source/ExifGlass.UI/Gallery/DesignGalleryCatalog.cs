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
using Avalonia.Layout;
using Avalonia.Media;
using ExifGlass.UI.Controls;
using ExifGlass.UI.Motion;
using ExifGlass.UI.Tokens;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ExifGlass.UI.Gallery;

/// <summary>
/// Builds the gallery's sections with the spec's own sample content: foundations here, controls in the other part.
/// </summary>
public static partial class DesignGalleryCatalog
{
    #region Fields and constants

    private const string LOGO = "avares://ExifGlass.UI/Assets/Logo/logo-{0}.png";

    private static readonly (string Key, string Use)[] SWATCHES =
    [
        ("BgWindow", "Window"), ("BgPanel", "Panels, cards"), ("BgElevated", "Menus, toasts"), ("BgHeader", "Column header"),
        ("BgSubtle", "Value wells"), ("TextPrimary", "Body"), ("TextSecondary", "Names, captions"), ("TextTertiary", "Hints"),
        ("StrokeHairline", "Borders"), ("StrokeControl", "Outlines"), ("StateHover", "Hover layer"), ("StatePressed", "Pressed layer"),
        ("Accent", "Fills, focus"), ("AccentText", "Links, sorted"), ("Accent2", "AI"), ("Accent2Text", "AI labels"),
        ("Highlight", "Search match"), ("SuccessText", "Saved"), ("DangerText", "Delete, errors"), ("SegmentedThumb", "Thumb"),
        ("InverseBg", "Pending bar"), ("Scrim", "Overlay scrim"),
    ];

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Builds every section with fresh controls (a control lives in one place, so both themes build their own).
    /// </summary>
    public static IReadOnlyList<GallerySection> Build() =>
    [
        Colors(), GroupColors(), TypeRamp(), Shape(), Elevation(), MotionSection(), Icons(),
        .. BuildControls(),
    ];

    #endregion // Public methods


    #region Foundations

    private static GallerySection Colors()
    {
        var states = new List<GalleryState>();
        foreach (var (key, use) in SWATCHES)
        {
            var swatch = new SquircleBorder { Width = 56, Height = 36, CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1) };
            Bind(swatch, SquircleBorder.BackgroundProperty, key + "Brush");
            Bind(swatch, SquircleBorder.BorderBrushProperty, TokenKeys.STROKE_HAIRLINE_BRUSH);
            states.Add(new GalleryState(key, swatch, use));
        }

        return new GallerySection("tokens", "Color tokens", "DS 1.2: overlays are Emphasis (black in Light, white in Dark) at an alpha; Appendix A item 17 tints replace blur.", states);
    }


    private static GallerySection GroupColors()
    {
        var states = new List<GalleryState>();
        foreach (var group in GroupPalette.NamedGroups) states.Add(new GalleryState(group, GroupDot(group, null), ""));
        for (var i = 0; i < GroupPalette.RingSize; i++) states.Add(new GalleryState("ring " + i.ToString(CultureInfo.InvariantCulture), GroupDot(null, i), "other groups"));

        // Proto: square segments in a track that clips them to radius 2, so only the bar's ends are rounded
        const double BAR_WIDTH = 300;
        var segments = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        double[] counts = [17, 55, 23, 31, 9, 12, 6];
        string[] groups = ["File", "EXIF", "MakerNotes", "XMP", "ICC_Profile", "Composite", "PNG"];
        var total = counts.Sum();
        var room = BAR_WIDTH - segments.Spacing * (counts.Length - 1);
        for (var i = 0; i < counts.Length; i++) segments.Children.Add(GroupBarSegment(groups[i], room * counts[i] / total));

        var bar = new SquircleBorder { Width = BAR_WIDTH, Height = 6, CornerRadius = new CornerRadius(2), ClipToBounds = true, Child = segments };
        Bind(bar, SquircleBorder.BackgroundProperty, TokenKeys.EMPHASIS_A06_BRUSH);
        states.Add(new GalleryState("composition bar", bar, "6 px, 2 px gaps, segments by tag count; radius 2 at the ends only"));

        return new GallerySection("groups", "Group palette (D17)", "plan 5.2: the DS eight plus extended groups and a 12-colour ring; each dot keeps 3:1 against Bg.Panel in its theme (tuned by gen-tokens). Danger red is never a group colour.", states);
    }


    private static GallerySection TypeRamp()
    {
        (TextRole Role, string Sample, string Spec)[] rows =
        [
            (TextRole.Display, "ExifGlass", "22 SemiBold"),
            (TextRole.Readout, "f/2.0  1/250  ISO 160", "22 Medium tnum"),
            (TextRole.Title, "Exposure Time", "16/22 SemiBold"),
            (TextRole.Subtitle, "128 tags", "14/20 Medium"),
            (TextRole.EmptyTitle, "Open an AI image or photo", "17 Medium"),
            (TextRole.Body, "Aperture-priority AE", "13/19"),
            (TextRole.BodyStrong, "Remove GPS data", "13/19 Medium"),
            (TextRole.Control, "Export", "12.5 Medium"),
            (TextRole.Caption, "Float above other apps while you compare files.", "12/16"),
            (TextRole.Meta, "JPEG · 18 MB · 7728×5152", "11.5/16"),
            (TextRole.Overline, "GROUPS", "11 Medium +0.06em"),
            (TextRole.Badge, "Edited", "10.5 Medium"),
            (TextRole.Mono, "-EXIF:FNumber=2.0", "mono 11.5"),
            (TextRole.MonoValue, "FUJIFILM Corporation", "mono 12.5"),
            (TextRole.MonoBadge, "128", "mono 10.5 Medium"),
        ];

        var states = new List<GalleryState>();
        foreach (var (role, sample, spec) in rows)
        {
            var text = DesignGallery.Text(sample, role, TokenKeys.TEXT_PRIMARY_BRUSH);
            states.Add(new GalleryState(KebabCase().Replace(role.ToString(), "-$1").ToLowerInvariant(), text, spec));
        }

        states.Add(new GalleryState("fonts", DesignGallery.Text(AppFonts.Ui + " · " + AppFonts.Mono, TextRole.Caption, TokenKeys.TEXT_SECONDARY_BRUSH), "UI and mono family on this OS (mono: the system's, owner 2026-10-21)"));
        return new GallerySection("type", "Type ramp", "DS 1.1; macOS and Linux draw Inter 1 pt smaller (D4), mono sizes add the point back (S3); grayscale antialiasing on every window.", states);
    }


    private static GallerySection Shape()
    {
        (double Height, string Label)[] controls = [(18, "18 / 8"), (24, "24 / 11"), (28, "28 / 13"), (32, "32 / 14"), (36, "36 / 16")];
        var states = new List<GalleryState>();
        foreach (var (height, label) in controls)
        {
            var radius = Math.Min(16, Math.Round(height * 0.45));
            var box = new SquircleBorder { Width = height * 2.5, Height = height, CornerRadius = new CornerRadius(radius), BorderThickness = new Thickness(1) };
            Bind(box, SquircleBorder.BackgroundProperty, TokenKeys.BG_PANEL_BRUSH);
            Bind(box, SquircleBorder.BorderBrushProperty, TokenKeys.STROKE_CONTROL_BRUSH);
            states.Add(new GalleryState(label, box, "height / radius"));
        }

        var panel = new SquircleBorder { Width = 120, Height = 80, CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(1) };
        Bind(panel, SquircleBorder.BackgroundProperty, TokenKeys.BG_PANEL_BRUSH);
        Bind(panel, SquircleBorder.BorderBrushProperty, TokenKeys.STROKE_HAIRLINE_BRUSH);
        states.Add(new GalleryState("panel 18", panel, "panels, cards"));

        // the concentric rule: menu 20 with padding 6 holds items of 14
        var item = new SquircleBorder { CornerRadius = new CornerRadius(14), Height = 34 };
        Bind(item, SquircleBorder.BackgroundProperty, TokenKeys.STATE_HOVER_BRUSH);
        var menu = new SquircleBorder { Width = 160, Padding = new Thickness(6), CornerRadius = new CornerRadius(20), BorderThickness = new Thickness(1), Child = item };
        Bind(menu, SquircleBorder.BackgroundProperty, TokenKeys.BG_ELEVATED_BRUSH);
        Bind(menu, SquircleBorder.BorderBrushProperty, TokenKeys.EMPHASIS_A12_BRUSH);
        states.Add(new GalleryState("menu 20 / item 14", menu, "concentric: inner = outer - inset"));

        var round = new SquircleBorder { Width = 80, Height = 80, CornerRadius = new CornerRadius(40), BorderThickness = new Thickness(1) };
        SquircleBorder.SetCornerShape(round, CornerShape.Round);
        Bind(round, SquircleBorder.BorderBrushProperty, TokenKeys.ACCENT_BRUSH);
        var squircle = new SquircleBorder { Width = 80, Height = 80, CornerRadius = new CornerRadius(40), BorderThickness = new Thickness(1) };
        Bind(squircle, SquircleBorder.BorderBrushProperty, TokenKeys.ACCENT2_BRUSH);
        var both = new Panel { Width = 80, Height = 80, Children = { round, squircle } };
        states.Add(new GalleryState("squircle vs round", both, "superellipse(1.5) in Accent2 over a circle in Accent (O1)"));

        return new GallerySection("shape", "Radius and shape", "DS 3: radius = min(16, round(height x 0.45)); containers 18, menus 20, wells 12; squircle corners on every self-drawn shape (O1).", states);
    }


    private static GallerySection Elevation()
    {
        Control Surface(string shadowKey, double radius, string brush)
        {
            var box = new SquircleBorder { Width = 150, Height = 70, Margin = new Thickness(0, 0, 0, 24), CornerRadius = new CornerRadius(radius), BorderThickness = new Thickness(1) };
            Bind(box, SquircleBorder.BackgroundProperty, brush);
            Bind(box, SquircleBorder.BorderBrushProperty, TokenKeys.EMPHASIS_A12_BRUSH);
            if (Application.Current?.TryFindResource(shadowKey, out var shadow) == true && shadow is BoxShadows shadows) box.BoxShadow = shadows;
            return box;
        }

        return new GallerySection("elevation", "Elevation", "DS 1.5: none on panels, buttons, chips and badges; 1 for menus; 2 for dialogs; the toast its own.",
        [
            new("0 panel", Surface("none", 18, TokenKeys.BG_PANEL_BRUSH), "hairline only"),
            new("1 menu", Surface(TokenKeys.ELEVATION1, 20, TokenKeys.BG_ELEVATED_BRUSH), "0 18 50 -12 #000 35%"),
            new("2 dialog", Surface(TokenKeys.ELEVATION2, 20, TokenKeys.BG_ELEVATED_BRUSH), "0 32 80 -20 #000 50%"),
            new("toast", Surface(TokenKeys.ELEVATION_TOAST, 16, TokenKeys.BG_TOAST_BRUSH), "0 18 44 -14 #000 40%"),
        ]);
    }


    private static GallerySection MotionSection()
    {
        (string Name, Avalonia.Animation.Easings.Easing Easing, TimeSpan Duration)[] curves =
        [
            ("Standard 160", MotionTokens.StandardEasing, MotionTokens.Hover),
            ("Emphasized 420", MotionTokens.EmphasizedEasing, MotionTokens.Panel),
            ("Spring 320", MotionTokens.SpringEasing, MotionTokens.SwitchKnob),
            ("Soft spring 420", MotionTokens.SpringSoftEasing, MotionTokens.SegmentedThumb),
            ("Child window 260", MotionTokens.ChildWindowEasing, MotionTokens.ChildWindowOpen),
            ("Quartic out 1100", MotionTokens.QuarticOutEasing, MotionTokens.CountUp),
        ];

        var states = new List<GalleryState>();
        foreach (var (name, easing, duration) in curves)
        {
            var dot = new SquircleBorder { Width = 14, Height = 14, CornerRadius = new CornerRadius(7), HorizontalAlignment = HorizontalAlignment.Left };
            SquircleBorder.SetCornerShape(dot, CornerShape.Round);
            Bind(dot, SquircleBorder.BackgroundProperty, TokenKeys.ACCENT_BRUSH);

            var track = new SquircleBorder { Width = 180, Height = 22, Padding = new Thickness(4), CornerRadius = new CornerRadius(10), Child = dot };
            Bind(track, SquircleBorder.BackgroundProperty, TokenKeys.EMPHASIS_A06_BRUSH);

            var play = new GlassButton { Icon = IconId.ArrowSync, Classes = { "small" } };
            ToolTip.SetTip(play, "Play");
            play.Click += (_, _) => _ = new Choreo().Add(duration, easing, p => dot.Margin = new Thickness(158 * p, 0, 0, 0)).PlayAsync(dot);

            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { track, play } };
            states.Add(new GalleryState(name, row, easing.ToString() ?? string.Empty));
        }

        return new GallerySection("motion", "Motion", "DS 5: hover 160, press 140/160 Emphasized, focus 120, menus 220, panels 420, springs 260-460; every change animates; no reduce-motion mode (O12).", states);
    }


    private static GallerySection Icons()
    {
        var all = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 4, LineSpacing = 4, Width = 640 };
        foreach (var id in Enum.GetValues<IconId>())
        {
            var icon = new GlassIcon { Icon = id, Width = 18, Height = 18 };
            var tile = new Border { Width = 34, Height = 34, Child = icon };
            ToolTip.SetTip(tile, id.ToString());
            all.Children.Add(tile);
        }

        var sizes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        foreach (var size in (ReadOnlySpan<double>)[12, 13, 14, 16, 18])
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            foreach (var id in (ReadOnlySpan<IconId>)[IconId.Search, IconId.Settings, IconId.Sparkle, IconId.Copy, IconId.AiWorkflow])
            {
                row.Children.Add(new GlassIcon { Icon = id, Width = size, Height = size });
            }

            sizes.Children.Add(row);
        }

        var filled = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        foreach (var id in (ReadOnlySpan<IconId>)[IconId.Pin, IconId.PinFilled, IconId.PanelRight, IconId.PanelRightFilled, IconId.CheckmarkCircleFilled, IconId.ErrorCircleFilled, IconId.InfoFilled, IconId.BrandMicrosoftStore])
        {
            filled.Children.Add(new GlassIcon { Icon = id, Width = 20, Height = 20 });
        }

        return new GallerySection("icons", "Icons (O16)", "Drawn for ExifGlass: 20-unit box, 1.5 stroke, round caps; Filled variants for on states; the Store mark is the official one. Hover a tile for its id.",
        [
            new("all " + Enum.GetValues<IconId>().Length.ToString(CultureInfo.InvariantCulture), all, "18 px"),
            new("12 · 13 · 14 · 16 · 18", sizes, "chip 14, button 16, toolbar 18, small 12-13"),
            new("regular / filled", filled, "on states and statuses"),
            new("logo 48 · 60 · 104", new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 16,
                Children =
                {
                    new ScaledImage { Source = LOGO, Width = 48, Height = 48, VerticalAlignment = VerticalAlignment.Bottom },
                    new ScaledImage { Source = LOGO, Width = 60, Height = 60, VerticalAlignment = VerticalAlignment.Bottom },
                    new ScaledImage { Source = LOGO, Width = 128, Height = 128, VerticalAlignment = VerticalAlignment.Bottom },
                },
            }, "ScaledImage picks the 1x, 2x or 3x render (gen-app-icons)"),
        ]);
    }

    #endregion // Foundations


    #region Helpers

    private static void Bind(AvaloniaObject target, AvaloniaProperty property, string key)
    {
        if (target is StyledElement element) target[!property] = element.GetResourceObservable(key).ToBinding();
    }


    private static Control GroupDot(string? group, int? ring) => new Controls.GroupDot { Group = group, RingIndex = ring };


    private static Control GroupBarSegment(string group, double width) => new Controls.GroupDot { Group = group, Width = Math.Max(2, width), Height = 6, Radius = 0 };


    // the ramp's names as DS 1.1 writes them: BodyStrong is body-strong
    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex KebabCase();

    #endregion // Helpers
}
