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
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using ExifGlass.UI.Tokens;

namespace ExifGlass.UI.Controls;

/// <summary>
/// A quick-filter chip (DS 6.6): 28 high, radius 13, icon 14, label 12 and a mono count at 70%; on shows Accent border, Accent 10% and AccentText.
/// </summary>
public class Chip : ToggleButton
{
    #region Fields and constants

    public static readonly StyledProperty<IconId?> IconProperty =
        AvaloniaProperty.Register<Chip, IconId?>(nameof(Icon));

    public static readonly StyledProperty<string?> CountProperty =
        AvaloniaProperty.Register<Chip, string?>(nameof(Count));

    #endregion // Fields and constants


    #region Properties

    public IconId? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? Count
    {
        get => GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(Chip);

    #endregion // Properties


    #region Protected methods

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IconProperty) PseudoClasses.Set(":icon", Icon is not null);
        else if (change.Property == CountProperty) PseudoClasses.Set(":count", !string.IsNullOrEmpty(Count));
    }

    #endregion // Protected methods
}


/// <summary>
/// An active filter in the table toolbar (DS 6.6): 24 high, radius 11, accent tint, a trailing remove button (18).
/// </summary>
public class FilterChip : TemplatedControl
{
    #region Fields and constants

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<FilterChip, string?>(nameof(Text));

    public static readonly StyledProperty<IconId?> IconProperty =
        AvaloniaProperty.Register<FilterChip, IconId?>(nameof(Icon));

    public static readonly RoutedEvent<RoutedEventArgs> RemovedEvent =
        RoutedEvent.Register<FilterChip, RoutedEventArgs>(nameof(Removed), RoutingStrategies.Bubble);

    private Button? _remove;

    #endregion // Fields and constants


    #region Properties

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public IconId? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    #endregion // Properties


    #region Events

    public event EventHandler<RoutedEventArgs>? Removed
    {
        add => AddHandler(RemovedEvent, value);
        remove => RemoveHandler(RemovedEvent, value);
    }

    #endregion // Events


    #region Protected methods

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_remove is not null) _remove.Click -= Remove_Click;

        base.OnApplyTemplate(e);

        _remove = e.NameScope.Find<Button>("PART_Remove");
        if (_remove is not null) _remove.Click += Remove_Click;
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IconProperty) PseudoClasses.Set(":icon", Icon is not null);
    }

    #endregion // Protected methods


    #region Event handlers

    private void Remove_Click(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(RemovedEvent, this));

    #endregion // Event handlers
}


/// <summary>
/// What a badge says (DS 6.7, AI addendum): a count, a staged edit, or an AI generator or kind.
/// </summary>
public enum BadgeKind
{
    Count,
    Edited,
    Delete,
    New,
    Ai,
    Neutral,

    /// <summary>
    /// A credit's licence in About: mono 11 on Emphasis 7% (FewerPhotos).
    /// </summary>
    Licence,
}


/// <summary>
/// A badge (DS 6.7): 18 high for counts (mono, Emphasis 6%), 20 for status (Medium 10.5); never a pill.
/// </summary>
public class Badge : TemplatedControl
{
    #region Fields and constants

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<Badge, string?>(nameof(Text));

    public static readonly StyledProperty<BadgeKind> KindProperty =
        AvaloniaProperty.Register<Badge, BadgeKind>(nameof(Kind));

    public static readonly StyledProperty<IconId?> IconProperty =
        AvaloniaProperty.Register<Badge, IconId?>(nameof(Icon));

    #endregion // Fields and constants


    #region Properties

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public BadgeKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public IconId? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    #endregion // Properties


    #region Constructors

    public Badge()
    {
        UpdatePseudoClasses();
    }

    #endregion // Constructors


    #region Protected methods

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == KindProperty || change.Property == IconProperty) UpdatePseudoClasses();
    }

    #endregion // Protected methods


    #region Private methods

    private void UpdatePseudoClasses()
    {
        var kind = Kind;
        PseudoClasses.Set(":count", kind == BadgeKind.Count);
        PseudoClasses.Set(":edited", kind == BadgeKind.Edited);
        PseudoClasses.Set(":delete", kind == BadgeKind.Delete);
        PseudoClasses.Set(":new", kind == BadgeKind.New);
        PseudoClasses.Set(":ai", kind == BadgeKind.Ai);
        PseudoClasses.Set(":neutral", kind == BadgeKind.Neutral);
        PseudoClasses.Set(":licence", kind == BadgeKind.Licence);
        PseudoClasses.Set(":icon", Icon is not null);
    }

    #endregion // Private methods
}


/// <summary>
/// A keyboard hint (DS 6.2): mono 10.5, Emphasis 6%, radius 8, 20 high.
/// </summary>
public class Kbd : TemplatedControl
{
    #region Fields and constants

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<Kbd, string?>(nameof(Text));

    #endregion // Fields and constants


    #region Properties

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    #endregion // Properties
}


/// <summary>
/// A group's colour dot (plan 5.2): 8 px, round, in the group's colour for the current theme; Width, Height and Radius make a composition bar segment.
/// </summary>
public class GroupDot : Control
{
    #region Fields and constants

    public static readonly StyledProperty<string?> GroupProperty =
        AvaloniaProperty.Register<GroupDot, string?>(nameof(Group));

    public static readonly StyledProperty<int?> RingIndexProperty =
        AvaloniaProperty.Register<GroupDot, int?>(nameof(RingIndex));

    public static readonly StyledProperty<double> RadiusProperty =
        AvaloniaProperty.Register<GroupDot, double>(nameof(Radius), 4);

    #endregion // Fields and constants


    #region Properties

    public string? Group
    {
        get => GetValue(GroupProperty);
        set => SetValue(GroupProperty, value);
    }

    /// <summary>
    /// Gets or sets a ring colour to draw instead of a group's; the gallery shows the ring this way.
    /// </summary>
    public int? RingIndex
    {
        get => GetValue(RingIndexProperty);
        set => SetValue(RingIndexProperty, value);
    }

    public double Radius
    {
        get => GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    #endregion // Properties


    #region Constructors

    static GroupDot()
    {
        AffectsRender<GroupDot>(GroupProperty, RingIndexProperty, RadiusProperty);
        WidthProperty.OverrideDefaultValue<GroupDot>(8);
        HeightProperty.OverrideDefaultValue<GroupDot>(8);
    }


    public GroupDot()
    {
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    #endregion // Constructors


    #region Public methods

    public override void Render(DrawingContext context)
    {
        var dark = ActualThemeVariant == ThemeVariant.Dark;
        var color = Group is { } group ? GroupPalette.Get(group, dark) : GroupPalette.GetRing(RingIndex ?? 0, dark);
        context.DrawRectangle(new ImmutableSolidColorBrush(color), null, new RoundedRect(new Rect(Bounds.Size), Radius));
    }

    #endregion // Public methods
}
