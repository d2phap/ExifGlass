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
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExifGlass.UI.Motion;
using ExifGlass.UI.Tokens;

namespace ExifGlass.UI.Controls;

/// <summary>
/// A menu item (DS 6.8): 34 high (44 with a description), radius 14, an icon tile 30 or a checkbox, label Medium 13 with a description, a trailing hint; pressed is held 170 ms before it acts.
/// </summary>
public class GlassMenuItem : MenuItem
{
    #region Fields and constants

    public static readonly StyledProperty<IconId?> GlyphProperty =
        AvaloniaProperty.Register<GlassMenuItem, IconId?>(nameof(Glyph));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<GlassMenuItem, string?>(nameof(Description));

    public static readonly StyledProperty<string?> HintProperty =
        AvaloniaProperty.Register<GlassMenuItem, string?>(nameof(Hint));

    private bool _acting;

    #endregion // Fields and constants


    #region Properties

    public IconId? Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>
    /// Right-aligned text such as "At least one".
    /// </summary>
    public string? Hint
    {
        get => GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(GlassMenuItem);

    #endregion // Properties


    #region Protected methods

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == GlyphProperty) PseudoClasses.Set(":glyph", Glyph is not null);
        else if (change.Property == DescriptionProperty) PseudoClasses.Set(":description", !string.IsNullOrEmpty(Description));
        else if (change.Property == HintProperty) PseudoClasses.Set(":hint", !string.IsNullOrEmpty(Hint));
    }


    // DS 4: an action that closes its host shows its pressed state for 170 ms first
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_acting || HasSubMenu || !IsEffectivelyEnabled || e.InitialPressMouseButton != MouseButton.Left || !IsPointerOver)
        {
            base.OnPointerReleased(e);
            return;
        }

        e.Handled = true;
        _acting = true;
        InteractionOverlay.SetForcedState(this, InteractionState.Pressed);

        DispatcherTimer.RunOnce(() =>
        {
            _acting = false;
            InteractionOverlay.SetForcedState(this, InteractionState.None);
            RaiseEvent(new RoutedEventArgs(ClickEvent));
            if (ToggleType == MenuItemToggleType.None) this.FindLogicalAncestorOfType<MenuBase>()?.Close();
        }, MotionTokens.IsFrozen ? TimeSpan.FromMilliseconds(1) : MotionTokens.PressHold);
    }

    #endregion // Protected methods
}


/// <summary>
/// A menu's header label (DS 6.8), such as "Exports the 42 tags in view": Overline, never interactive.
/// </summary>
public class GlassMenuHeader : MenuItem
{
    #region Fields and constants

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<GlassMenuHeader, string?>(nameof(Text));

    #endregion // Fields and constants


    #region Properties

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(GlassMenuHeader);

    #endregion // Properties


    #region Constructors

    // disabled, so keyboard navigation skips it; its theme has no disabled look
    public GlassMenuHeader()
    {
        IsEnabled = false;
        Focusable = false;
    }

    #endregion // Constructors
}


/// <summary>
/// Menu motion (DS 5.2): open in 220 ms (fade, -6 px, scale 0.97 from the top-right), close in a 120 ms fade; and the placement that keeps the shadow room out of the layout.
/// </summary>
public static class MenuMotion
{
    #region Fields and constants

    private const double OPEN_RISE = -6;
    private const double OPEN_SCALE = 0.97;
    private const double GAP = 4;

    /// <summary>
    /// The transparent margin around a menu surface and its GAP_BAND: popups are windows, and elevation 1 reaches about this far.
    /// </summary>
    public static readonly Thickness SHADOW_ROOM = new(40, 20 - GAP, 40, 60 - GAP);

    /// <summary>
    /// The gap to the button, as a transparent band above and below the surface: Avalonia places the popup child inside its margin but doesn't mirror an offset when it flips a menu above.
    /// </summary>
    public static readonly Thickness GAP_BAND = new(0, GAP, 0, GAP);

    private static bool _registered;

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Plays the open motion on every menu surface as it loads; called once at startup.
    /// </summary>
    public static void Register()
    {
        if (_registered) return;

        _registered = true;
        Control.LoadedEvent.AddClassHandler<MenuFlyoutPresenter>((presenter, _) => PlayOpen(presenter));
        Control.LoadedEvent.AddClassHandler<ContextMenu>((menu, _) => PlayOpen(menu));
    }


    /// <summary>
    /// Places a flyout under its button (above when there's no room), right edges aligned, 4 px away; and fades it out on close.
    /// </summary>
    public static void Attach(PopupFlyoutBase flyout)
    {
        flyout.Placement = PlacementMode.BottomEdgeAlignedRight;

        var fading = false;
        flyout.Closing += (_, e) =>
        {
            if (fading || MotionTokens.IsFrozen || flyout is not MenuFlyout { } menu || Surface(menu) is not { } surface) return;

            fading = true;
            e.Cancel = true;
            CompositionMotion.Fade(surface, 1, 0, MotionTokens.MenuClose, MotionTokens.MenuCloseEasing);
            DispatcherTimer.RunOnce(() =>
            {
                flyout.Hide();
                fading = false;
            }, MotionTokens.MenuClose);
        };
    }

    #endregion // Public methods


    #region Private methods

    private static void PlayOpen(Control host)
    {
        if (host.GetVisualChildren().FirstOrDefault() is not Control surface) return;

        Dispatcher.UIThread.Post(() =>
        {
            CompositionMotion.Fade(surface, 0, 1, MotionTokens.MenuOpen, MotionTokens.MenuOpenEasing);
            CompositionMotion.Move(surface, new Vector(0, OPEN_RISE), default, MotionTokens.MenuOpen, MotionTokens.MenuOpenEasing);
            CompositionMotion.Scale(surface, OPEN_SCALE, 1, MotionTokens.MenuOpen, MotionTokens.MenuOpenEasing, new RelativePoint(1, 0, RelativeUnit.Relative));
        }, DispatcherPriority.Render);
    }


    // the presenter a menu flyout shows, found through its items' visual root
    private static Control? Surface(MenuFlyout flyout)
    {
        foreach (var item in flyout.Items)
        {
            if (item is Control control && control.FindAncestorOfType<MenuFlyoutPresenter>() is { } presenter) return presenter.GetVisualChildren().FirstOrDefault() as Control;
        }

        return null;
    }

    #endregion // Private methods
}
