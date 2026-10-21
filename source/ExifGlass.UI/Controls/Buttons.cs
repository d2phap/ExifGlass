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

namespace ExifGlass.UI.Controls;

/// <summary>
/// A button with an optional leading icon (DS 6.1); classes pick the variant: primary, secondary (default), ghost, icon, danger, link, and the sizes card (30), small (28), tiny (24).
/// </summary>
public class GlassButton : Button
{
    #region Fields and constants

    public static readonly StyledProperty<IconId?> IconProperty =
        AvaloniaProperty.Register<GlassButton, IconId?>(nameof(Icon));

    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<GlassButton, double>(nameof(IconSize), 16);

    #endregion // Fields and constants


    #region Properties

    public IconId? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public double IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(GlassButton);

    #endregion // Properties


    #region Constructors

    public GlassButton()
    {
        UpdatePseudoClasses();
    }

    #endregion // Constructors


    #region Protected methods

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IconProperty || change.Property == ContentProperty) UpdatePseudoClasses();
    }

    #endregion // Protected methods


    #region Private methods

    // :icon has an icon, :label has text; an icon with no label is a square icon button
    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":icon", Icon is not null);
        PseudoClasses.Set(":label", Content is not null);
    }

    #endregion // Private methods
}


/// <summary>
/// An on/off icon button (DS 6.1): off shows the Regular icon, on the Filled one with Accent 10% and AccentText.
/// </summary>
public class GlassToggleButton : ToggleButton
{
    #region Fields and constants

    public static readonly StyledProperty<IconId?> IconProperty =
        AvaloniaProperty.Register<GlassToggleButton, IconId?>(nameof(Icon));

    public static readonly StyledProperty<IconId?> CheckedIconProperty =
        AvaloniaProperty.Register<GlassToggleButton, IconId?>(nameof(CheckedIcon));

    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<GlassToggleButton, double>(nameof(IconSize), 18);

    public static readonly DirectProperty<GlassToggleButton, IconId?> ShownIconProperty =
        AvaloniaProperty.RegisterDirect<GlassToggleButton, IconId?>(nameof(ShownIcon), b => b.ShownIcon);

    private IconId? _shownIcon;

    #endregion // Fields and constants


    #region Properties

    public IconId? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>
    /// The Filled icon shown while on; the Regular one when unset.
    /// </summary>
    public IconId? CheckedIcon
    {
        get => GetValue(CheckedIconProperty);
        set => SetValue(CheckedIconProperty, value);
    }

    public double IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public IconId? ShownIcon
    {
        get => _shownIcon;
        private set => SetAndRaise(ShownIconProperty, ref _shownIcon, value);
    }

    protected override Type StyleKeyOverride => typeof(GlassToggleButton);

    #endregion // Properties


    #region Protected methods

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IconProperty || change.Property == CheckedIconProperty || change.Property == IsCheckedProperty)
        {
            ShownIcon = IsChecked == true ? CheckedIcon ?? Icon : Icon;
        }
    }

    #endregion // Protected methods
}


/// <summary>
/// A button that opens a menu (DS 6.1): icon 16, label and a 12 px chevron at 70% that turns 180 degrees while open; the hover fill holds while open.
/// </summary>
public class GlassDropDownButton : DropDownButton
{
    #region Fields and constants

    public static readonly StyledProperty<IconId?> IconProperty =
        AvaloniaProperty.Register<GlassDropDownButton, IconId?>(nameof(Icon));

    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<GlassDropDownButton, double>(nameof(IconSize), 16);

    #endregion // Fields and constants


    #region Properties

    public IconId? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public double IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(GlassDropDownButton);

    #endregion // Properties


    #region Constructors

    public GlassDropDownButton()
    {
        UpdatePseudoClasses();
    }

    #endregion // Constructors


    #region Protected methods

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IconProperty || change.Property == ContentProperty) UpdatePseudoClasses();
    }

    #endregion // Protected methods


    #region Private methods

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":icon", Icon is not null);
        PseudoClasses.Set(":label", Content is not null);
    }

    #endregion // Private methods
}


/// <summary>
/// Where the Store badge sends the user (D11).
/// </summary>
public enum StoreBadgeKind
{
    MicrosoftStore,
    GitHubReleases,
}


/// <summary>
/// The About window's sales badge (DS 6.15): inverse fill, no shadow, radius 16, height 52, the official Store mark 24 and two lines.
/// </summary>
public class StoreBadge : Button
{
    #region Fields and constants

    public static readonly StyledProperty<StoreBadgeKind> KindProperty =
        AvaloniaProperty.Register<StoreBadge, StoreBadgeKind>(nameof(Kind));

    public static readonly DirectProperty<StoreBadge, string> CaptionProperty =
        AvaloniaProperty.RegisterDirect<StoreBadge, string>(nameof(Caption), b => b.Caption);

    public static readonly DirectProperty<StoreBadge, string> TargetProperty =
        AvaloniaProperty.RegisterDirect<StoreBadge, string>(nameof(Target), b => b.Target);

    public static readonly DirectProperty<StoreBadge, IconId> GlyphProperty =
        AvaloniaProperty.RegisterDirect<StoreBadge, IconId>(nameof(Glyph), b => b.Glyph);

    private string _caption = UiText.STORE_CAPTION;
    private string _target = UiText.STORE_TARGET;
    private IconId _glyph = IconId.BrandMicrosoftStore;

    #endregion // Fields and constants


    #region Properties

    public StoreBadgeKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public string Caption
    {
        get => _caption;
        private set => SetAndRaise(CaptionProperty, ref _caption, value);
    }

    public string Target
    {
        get => _target;
        private set => SetAndRaise(TargetProperty, ref _target, value);
    }

    public IconId Glyph
    {
        get => _glyph;
        private set => SetAndRaise(GlyphProperty, ref _glyph, value);
    }

    protected override Type StyleKeyOverride => typeof(StoreBadge);

    #endregion // Properties


    #region Protected methods

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != KindProperty) return;

        (Caption, Target, Glyph) = Kind == StoreBadgeKind.GitHubReleases
            ? (UiText.RELEASES_CAPTION, UiText.RELEASES_TARGET, IconId.ArrowDownload)
            : (UiText.STORE_CAPTION, UiText.STORE_TARGET, IconId.BrandMicrosoftStore);
    }

    #endregion // Protected methods
}
