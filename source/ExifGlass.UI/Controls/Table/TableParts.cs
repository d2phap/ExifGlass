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
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using ExifGlass.Core.Metadata;
using ExifGlass.Core.Query;
using ExifGlass.UI.Tokens;
using System.Globalization;

namespace ExifGlass.UI.Controls;

/// <summary>
/// A group header's content (DS 6.9): chevron, colour dot (the sparkle for the AI group, D22), name, count badge, description.
/// </summary>
public sealed class GroupHeaderView : Grid
{
    #region Fields and constants

    public static readonly StyledProperty<bool> IsCollapsedProperty =
        AvaloniaProperty.Register<GroupHeaderView, bool>(nameof(IsCollapsed));

    public static readonly StyledProperty<bool> ShowsDescriptionProperty =
        AvaloniaProperty.Register<GroupHeaderView, bool>(nameof(ShowsDescription), true);

    private static readonly TransformOperations OPEN = TransformOperations.Parse("rotate(0deg)");
    private static readonly TransformOperations CLOSED = TransformOperations.Parse("rotate(-90deg)");

    private readonly GlassIcon _chevron;
    private readonly GroupDot _dot;
    private readonly GlassIcon _sparkle;
    private readonly TextBlock _name;
    private readonly Badge _count;
    private readonly TextBlock _description;
    private StackPanel? _from;
    private SourceLink? _link;

    #endregion // Fields and constants


    #region Constructors

    public GroupHeaderView()
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto,*");
        ColumnSpacing = 10;
        Background = Brushes.Transparent;

        _chevron = new GlassIcon
        {
            Icon = IconId.ChevronDown,
            Width = 14,
            Height = 14,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = RelativePoint.Center,
            RenderTransform = OPEN,
        };
        _dot = new GroupDot { VerticalAlignment = VerticalAlignment.Center };
        _sparkle = new GlassIcon { Icon = IconId.Sparkle, Width = 14, Height = 14, VerticalAlignment = VerticalAlignment.Center, IsVisible = false };
        _name = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        Typography.SetRole(_name, TextRole.BodyStrong);
        _count = new Badge { Kind = BadgeKind.Count };
        _description = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Right,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Typography.SetRole(_description, TextRole.Meta);

        // set here, not by class styles: each of those would cost every icon and text block in the table (G6 finding 2)
        _chevron[!GlassIcon.ForegroundProperty] = _chevron.GetResourceObservable(TokenKeys.TEXT_SECONDARY_BRUSH).ToBinding();
        _sparkle[!GlassIcon.ForegroundProperty] = _sparkle.GetResourceObservable(TokenKeys.ACCENT2_TEXT_BRUSH).ToBinding();
        _description[!TextBlock.ForegroundProperty] = _description.GetResourceObservable(TokenKeys.TEXT_TERTIARY_BRUSH).ToBinding();

        SetColumn(_dot, 1);
        SetColumn(_sparkle, 1);
        SetColumn(_name, 2);
        SetColumn(_count, 3);
        SetColumn(_description, 4);
        Children.AddRange([_chevron, _dot, _sparkle, _name, _count, _description]);
    }

    #endregion // Constructors


    #region Properties

    public bool IsCollapsed
    {
        get => GetValue(IsCollapsedProperty);
        set => SetValue(IsCollapsedProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the description shows: hidden in a narrow table (below 460 px).
    /// </summary>
    public bool ShowsDescription
    {
        get => GetValue(ShowsDescriptionProperty);
        set => SetValue(ShowsDescriptionProperty, value);
    }

    public ViewGroup? Group { get; private set; }

    #endregion // Properties


    #region Public methods

    public void Show(ViewGroup group, bool animate = true)
    {
        Group = group;
        var ai = group.Name == GroupInfo.AI_GROUP;
        _name.Text = ai ? UiText.AI_GROUP : group.Name;
        _count.Text = CountLabel(group);
        _description.Text = group.Description;
        _dot.Group = ai ? null : group.Name;
        _dot.IsVisible = !ai;
        _sparkle.IsVisible = ai;
        ShowSource(ai ? group.SourceKey : null);

        // a recycled header takes its new group's chevron at once: only a toggle turns it
        if (!animate) _chevron.Transitions = null;
        IsCollapsed = group.IsCollapsed;
        if (!animate) _chevron.Transitions = ChevronTransitions();
    }


    /// <summary>
    /// The count badge as Proto shows it: "55", or "12 of 55" when the search or filters left fewer.
    /// </summary>
    public static string CountLabel(ViewGroup group) => group.Visible == group.Total
        ? group.Total.ToString(CultureInfo.InvariantCulture)
        : string.Create(CultureInfo.InvariantCulture, $"{group.Visible} of {group.Total}");

    #endregion // Public methods


    #region Protected methods

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _chevron.Transitions ??= ChevronTransitions();
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsCollapsedProperty) _chevron.RenderTransform = IsCollapsed ? CLOSED : OPEN;
        else if (change.Property == ShowsDescriptionProperty) ShowSource(Group is { Name: GroupInfo.AI_GROUP } group ? group.SourceKey : null);
    }

    #endregion // Protected methods


    #region Private methods

    // the AI group names the one tag most of its fields came from, in the description's place (AI addendum question 1)
    private void ShowSource(string? key)
    {
        if (key is not null && _from is null)
        {
            var from = new TextBlock { Text = UiText.FROM, VerticalAlignment = VerticalAlignment.Center };
            Typography.SetRole(from, TextRole.Meta);
            from[!TextBlock.ForegroundProperty] = from.GetResourceObservable(TokenKeys.TEXT_TERTIARY_BRUSH).ToBinding();
            _link = new SourceLink { Focusable = false };
            _from = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right, Children = { from, _link } };
            SetColumn(_from, 4);
            Children.Add(_from);
        }

        if (_link is not null) _link.SourceKey = key;
        if (_from is not null) _from.IsVisible = key is not null && ShowsDescription;
        _description.IsVisible = key is null && ShowsDescription;
    }


    private static Transitions ChevronTransitions() =>
    [
        new TransformOperationsTransition { Property = RenderTransformProperty, Duration = MotionTokens.Live(MotionTokens.GroupChevron), Easing = MotionTokens.GroupChevronEasing },
    ];

    #endregion // Private methods
}


/// <summary>
/// In the row template: marks the row (group header, selected) and draws a group header for the flat model's header items (S2).
/// </summary>
public sealed class RowChrome : Panel
{
    #region Fields and constants

    private GroupHeaderView? _header;
    private InteractionOverlay? _overlay;
    private MetadataTable? _table;

    #endregion // Fields and constants


    #region Properties

    public TableItem? Item => DataContext as TableItem;

    public bool IsPointerSnapped => _overlay?.IsSnapped == true;

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Re-reads the item's state: a selection change, or the same item kept across a view.
    /// </summary>
    public void Refresh(bool animate)
    {
        if (TemplatedParent is not Control row || Item is not { } item) return;

        if (!ReferenceEquals(_table, item.Table))
        {
            _table?.Unregister(this);
            _table = item.Table;
            _table.Register(this);
        }

        var selection = item.Table.Selection;
        row.Classes.Set("group", item.IsHeader);
        row.Classes.Set("selected", !item.IsHeader && selection?.IsSelected(item.Key) == true);
        row.Classes.Set("cursor", item.IsHeader && selection is { CurrentKey: { } key, CurrentColumn: Core.Export.TableColumns.None } && key == item.Key);
        IsVisible = item.IsHeader;
        if (item.Group is not { } group) return;

        // made with the first header this row shows, then kept as the row recycles
        if (_header is null)
        {
            _header = new GroupHeaderView { Margin = new Thickness(12, 0) };
            Children.Add(_header);
        }

        _header.ShowsDescription = !item.Table.IsNarrow;
        _header.Show(group, animate);
    }


    /// <summary>
    /// Shows the row's hover where the pointer is now (rows presenter coordinates), without a transition.
    /// </summary>
    public void SnapPointer(Point? pointer)
    {
        if (TemplatedParent is not Control row) return;

        _overlay ??= (Parent as Panel)?.Children.OfType<InteractionOverlay>().FirstOrDefault();
        _overlay?.Snap(pointer is { } at && row.IsVisible && row.Bounds.Contains(at));
    }

    #endregion // Public methods


    #region Protected methods

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Refresh(animate: false);
    }


    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TemplatedParent is InputElement row) row.PointerEntered += Row_PointerEntered;
        Refresh(animate: false);
    }


    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (TemplatedParent is InputElement row) row.PointerEntered -= Row_PointerEntered;
        _table?.Unregister(this);
        _table = null;
    }

    #endregion // Protected methods


    #region Event handlers

    // the copy button is made on the row's first hover, so most rows never carry one
    private void Row_PointerEntered(object? sender, PointerEventArgs e)
    {
        if (TemplatedParent is DataGridRow row && Item is { IsHeader: false } item) item.Table.MaterializeCopy(row);
    }

    #endregion // Event handlers
}


/// <summary>
/// The sticky group header (S2): a copy of the group that owns the top of the rows, pushed up by the next header; a click collapses that group.
/// </summary>
public sealed class StickyGroupHeader : Button
{
    #region Constructors

    public StickyGroupHeader()
    {
        Focusable = false;
        Content = View;
    }

    #endregion // Constructors


    #region Properties

    public GroupHeaderView View { get; } = new();

    #endregion // Properties
}
