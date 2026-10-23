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
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExifGlass.UI.Controls;
using ExifGlass.UI.Tokens;
using ExifGlass.UI.Windows;

namespace ExifGlass.UI.Gallery;

public static partial class DesignGalleryCatalog
{
    #region Fields and constants

    private const string LONG_PROMPT = "masterpiece, (a lighthouse on a basalt cliff:1.2) at blue hour, volumetric fog, long exposure, 35mm film grain BREAK <lora:film_grain_v2:0.8> dramatic clouds, (sharp focus:1.1), easynegative, cinematic lighting, ultra detailed --ar 3:2 --v 6";

    private static readonly IWindowPlacementStore GalleryPlacements = new MemoryPlacementStore();

    #endregion // Fields and constants


    #region Controls

    private static IEnumerable<GallerySection> BuildControls() =>
    [
        Buttons(), IconButtons(), Dropdowns(), StoreBadges(), Inputs(), TokenFields(), Checks(), Switches(), Segmented(), Combo(),
        Chips(), Badges(), Menus(), ToolTips(), Toasts(), Dialogs(), PushPanels(), Scrollbars(), Cards(), Feedback(),
        TextControls(), Expanders(), Windows(), Table(),
    ];


    private static GallerySection Buttons()
    {
        static Control Row(Func<GlassButton, GlassButton> state)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(state(new GlassButton { Content = "Save", Icon = IconId.Save, Classes = { "primary" } }));
            row.Children.Add(state(new GlassButton { Content = "Cancel" }));
            row.Children.Add(state(new GlassButton { Content = "Collapse all", Icon = IconId.ChevronDoubleUp, Classes = { "ghost" } }));
            row.Children.Add(state(new GlassButton { Content = "Delete", Icon = IconId.Delete, Classes = { "danger" } }));
            row.Children.Add(state(new GlassButton { Content = "Show more", Classes = { "link" } }));
            return row;
        }

        return new GallerySection("c-button", "Buttons", "DS 6.1: 32 high (30 in cards), radius 14, label Medium 12.5 that never wraps, icon 16, gap 6; primary Accent, secondary 1 px Stroke.Control, ghost transparent, danger Danger.Text; link for text actions.",
        [
            new("rest", Row(b => b)),
            new(":pointerover", Row(b => b.Hover()), "State.Hover 160 ms; accent fills White 8%"),
            new(":pressed", Row(b => b.Pressed()), "State.Pressed + scale 0.96, 140 ms Emphasized"),
            new(":focus-visible", Row(b => b.FocusVisible()), "2 px inset ring, no glow, 120 ms"),
            new(":disabled", Row(b => b.Disabled()), "opacity 0.45"),
            new("card 30 · small 28 · tiny 24", new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new GlassButton { Content = "Apply", Classes = { "primary", "card" } },
                    new GlassButton { Content = "Cancel", Classes = { "card" } },
                    new GlassButton { Content = "Copy", Icon = IconId.Copy, Classes = { "small" } },
                    new GlassButton { Content = "Clear all", Classes = { "tiny", "ghost" } },
                },
            }),
        ]);
    }


    private static GallerySection IconButtons()
    {
        static Control Row(Func<Control, Control> state)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            row.Children.Add(state(new GlassButton { Icon = IconId.FolderOpen, Classes = { "toolbar" } }));
            row.Children.Add(state(new GlassButton { Icon = IconId.Settings, Classes = { "toolbar" } }));
            row.Children.Add(state(new GlassToggleButton { Icon = IconId.Pin, CheckedIcon = IconId.PinFilled }));
            row.Children.Add(state(new GlassToggleButton { Icon = IconId.PanelRight, CheckedIcon = IconId.PanelRightFilled, IsChecked = true }));
            row.Children.Add(state(new GlassButton { Icon = IconId.Delete, Classes = { "small" } }));
            row.Children.Add(state(new GlassButton { Icon = IconId.Dismiss, Classes = { "tiny" } }));
            return row;
        }

        return new GallerySection("c-icon-button", "Icon and toggle buttons", "DS 6.1: 32 x 32 (28, 24 small), icon 18 in toolbars; toggles: Regular icon off, Filled icon + Accent 10% + AccentText on.",
        [
            new("rest", Row(c => c)),
            new(":pointerover", Row(c => c.Hover())),
            new(":pressed", Row(c => c.Pressed())),
            new(":focus-visible", Row(c => c.FocusVisible())),
            new(":disabled", Row(c => c.Disabled())),
        ]);
    }


    private static GallerySection Dropdowns()
    {
        static Control Row(Func<Control, Control> state)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            row.Children.Add(state(new GlassDropDownButton { Content = "Edit", Icon = IconId.Edit }));
            row.Children.Add(state(new GlassDropDownButton { Content = "Columns", Icon = IconId.ColumnTriple }));
            row.Children.Add(state(new GlassDropDownButton { Content = "Export", Icon = IconId.ArrowDownload, Classes = { "primary" } }));
            row.Children.Add(state(new GlassDropDownButton { Icon = IconId.ArrowDownload, Classes = { "primary" } }));
            return row;
        }

        var live = new GlassDropDownButton { Content = "Export", Icon = IconId.ArrowDownload, Classes = { "primary" } };
        var flyout = SampleMenuFlyout();
        live.Flyout = flyout;
        MenuMotion.Attach(flyout);

        return new GallerySection("c-dropdown", "Dropdown buttons", "DS 6.1: icon 16 + label + chevron 12 at 70%; open holds the hover fill and turns the chevron 180 degrees in 260 ms; the menu opens from the top-right.",
        [
            new("rest", Row(c => c)),
            new(":pointerover", Row(c => c.Hover())),
            new(":pressed", Row(c => c.Pressed())),
            new("open", Row(c => c.Open())),
            new(":disabled", Row(c => c.Disabled())),
            new("live", live, "click: the real menu with its motion"),
        ]);
    }


    private static GallerySection StoreBadges() => new("c-store", "Store badge", "DS 6.15: inverse fill, no shadow, radius 16, 52 high, the official Store mark 24; hover brightness 1.15, pressed 0.9 and scale 0.97.",
    [
        new("rest", new StoreBadge().Left()),
        new(":pointerover", new StoreBadge().Hover().Left()),
        new(":pressed", new StoreBadge().Pressed().Left()),
        new(":focus-visible", new StoreBadge().FocusVisible().Left()),
        new("GitHub Releases", new StoreBadge { Kind = StoreBadgeKind.GitHubReleases }.Left(), "macOS direct (D11)"),
    ]);


    private static GallerySection Inputs()
    {
        static SearchBox Search(string? text = null) => new() { Width = 300, PlaceholderText = "Search 128 tags", Text = text };

        var editor = new TextBox { Width = 300, Text = "FUJIFILM Corporation\nX-T5", Classes = { "mono-editor" } };

        return new GallerySection("c-input", "Text input and search", "DS 6.2: 32 high, radius 14, padding 10, Bg.Input; hover border Emphasis 24%; focus = Accent border + inset 1 px (crisp 2 px), no glow; search shows the Ctrl+K hint while empty and a clear button while not.",
        [
            new("rest", Search()),
            new(":pointerover", Search().Hover()),
            new(":focus", Search("fuji").FocusVisible(), "ring, clear button"),
            new(":disabled", new SearchBox { Width = 300, PlaceholderText = "Open a file to search" }.Disabled(), "opacity 0.6"),
            new("text box", new TextBox { Width = 300, Text = @"C:\Tools\exiftool.exe" }),
            new("mono editor", editor, "multiline value edit, mono 12.5, min 64"),
        ]);
    }


    private static GallerySection TokenFields()
    {
        var field = new TokenField { Width = 460, LockedTokens = ["-G", "-t", "-H"], Suggestions = ["-n", "-a", "-u", "-ee", "-s"] };
        foreach (var t in new[] { "-fast", "-G", "-t", "-m", "-q", "-H", "-charset", "UTF8" }) field.Tokens.Add(t);
        field.DescriptionProvider = t => t switch
        {
            "-fast" => "Skip the trailer and maker notes scan",
            "-m" => "Ignore minor errors",
            "-q" => "Quiet",
            "-charset" => "Character set of the output",
            _ => null,
        };

        var invalid = new TokenField { Width = 460, LockedTokens = ["-G"] };
        foreach (var t in new[] { "-G", "-fast", "-o", "out.txt" }) invalid.Tokens.Add(t);
        invalid.Validator = t => t == "-o" ? "Refused: -o writes files (D1)" : null;

        return new GallerySection("c-token-field", "Argument token field", "DS 6.15, D1: chips 26 high, radius 12, mono 11.5; flags Accent 10% in AccentText, values Accent2 10%; Enter or Space adds, Backspace removes the last; locked chips for app-owned flags; dashed suggestions.",
        [
            new("rest", field),
            new("refused", invalid, "a refused argument turns red; the tooltip says why"),
        ]);
    }


    private static GallerySection Checks()
    {
        static CheckBox Box(bool? on = true, string? label = null) => new() { IsChecked = on, Content = label, IsThreeState = on is null };

        return new GallerySection("c-check", "Checkbox", "DS 6.3: 16, radius 5, border Emphasis 40%; on: Accent fill, white check 12 springing in (260 ms); the 36 x 32 hit area carries the state layer.",
        [
            new("off", Box(false)),
            new("on", Box()),
            new("mixed", Box(null)),
            new(":pointerover", Box().Hover()),
            new(":pressed", Box().Pressed(), "box scale 0.96"),
            new(":focus-visible", Box().FocusVisible()),
            new(":disabled", Box().Disabled()),
            new("with label", Box(true, "Show all groups")),
        ]);
    }


    private static GallerySection Switches()
    {
        static ToggleSwitch Switch(bool on) => new() { IsChecked = on };

        return new GallerySection("c-switch", "Toggle switch", "DS 6.4: 40 x 20 round track; off 1 px Emphasis 55% with a 12 knob at 70%; on Accent with a white knob 20 px over (320 ms spring); the knob stretches to 15 while pressed; On/Off on the left.",
        [
            new("off", Switch(false)),
            new("on", Switch(true)),
            new(":pointerover", Switch(true).Hover()),
            new(":pressed off", Switch(false).Pressed(), "knob stretches"),
            new(":pressed on", Switch(true).Pressed()),
            new(":focus-visible", Switch(true).FocusVisible()),
            new(":disabled", Switch(true).Disabled()),
        ]);
    }


    private static GallerySection Segmented()
    {
        static SegmentedControl Theme(int selected)
        {
            var control = new SegmentedControl();
            control.Items.Add(new SegmentedItem { Content = "System", Icon = IconId.DarkTheme });
            control.Items.Add(new SegmentedItem { Content = "Light", Icon = IconId.WeatherSunny });
            control.Items.Add(new SegmentedItem { Content = "Dark", Icon = IconId.WeatherMoon });
            control.SelectedIndex = selected;
            return control;
        }

        var hover = Theme(0);
        if (hover.Items[2] is SegmentedItem item) item.Hover();

        return new GallerySection("c-segmented", "Segmented control", "DS 6.5: 32 high, radius 14, padding 3, Emphasis 7%; the thumb (radius 11, shadow) slides with the soft spring in 420 ms; active Medium TextPrimary, inactive TextSecondary.",
        [
            new("rest", Theme(1)),
            new("hover", hover, "inactive option: State.Hover"),
            new("live", Theme(0), "click another option"),
        ]);
    }


    private static GallerySection Combo()
    {
        static ComboBox Box()
        {
            var box = new ComboBox { Width = 200 };
            box.Items.Add(new ComboBoxItem { Content = "English" });
            box.Items.Add(new ComboBoxItem { Content = "Ti\u1EBFng Vi\u1EC7t" });
            box.SelectedIndex = 0;
            return box;
        }

        return new GallerySection("c-combo", "Combo box", "A secondary field with a chevron; the list is a menu surface (DS 6.8).",
        [
            new("rest", Box()),
            new(":pointerover", Box().Hover()),
            new("open", Box().Open()),
            new(":focus-visible", Box().FocusVisible()),
            new(":disabled", Box().Disabled()),
        ]);
    }


    private static GallerySection Chips()
    {
        static Control Row(Func<Chip, Chip> state)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            row.Children.Add(state(new Chip { Content = "Date & time", Icon = IconId.Clock, Count = "13" }));
            row.Children.Add(state(new Chip { Content = "GPS", Icon = IconId.Location, Count = "14", IsChecked = true }));
            row.Children.Add(state(new Chip { Content = "AI generation", Icon = IconId.Sparkle, Count = "23", IsChecked = true, Classes = { "ai" } }));
            return row;
        }

        var filters = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new FilterChip { Text = "GPS", Icon = IconId.Location },
                new FilterChip { Text = "\u201Cfuji\u201D", Icon = IconId.Search },
            },
        };

        return new GallerySection("c-chip", "Chips", "DS 6.6: 28 high, radius 13, padding 8/10, Stroke.Control; icon 14, label 12, mono count at 70%; on: Accent border, Accent 10%, AccentText (AI chips in Accent2). Filter chips: 24 high, radius 11, remove 18.",
        [
            new("rest", Row(c => c)),
            new(":pointerover", Row(c => c.Hover())),
            new(":pressed", Row(c => c.Pressed())),
            new(":focus-visible", Row(c => c.FocusVisible())),
            new(":disabled", Row(c => c.Disabled())),
            new("filter chips", filters, "table toolbar, removable"),
        ]);
    }


    private static GallerySection Badges()
    {
        var row = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            ItemSpacing = 6,
            LineSpacing = 6,
            Width = 520,
            Children =
            {
                new Badge { Text = "128" },
                new Badge { Text = "12 of 55" },
                new Badge { Text = "Edited", Kind = BadgeKind.Edited },
                new Badge { Text = "Delete", Kind = BadgeKind.Delete },
                new Badge { Text = "New", Kind = BadgeKind.New },
                new Badge { Text = "SD WebUI v1.10.1", Kind = BadgeKind.Ai },
                new Badge { Text = "Generated", Kind = BadgeKind.Ai, Icon = IconId.Sparkle },
                new Badge { Text = "Edited with AI", Kind = BadgeKind.Ai, Icon = IconId.AiEdited },
                new Badge { Text = "Composite", Kind = BadgeKind.Ai, Icon = IconId.AiComposite },
                new Badge { Text = "Unknown AI", Kind = BadgeKind.Neutral, Icon = IconId.AiQuestion },
                new Badge { Text = "Not verified", Kind = BadgeKind.Neutral, Icon = IconId.AiShieldQuestion },
                new Kbd { Text = UiText.Shortcut("K") },
            },
        };

        return new GallerySection("c-badge", "Badges and kbd", "DS 6.7: 18 high counts (mono, Emphasis 6%), 20 high status (Medium 10.5); AI kind chips 22 in Accent2, Unknown AI and Not verified neutral (addendum Q5, Q6); never a pill.",
        [
            new("all", row),
        ]);
    }


    private static GallerySection Menus()
    {
        var export = new MenuFlyoutPresenter
        {
            Items =
            {
                new GlassMenuHeader { Text = "EXPORTS THE 128 TAGS IN VIEW" },
                new GlassMenuItem { Header = "CSV", Description = "Spreadsheet-ready, one row per tag", Glyph = IconId.Table }.FocusVisible(),
                new GlassMenuItem { Header = "Plain text", Description = "Tab-separated, like exiftool -t", Glyph = IconId.Document }.Hover(),
                new GlassMenuItem { Header = "JSON", Description = "Compatible with exiftool -j -G", Glyph = IconId.Braces }.Pressed(),
                new Separator(),
                new GlassMenuItem { Header = "Strip all metadata", Description = "Removes every writable tag \u00B7 -all=", Glyph = IconId.Eraser, Classes = { "danger" } },
                new GlassMenuItem { Header = "Remove GPS data", Description = "No GPS data in this file", Glyph = IconId.LocationOff, IsEnabled = false },
            },
        };

        var columns = new MenuFlyoutPresenter
        {
            Items =
            {
                new GlassMenuItem { Header = "#", ToggleType = MenuItemToggleType.CheckBox },
                new GlassMenuItem { Header = "Tag ID", ToggleType = MenuItemToggleType.CheckBox, IsChecked = true }.Hover(),
                new GlassMenuItem { Header = "Tag name", ToggleType = MenuItemToggleType.CheckBox, IsChecked = true }.FocusVisible(),
                new GlassMenuItem { Header = "Value", ToggleType = MenuItemToggleType.CheckBox, IsChecked = true, Hint = "At least one" },
            },
        };

        return new GallerySection("c-menu", "Menus and flyouts", "DS 6.8: 240-280 wide, padding 6, radius 20, Bg.Elevated, 1 px Emphasis 12%, elevation 1; items 34/44, radius 14, tile 30; destructive in Danger.Text; disabled 0.45; open 220 ms from the top-right, close 120 ms; pressed held 170 ms; the arrow keys' item shows the 2 px focus ring.",
        [
            new("items", export, "keyboard focus (ring), hover, pressed (dims, scales 0.98), destructive, disabled"),
            new("checklist", columns, "Columns menu; the last visible column can't be hidden; hover and keyboard focus"),
        ]);
    }


    private static GallerySection ToolTips()
    {
        var tip = new ToolTip { Content = "Open file\u2026 \u00B7 " + UiText.Shortcut("O") };
        var live = new GlassButton { Icon = IconId.FolderOpen, Classes = { "toolbar" } };
        ToolTip.SetTip(live, "Open file\u2026 \u00B7 " + UiText.Shortcut("O"));

        return new GallerySection("c-tooltip", "Tooltip", "Names and shortcuts (DS 8, 9): Bg.Elevated, 1 px Emphasis 12%, radius 12, caption 12.",
        [
            new("static", tip),
            new("live", live, "hover the button"),
        ]);
    }


    private static GallerySection Toasts()
    {
        static Control Host(string title, string sub, ToastKind kind)
        {
            var host = new ToastHost { Width = 420, Height = 120, Lifetime = Timeout.InfiniteTimeSpan };
            host.AttachedToVisualTree += (_, _) => host.Show(title, sub, kind);
            return host;
        }

        var stage = new ToastHost { Width = 420, Height = 120 };
        var show = new GlassButton { Content = "Show toast", Icon = IconId.CheckmarkCircle };
        show.Click += (_, _) => stage.Show("Copied prompt", "412 characters");
        var live = new Panel { Children = { stage, show.Left() } };

        return new GallerySection("c-toast", "Toast", "DS 6.14: bottom centre, 62 above the bottom, 44 high, radius 16, Bg.Elevated 98%, Filled status icon 18; soft spring in 440 ms, auto-hide 2.3 s, fade 220 ms; one at a time.",
        [
            new("success", Host("Saved 3 changes", "Backup kept as DSCF4127.JPG_original", ToastKind.Success)),
            new("error", Host("Couldn\u2019t read that file", "Try a JPEG, PNG or TIFF", ToastKind.Danger)),
            new("live", live, "replaces the one on screen"),
        ]);
    }


    private static GallerySection Dialogs()
    {
        var card = new DialogCard();
        card.Apply(new DialogRequest("Save changes to DSCF4127.JPG?", "3 pending changes. Save writes the file and keeps DSCF4127.JPG_original.", "Save", "Discard", Icon: IconId.Save));

        var host = new DialogHost { Width = 520, Height = 260 };
        var open = new GlassButton { Content = "Open another file\u2026", Icon = IconId.FolderOpen, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        open.Click += async (_, _) => await host.ShowAsync(new DialogRequest("Save changes to DSCF4127.JPG?", "3 pending changes.", "Save", "Discard", Icon: IconId.Save));
        host.Content = new SquircleBorder { CornerRadius = new CornerRadius(18), Child = open };
        if (host.Content is SquircleBorder surface) Bind(surface, SquircleBorder.BackgroundProperty, TokenKeys.BG_PANEL_BRUSH);

        return new GallerySection("c-dialog", "In-window dialog (D8)", "Save / Discard / Cancel when edits are staged: menu-style surface at elevation 2, a scrim, focus kept inside, Esc cancels.",
        [
            new("card", card),
            new("live", host, "Tab stays inside; Esc cancels"),
        ]);
    }


    private static GallerySection PushPanels()
    {
        static Control Surface(Control child, double width = double.NaN)
        {
            var surface = new SquircleBorder { Width = width, Padding = new Thickness(12), CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(1), Child = child };
            Bind(surface, SquircleBorder.BackgroundProperty, TokenKeys.BG_PANEL_BRUSH);
            Bind(surface, SquircleBorder.BorderBrushProperty, TokenKeys.STROKE_HAIRLINE_BRUSH);
            return surface;
        }

        static Control Pane(string text, double width) => Surface(DesignGallery.Text(text, TypeClasses.BODY_STRONG, TokenKeys.TEXT_PRIMARY_BRUSH), width);

        var host = new PushHost { Width = 560, Height = 240, LeftPane = Pane("Rail", 180), RightPane = Pane("Inspector", 200), LeftTravel = 188, RightTravel = 208 };
        var left = new GlassButton { Content = "Rail", Icon = IconId.PanelLeft };
        var right = new GlassButton { Content = "Inspector", Icon = IconId.PanelRight };
        left.Click += (_, _) => host.IsLeftOpen = !host.IsLeftOpen;
        right.Click += (_, _) => host.IsRightOpen = !host.IsRightOpen;
        host.Content = Surface(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Children = { left, right } });

        return new GallerySection("c-push", "Push panels", "Owner at G4 (replaces DS 2.1's overlays): a pane slides in from its edge in 380 ms and pushes the content the same way, which keeps its width and is clipped; no scrim, no shadow; Esc closes; opening one closes the other.",
        [
            new("live", host),
        ]);
    }


    private static GallerySection Scrollbars()
    {
        // a viewer that never auto-hides keeps its bars expanded, which pins that state
        static Control Viewer(bool expanded = false)
        {
            var lines = new StackPanel { Spacing = 6, Margin = new Thickness(12) };
            for (var i = 0; i < 30; i++) lines.Children.Add(new Skeleton { Width = 120 - i % 4 * 18 });

            var viewer = new ScrollViewer { Width = 180, Height = 120, Content = lines, VerticalScrollBarVisibility = ScrollBarVisibility.Visible, AllowAutoHide = !expanded };
            var frame = new SquircleBorder { CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), ClipToBounds = true, Child = viewer };
            Bind(frame, SquircleBorder.BackgroundProperty, TokenKeys.BG_PANEL_BRUSH);
            Bind(frame, SquircleBorder.BorderBrushProperty, TokenKeys.STROKE_HAIRLINE_BRUSH);
            return frame;
        }

        return new GallerySection("c-scroll", "Scroll bars", "Fluent 2, as in v2 (owner 2026-10-21; replaces DS 6.16): 14 px over the content's edge; at rest a third of the 8 px thumb; over the bar it expands: the whole pill, a tinted track and arrows (100 ms); thumb darker on hover and press; wheel notches ease.",
        [
            new("rest", Viewer()),
            new("[IsExpanded]", Viewer(expanded: true), "pointer over the bar"),
            new("live", Viewer(), "hover, wheel, drag the thumb, the arrows"),
        ]);
    }


    private static GallerySection Cards()
    {
        static Card Sample(string title, params string[] classes)
        {
            var card = new Card { Width = 280, Content = new StackPanel { Spacing = 6, Children = { DesignGallery.Text(title, TypeClasses.TITLE, TokenKeys.TEXT_PRIMARY_BRUSH), DesignGallery.Text("Value well, details, actions.", TypeClasses.CAPTION, TokenKeys.TEXT_SECONDARY_BRUSH) } } };
            foreach (var c in classes) card.Classes.Add(c);
            return card;
        }

        var replay = Sample("Exposure");
        var button = new GlassButton { Content = "Replay entry", Icon = IconId.ArrowSync, Classes = { "small" } };
        button.Click += (_, _) => replay.Appear();

        return new GallerySection("c-card", "Cards", "DS 3.2, 6.11: Bg.Panel, radius 18, hairline, padding 12; selected tag: Accent 35% border + 3 px ring 8%; AI: Accent2 30% border; entry: fade + 12 px rise in 400 ms.",
        [
            new("card", Sample("Frame")),
            new(".accent", Sample("F Number", "accent"), "selected tag"),
            new(".ai", Sample("AI generation", "ai")),
            new("entry", new StackPanel { Spacing = 8, Children = { replay, button.Left() } }),
        ]);
    }


    private static GallerySection Feedback()
    {
        var skeleton = new StackPanel { Spacing = 8, Width = 220 };
        for (var i = 0; i < 4; i++) skeleton.Children.Add(new Skeleton { Width = 220 - i * 30, HorizontalAlignment = HorizontalAlignment.Left });

        var pulsing = new StackPanel { Spacing = 8, Width = 220 };
        for (var i = 0; i < 4; i++) pulsing.Children.Add(new Skeleton { Width = 220 - i * 30, HorizontalAlignment = HorizontalAlignment.Left, IsPulsing = true, Index = i });

        var countUp = new CountUpText { Classes = { TypeClasses.READOUT }, Value = 250, Format = CountUpFormat.Shutter };
        var aperture = new CountUpText { Classes = { TypeClasses.READOUT }, Value = 2.0, Format = CountUpFormat.OneDecimal, Prefix = "f/" };
        var replay = new GlassButton { Content = "Count again", Icon = IconId.ArrowSync, Classes = { "small" } };
        replay.Click += (_, _) =>
        {
            countUp.Replay();
            aperture.Replay();
        };

        return new GallerySection("c-feedback", "Loading and readouts", "DS 6.9, 5.2: skeleton Emphasis 8% radius 4, pulse 0.45-1 over 1.2 s staggered 70 ms; 2 px accent progress line; spinner 800 ms a turn; readouts count up in 1100 ms (quartic), shutter as 1/N.",
        [
            new("skeleton", skeleton),
            new("pulsing", pulsing),
            new("progress", new StackPanel { Spacing = 14, Width = 240, Children = { new ProgressLine(), new ProgressLine { IsIndeterminate = false, Value = 0.4 } } }, "indeterminate, 40%"),
            new("spinner", new SpinningIcon { Icon = IconId.SpinnerArc, Width = 18, Height = 18, IsSpinning = true }),
            new("count-up", new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20, Children = { aperture, countUp, replay } }),
        ]);
    }


    private static GallerySection TextControls()
    {
        var highlight = new HighlightTextBlock { Text = "FUJIFILM Corporation, Fujinon XF 23mm F1.4", Query = "fuji" };
        Bind(highlight, HighlightTextBlock.HighlightBrushProperty, TokenKeys.HIGHLIGHT_BRUSH);

        var prompt = new PromptText { Width = 300, Prompt = LONG_PROMPT, Embeddings = ["easynegative"], FontSize = 12.5 };
        var more = new GlassButton { Content = UiText.SHOW_MORE, Classes = { "link" } };
        more.Click += (_, _) =>
        {
            prompt.IsExpanded = !prompt.IsExpanded;
            more.Content = prompt.IsExpanded ? UiText.SHOW_LESS : UiText.SHOW_MORE;
        };

        return new GallerySection("c-text", "Highlight and prompt text", "Every search match on a rounded Highlight background (radius 3); prompts tinted: weights AccentText, LoRA tags Accent2 mono on a tint, embeddings dotted, BREAK/AND Accent2, flags mono; 4-line clamp with Show more.",
        [
            new("search highlight", highlight),
            new("prompt", new StackPanel { Spacing = 4, Children = { prompt, more.Left() } }),
        ]);
    }


    private static GallerySection Expanders()
    {
        var content = new StackPanel { Spacing = 6 };
        foreach (var line in new[] { "Hires upscale: 1.5", "Hires steps: 12", "Denoising strength: 0.45", "Clip skip: 2" }) content.Children.Add(DesignGallery.Text(line, TypeClasses.CAPTION, TokenKeys.TEXT_SECONDARY_BRUSH));

        var panel = new ExpandPanel { Child = content, Width = 260 };
        var toggle = new DisclosureButton { Content = "All parameters", Classes = { "ghost", "small" } };
        toggle.Click += (_, _) => panel.IsExpanded = toggle.IsExpanded;

        return new GallerySection("c-expand", "Expand panel", "Height 0 to content in 420 ms Emphasized, clipped while moving; fade 220 ms; closed content leaves the tab order. The disclosure chevron points right while closed and turns down (260 ms).",
        [
            new("closed", new DisclosureButton { Content = "All parameters", Classes = { "ghost", "small" } }),
            new(":expanded", new DisclosureButton { Content = "All parameters", IsExpanded = true, Classes = { "ghost", "small" } }),
            new("live", new StackPanel { Spacing = 4, Children = { toggle.Left(), panel } }),
        ]);
    }


    private static GallerySection Windows()
    {
        var open = new GlassButton { Content = "Open an owned window", Icon = IconId.Open };
        open.Click += (_, _) =>
        {
            if (TopLevel.GetTopLevel(open) is not Window owner) return;

            var window = new OwnedWindow { Title = "Settings", Height = 420, Content = new Border { Padding = new Thickness(20), Child = DesignGallery.Text("Placement is saved: move or resize, close, open again.", TypeClasses.BODY, TokenKeys.TEXT_PRIMARY_BRUSH) } };
            window.UsePlacement("GallerySettings", new Size(560, 420), GalleryPlacements, owner);
            window.Show(owner);
        };

        return new GallerySection("c-window", "Owned window and placement (O15)", "plan 5.6, S4: owned, native title bar, resizable from the spec width, Esc closes, placement saved with the frame extents; DWM animates it on Windows, the 260 ms content motion runs where the OS doesn't.",
        [
            new("live", open),
        ]);
    }

    #endregion // Controls


    #region Helpers

    private static MenuFlyout SampleMenuFlyout() => new()
    {
        Items =
        {
            new GlassMenuHeader { Text = "EXPORTS THE 128 TAGS IN VIEW" },
            new GlassMenuItem { Header = "CSV", Description = "Spreadsheet-ready, one row per tag", Glyph = IconId.Table },
            new GlassMenuItem { Header = "Plain text", Description = "Tab-separated, like exiftool -t", Glyph = IconId.Document },
            new GlassMenuItem { Header = "JSON", Description = "Compatible with exiftool -j -G", Glyph = IconId.Braces },
            new Separator(),
            new GlassMenuItem { Header = "Copy as JSON", Description = "Straight to the clipboard", Glyph = IconId.Copy },
        },
    };

    #endregion // Helpers
}
