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
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using ExifGlass.UI.Controls;
using ExifGlass.UI.Motion;
using ExifGlass.UI.Tokens;
using ExifGlass.UI.Windows;
using Xunit;

namespace ExifGlass.UI.Tests;

public class ControlTests
{
    #region Motion and shape

    [Fact]
    public void CubicBezierMatchesCss()
    {
        var linear = new CubicBezierEasing(0, 0, 1, 1);
        Assert.Equal(0.5, linear.Ease(0.5), 4);

        // CSS ease at half time (WebKit UnitBezier)
        Assert.Equal(0.8024, new CubicBezierEasing(0.25, 0.1, 0.25, 1).Ease(0.5), 3);

        // the spring overshoots past 1 before it settles
        var spring = (CubicBezierEasing)MotionTokens.SpringEasing;
        Assert.True(Enumerable.Range(1, 99).Max(i => spring.Ease(i / 100.0)) > 1.05);
    }


    [Fact]
    public void ChoreoSeeksEveryTrack()
    {
        double a = -1, b = -1;
        var choreo = new Choreo()
            .Add(TimeSpan.FromMilliseconds(100), new CubicBezierEasing(0, 0, 1, 1), p => a = p)
            .Add(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100), new CubicBezierEasing(0, 0, 1, 1), p => b = p);

        choreo.Seek(TimeSpan.FromMilliseconds(50));
        Assert.Equal(0.5, a, 3);
        Assert.Equal(0, b);

        choreo.Seek(TimeSpan.FromMilliseconds(200));
        Assert.Equal(1, a);
        Assert.Equal(1, b);
        Assert.Equal(TimeSpan.FromMilliseconds(200), choreo.Duration);
    }


    [Fact]
    public void SquircleRadiiFitTheBoxAsCssDoes()
    {
        var radii = Squircle.Radii(new Rect(0, 0, 40, 20), new CornerRadius(16));
        Assert.Equal(10, radii.TopLeft.Width, 6);

        // the holding round rect touches the squircle at its apex, so it is smaller than the radius
        var holding = Squircle.RoundHolding(14, CornerShape.Squircle);
        Assert.InRange(holding, 14 * 0.5, 14 * 0.9);
        Assert.Equal(14, Squircle.RoundHolding(14, CornerShape.Round));
    }


    [Fact]
    public void GroupColoursKeepThreeToOneAndAreStable()
    {
        foreach (var dark in new[] { false, true })
        {
            var panel = dark ? Color.Parse("#282E31") : Color.Parse("#FFFFFF");
            foreach (var group in GroupPalette.NamedGroups) Assert.True(Contrast(GroupPalette.Get(group, dark), panel) >= 2.995, $"{group} {(dark ? "dark" : "light")}");
            for (var i = 0; i < GroupPalette.RingSize; i++) Assert.True(Contrast(GroupPalette.GetRing(i, dark), panel) >= 2.995, $"ring {i}");
        }

        Assert.Equal(GroupPalette.RingIndex("Kodak"), GroupPalette.RingIndex("KODAK"));
        Assert.Equal(GroupPalette.Get("exif", true), GroupPalette.Get("EXIF", true));
    }

    #endregion // Motion and shape


    #region Controls

    [Fact]
    public void ReadoutsFormatAsTheInspectorShowsThem()
    {
        Assert.Equal("1/250", CountUpText.FormatValue(250, CountUpFormat.Shutter));
        Assert.Equal("1/1", CountUpText.FormatValue(0, CountUpFormat.Shutter));
        Assert.Equal("2.0", CountUpText.FormatValue(2, CountUpFormat.OneDecimal));
        Assert.Equal("12,800", CountUpText.FormatValue(12800, CountUpFormat.Integer));
    }


    [Fact]
    public void HighlightFindsEveryMatchIgnoringCase()
    {
        Assert.Equal([0, 4, 22, 4], HighlightTextBlock.FindRanges("FUJIFILM Corporation, Fujinon", "fuji")!);
        Assert.Null(HighlightTextBlock.FindRanges("Canon", "fuji"));
        Assert.Null(HighlightTextBlock.FindRanges("Canon", ""));
    }


    [AvaloniaFact]
    public void TokenFieldAddsAndRemovesWithTheKeyboard()
    {
        var field = new TokenField { Width = 400, LockedTokens = ["-G"] };
        field.Tokens.Add("-G");
        var window = Show(field);

        var input = Find<TextBox>(field, "PART_Input");
        input.Focus();
        window.KeyTextInput("-fast");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.Equal(["-G", "-fast"], field.Tokens);

        window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        Assert.Equal(["-G"], field.Tokens);

        field.Add("-charset UTF8");
        Assert.Equal(["-G", "-charset", "UTF8"], field.Tokens);
        window.Close();
    }


    [AvaloniaFact]
    public void ToggleButtonsSwapToTheirFilledIcon()
    {
        var pin = new GlassToggleButton { Icon = IconId.Pin, CheckedIcon = IconId.PinFilled };
        var window = Show(pin);

        Assert.Equal(IconId.Pin, pin.ShownIcon);
        Click(window, pin);
        Assert.True(pin.IsChecked);
        Assert.Equal(IconId.PinFilled, pin.ShownIcon);
        window.Close();
    }


    [AvaloniaTheory]
    [InlineData("")]
    [InlineData("small")]
    [InlineData("tiny")]
    [InlineData("card")]
    [InlineData("ghost")]
    [InlineData("ghost tiny")]
    [InlineData("toolbar")]
    [InlineData("link")]
    public void IconOnlyButtonsCentreTheirIcon(string classes)
    {
        var button = new GlassButton { Icon = IconId.Dismiss, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        foreach (var c in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries)) button.Classes.Add(c);
        var window = Show(button);

        var icon = Find<GlassIcon>(button, "PART_Icon");
        var centre = icon.TranslatePoint(new Point(icon.Bounds.Width / 2, icon.Bounds.Height / 2), button) ?? default;
        Assert.Equal(button.Bounds.Width / 2, centre.X, 0.5);
        Assert.Equal(button.Bounds.Height / 2, centre.Y, 0.5);
        window.Close();
    }


    [AvaloniaFact]
    public void TheSwitchTogglesWithOurTemplate()
    {
        var toggle = new ToggleSwitch();
        var window = Show(toggle);

        Click(window, toggle);
        Assert.True(toggle.IsChecked);
        Assert.NotNull(Find<Panel>(toggle, "PART_MovingKnobs"));
        window.Close();
    }


    [AvaloniaFact]
    public void SearchBoxEscClearsThenLetsGo()
    {
        var search = new SearchBox { Width = 300, Text = "fuji" };
        var window = Show(search);
        search.Focus();

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Assert.Equal(string.Empty, search.Text);
        Assert.True(search.IsFocused);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Assert.False(search.IsFocused);
        window.Close();
    }


    [AvaloniaFact]
    public void TheStateLayerFollowsAForcedState()
    {
        MotionTokens.IsFrozen = true;
        try
        {
            var button = new GlassButton { Content = "Save" };
            var window = Show(button);
            var overlay = Find<InteractionOverlay>(button, "PART_Overlay");

            Assert.Same(overlay.RestBrush, overlay.Background);
            InteractionOverlay.SetForcedState(button, InteractionState.Hover);
            Assert.Same(overlay.HoverBrush, overlay.Background);
            InteractionOverlay.SetForcedState(button, InteractionState.Pressed);
            Assert.Same(overlay.PressBrush, overlay.Background);
            window.Close();
        }
        finally
        {
            MotionTokens.IsFrozen = false;
        }
    }


    [AvaloniaFact]
    public async Task TheDialogAnswersAndEscCancels()
    {
        var host = new DialogHost { Content = new Border() };
        var window = Show(host, 600, 400);

        var first = host.ShowAsync(new DialogRequest("Save changes?", null, "Save", "Discard"));
        Dispatcher.UIThread.RunJobs();
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Assert.Equal(DialogAnswer.Cancel, await first);

        var second = host.ShowAsync(new DialogRequest("Save changes?", null, "Save", "Discard"));
        Dispatcher.UIThread.RunJobs();
        Click(window, Find<Button>(Find<DialogCard>(host, "PART_Card"), "PART_Primary"));
        Assert.Equal(DialogAnswer.Primary, await second);
        Assert.False(host.IsOpen);
        window.Close();
    }


    [AvaloniaFact]
    public void OnePushPaneAtATimeAndEscClosesIt()
    {
        var inside = new Button { Content = "Open" };
        var host = new PushHost { LeftPane = new Border { Width = 200 }, RightPane = new Border { Width = 200 }, LeftTravel = 208, RightTravel = 208, Content = inside };
        var window = Show(host, 800, 400);

        host.IsLeftOpen = true;
        Assert.Equal(208, host.Offset);
        host.IsRightOpen = true;
        Assert.False(host.IsLeftOpen);
        Assert.Equal(-208, host.Offset);

        // the content keeps its width and moves; the host clips it instead of scrolling
        Assert.Equal(800, inside.Bounds.Width);
        Assert.Equal(-208, inside.TranslatePoint(default, window)!.Value.X);

        inside.Focus();
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Assert.False(host.IsRightOpen);
        Assert.Equal(0, host.Offset);
        window.Close();
    }


    [AvaloniaFact]
    public void ADockedPanelSlidesWithTheColumnEdgeOnTheTableSide()
    {
        var rail = new Border { Width = 236, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var inspector = new Border { Width = 304, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };

        // two columns caught 100 px open, as on one frame of the glide
        var left = new DockSlot { Anchor = Avalonia.Layout.HorizontalAlignment.Right, OpenWidth = 244, Width = 100, IsOpen = true, Child = new Panel { Children = { rail } } };
        var right = new DockSlot { Anchor = Avalonia.Layout.HorizontalAlignment.Left, OpenWidth = 312, Width = 100, IsOpen = true, Child = new Panel { Children = { inspector } } };
        Grid.SetColumn(right, 2);
        var window = Show(new Grid { ColumnDefinitions = new("Auto,*,Auto"), Children = { left, right } }, 800, 400);

        // each panel keeps its 8 px gap to the table; the rest is still past the column's outer edge
        Assert.Equal(100 - 8, rail.TranslatePoint(new Point(236, 0), left)!.Value.X);
        Assert.Equal(8, inspector.TranslatePoint(default, right)!.Value.X);
        window.Close();
    }


    [AvaloniaFact]
    public void AMenuItemActsOnReleaseAndCloses()
    {
        MotionTokens.IsFrozen = true;
        try
        {
            var clicked = 0;
            var item = new GlassMenuItem { Header = "CSV" };
            item.Click += (_, _) => clicked++;
            var toggle = new GlassMenuItem { Header = "Tag ID", ToggleType = MenuItemToggleType.CheckBox };
            var menu = new ContextMenu { Items = { toggle, item } };
            var target = new Border { Width = 300, Height = 200, Background = Brushes.Transparent, ContextMenu = menu };
            var window = Show(target, 600, 400);

            menu.Open(target);
            Dispatcher.UIThread.RunJobs();
            Click(window, toggle);
            Assert.True(toggle.IsChecked);
            Assert.True(menu.IsOpen);

            // no hold before the action, as a ComboBox row
            Click(window, item);
            Assert.Equal(1, clicked);
            Wait(30);
            Assert.False(menu.IsOpen);
            window.Close();
        }
        finally
        {
            MotionTokens.IsFrozen = false;
        }
    }


    [AvaloniaFact]
    public void AMenuOpensFromTheCornerNearestItsButton()
    {
        MotionTokens.IsFrozen = true;
        try
        {
            var top = new GlassButton { Content = "Top", Margin = new Thickness(300, 20, 0, 0), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
            var bottom = new GlassButton { Content = "Bottom", Margin = new Thickness(300, 0, 0, 20), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
            var window = Show(new Panel { Children = { top, bottom } }, 600, 500);

            foreach (var (button, above) in new[] { (top, false), (bottom, true) })
            {
                var flyout = new MenuFlyout();
                for (var i = 0; i < 4; i++) flyout.Items.Add(new GlassMenuItem { Header = "Item", Description = "A line" });
                MenuMotion.Attach(flyout);
                flyout.ShowAt(button);
                Wait(50);

                var presenter = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<MenuFlyoutPresenter>().First();
                var (origin, rise) = MenuMotion.OpenFrom(presenter, Find<SquircleBorder>(presenter, "PART_Surface"));
                Assert.Equal(new RelativePoint(1, above ? 1 : 0, RelativeUnit.Relative), origin);
                Assert.True(above ? rise > 0 : rise < 0, $"{button.Content}: rise {rise}");
                flyout.Hide();
                Wait(50);
            }

            window.Close();
        }
        finally
        {
            MotionTokens.IsFrozen = false;
        }
    }


    [AvaloniaFact]
    public void AMenuItemShowsTheFocusRingOnlyAfterAKey()
    {
        MotionTokens.IsFrozen = true;
        try
        {
            var first = new GlassMenuItem { Header = "CSV" };
            var second = new GlassMenuItem { Header = "JSON" };
            var menu = new ContextMenu { Items = { first, second } };
            var target = new Border { Width = 300, Height = 200, Background = Brushes.Transparent, ContextMenu = menu };
            var window = Show(target, 600, 400);

            window.MouseMove(new Point(150, 100));
            menu.Open(target);
            Dispatcher.UIThread.RunJobs();
            var opened = new[] { first, second }.FirstOrDefault(i => i.IsFocused);
            Assert.False(opened?.Classes.Contains(FocusCue.KEYBOARD_FOCUS) ?? false, "a menu opened by the pointer shows no ring");

            (TopLevel.GetTopLevel(first) as Window ?? window).KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            var focused = new[] { first, second }.Single(i => i.IsFocused);
            Assert.Contains(FocusCue.KEYBOARD_FOCUS, focused.Classes);
            Wait(200);
            Assert.Equal(1, Find<SquircleBorder>(focused, "PART_FocusRing").Opacity);

            window.MouseMove(new Point(10, 10));
            Dispatcher.UIThread.RunJobs();
            Assert.DoesNotContain(FocusCue.KEYBOARD_FOCUS, focused.Classes);
            menu.Close();
            window.Close();
        }
        finally
        {
            MotionTokens.IsFrozen = false;
        }
    }


    // Avalonia doesn't mirror a popup offset when it flips a popup above, so the gap is a band inside the shadow room
    [AvaloniaFact]
    public void MenusAndListsKeepTheirGapBelowAndAbove()
    {
        MotionTokens.IsFrozen = true;
        try
        {
            var top = new GlassButton { Content = "Top", Margin = new Thickness(300, 20, 0, 0), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
            var bottom = new GlassButton { Content = "Bottom", Margin = new Thickness(300, 0, 0, 20), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
            var comboTop = new ComboBox { ItemsSource = new[] { "English", "Tiếng Việt" }, SelectedIndex = 0, Margin = new Thickness(40, 20, 0, 0), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
            var comboBottom = new ComboBox { ItemsSource = new[] { "English", "Tiếng Việt" }, SelectedIndex = 0, Margin = new Thickness(40, 0, 0, 20), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
            var window = Show(new Panel { Children = { top, bottom, comboTop, comboBottom } }, 600, 500);

            foreach (var (button, below) in new[] { (top, true), (bottom, false) })
            {
                var flyout = new MenuFlyout();
                for (var i = 0; i < 4; i++) flyout.Items.Add(new GlassMenuItem { Header = "Item", Description = "A line" });
                MenuMotion.Attach(flyout);
                flyout.ShowAt(button);
                Wait(50);

                var presenter = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<MenuFlyoutPresenter>().First();
                var surface = Screen(Find<SquircleBorder>(presenter, "PART_Surface"));
                var anchor = Screen(button);
                Assert.Equal(4, below ? surface.Top - anchor.Bottom : anchor.Top - surface.Bottom, 0.5);
                Assert.Equal(anchor.Right, surface.Right, 0.5);
                flyout.Hide();
                Wait(50);
            }

            foreach (var (combo, below) in new[] { (comboTop, true), (comboBottom, false) })
            {
                combo.IsDropDownOpen = true;
                Wait(50);

                var popup = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(combo).OfType<Avalonia.Controls.Primitives.Popup>().First();
                var list = Screen(Find<SquircleBorder>((Control)popup.Child!, "PART_ListSurface"));
                var box = Screen(combo);
                Assert.Equal(2, below ? list.Top - box.Bottom : box.Top - list.Bottom, 0.5);
                Assert.Equal(box.Left, list.Left, 0.5);
                combo.IsDropDownOpen = false;
                Wait(50);
            }

            window.Close();
        }
        finally
        {
            MotionTokens.IsFrozen = false;
        }
    }


    [AvaloniaFact]
    public void EveryIconHasItsGeometry()
    {
        var host = new Border();
        var window = Show(host);
        foreach (var id in Enum.GetValues<IconId>())
        {
            var hasStroke = host.TryFindResource($"Icon.{id}.Stroke", out var stroke) && stroke is Geometry;
            var hasFill = host.TryFindResource($"Icon.{id}.Fill", out var fill) && fill is Geometry;
            Assert.True(hasStroke || hasFill, id.ToString());
        }

        window.Close();
    }


    [AvaloniaFact]
    public void OwnedWindowsCloseOnEscAndRememberTheirPlace()
    {
        MotionTokens.IsFrozen = true;
        try
        {
            var owner = Show(new Border(), 1240, 820);
            var store = new MemoryPlacementStore();
            var child = new OwnedWindow { Content = new Border() };
            child.UsePlacement("Settings", new Size(560, 420), store, owner);
            child.Show(owner);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(560, child.MinWidth);
            child.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Wait(30);

            Assert.False(child.IsVisible);
            Assert.Equal(560, store.Get("Settings")!.Width);
            owner.Close();
        }
        finally
        {
            MotionTokens.IsFrozen = false;
        }
    }

    #endregion // Controls


    #region Private methods

    private static Window Show(Control content, double width = 500, double height = 300)
    {
        var window = new Window { Width = width, Height = height, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }


    private static Rect Screen(Visual visual) => new(visual.PointToScreen(default).ToPoint(1), visual.Bounds.Size);


    private static void Click(Window window, Control target)
    {
        var centre = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window) ?? default;
        var root = TopLevel.GetTopLevel(target) as Window ?? window;
        if (!ReferenceEquals(root, window)) centre = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), root) ?? default;

        root.MouseMove(centre);
        root.MouseDown(centre, MouseButton.Left);
        root.MouseUp(centre, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }


    private static void Wait(int milliseconds)
    {
        var frame = new DispatcherFrame();
        DispatcherTimer.RunOnce(() => frame.Continue = false, TimeSpan.FromMilliseconds(milliseconds));
        Dispatcher.UIThread.PushFrame(frame);
    }


    private static T Find<T>(Control templated, string name) where T : Control
    {
        foreach (var visual in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(templated))
        {
            if (visual is T match && match.Name == name) return match;
        }

        throw new InvalidOperationException($"{name} not found");
    }


    private static double Contrast(Color a, Color b)
    {
        static double Lin(double v) => v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        static double Lum(Color c) => 0.2126 * Lin(c.R / 255.0) + 0.7152 * Lin(c.G / 255.0) + 0.0722 * Lin(c.B / 255.0);
        var (la, lb) = (Lum(a), Lum(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    #endregion // Private methods
}
