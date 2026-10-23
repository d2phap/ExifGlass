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
using Avalonia.Automation;
using Avalonia.Layout;
using Avalonia.Media;
using ExifGlass.Core.Editing;
using ExifGlass.Core.Export;
using ExifGlass.Core.Metadata;
using ExifGlass.UI.Tokens;
using System.Globalization;

namespace ExifGlass.UI.Controls;

// ProDataGrid re-runs a recycling factory on scroll but not after a Reset, where it only swaps the DataContext: each cell updates itself from it (S2)

/// <summary>
/// The # cell: ExifTool's order, "+" for a staged new tag, "—" for an AI field.
/// </summary>
public sealed class IndexCell : TextBlock
{
    #region Fields and constants

    private static string[] _numbers = [];

    #endregion // Fields and constants


    #region Constructors

    public IndexCell()
    {
        Typography.SetRole(this, TextRole.MonoSmall);
        VerticalAlignment = VerticalAlignment.Center;
        TextTrimming = TextTrimming.CharacterEllipsis;
    }

    #endregion // Constructors


    #region Public methods

    public void Update(TableItem item)
    {
        Text = item.Row switch
        {
            null => string.Empty,
            { EditState: EditState.Added } => "+",
            { Index: > 0 } row => Number(row.Index),
            _ => TagIdRule.NONE,
        };
    }

    #endregion // Public methods


    #region Protected methods

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is TableItem item) Update(item);
    }

    #endregion // Protected methods


    #region Private methods

    // one string per number for the life of the app: scrolling recycles cells and must not allocate (plan 8)
    private static string Number(int n)
    {
        if (n >= _numbers.Length)
        {
            var grown = new string[Math.Max(n + 1, _numbers.Length * 2)];
            _numbers.CopyTo(grown, 0);
            _numbers = grown;
        }

        return _numbers[n] ??= n.ToString(CultureInfo.InvariantCulture);
    }

    #endregion // Private methods
}


/// <summary>
/// The Tag ID cell: mono 11.5, one line.
/// </summary>
public sealed class IdCell : HighlightTextBlock
{
    #region Fields and constants

    private bool? _ai;

    #endregion // Fields and constants


    #region Constructors

    public IdCell()
    {
        Typography.SetRole(this, TextRole.Mono);
        IdCell.BindHighlight(this);
        VerticalAlignment = VerticalAlignment.Center;
        TextTrimming = TextTrimming.CharacterEllipsis;
    }

    #endregion // Constructors


    #region Public methods

    // HighlightTextBlock styles as TextBlock, so a selector on its own type never matches: the cells take the brush here
    internal static void BindHighlight(HighlightTextBlock text) => text[!HighlightBrushProperty] = text.GetResourceObservable(TokenKeys.HIGHLIGHT_BRUSH).ToBinding();


    public void Update(TableItem item)
    {
        SetText(item.TagId, item.Row?.IdMatches);

        // AI fields in Accent2 (Proto); rebound only when a recycled cell changes side
        var ai = item.Row?.IsAi == true;
        if (_ai == ai) return;

        _ai = ai;
        this[!ForegroundProperty] = this.GetResourceObservable(ai ? TokenKeys.ACCENT2_TEXT_BRUSH : TokenKeys.TEXT_SECONDARY_BRUSH).ToBinding();
    }

    #endregion // Public methods


    #region Protected methods

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is TableItem item) Update(item);
    }

    #endregion // Protected methods
}


/// <summary>
/// The Tag name cell: one line with an ellipsis, the whole name in its tooltip (Proto).
/// </summary>
public sealed class NameCell : HighlightTextBlock
{
    #region Constructors

    public NameCell()
    {
        Typography.SetRole(this, TextRole.Body);
        IdCell.BindHighlight(this);
        this[!ForegroundProperty] = this.GetResourceObservable(TokenKeys.TEXT_SECONDARY_BRUSH).ToBinding();
        VerticalAlignment = VerticalAlignment.Center;
        TextTrimming = TextTrimming.CharacterEllipsis;
    }

    #endregion // Constructors


    #region Public methods

    public void Update(TableItem item)
    {
        var name = item.TagName;
        SetText(name, item.Row?.NameMatches);
        if (!ReferenceEquals(ToolTip.GetTip(this), name)) ToolTip.SetTip(this, name.Length > 0 ? name : null);
    }

    #endregion // Public methods


    #region Protected methods

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is TableItem item) Update(item);
    }

    #endregion // Protected methods
}


/// <summary>
/// The Value cell: wrapped and clamped at 6 lines (Appendix B item 7), with the staged-edit badge after its first line (S2 fallback 5).
/// </summary>
public sealed class ValueCell : Panel
{
    #region Fields and constants

    private const int MAX_LINES = 6;

    private readonly HighlightTextBlock _text;
    private Badge? _badge;

    #endregion // Fields and constants


    #region Constructors

    public ValueCell()
    {
        _text = new HighlightTextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = MAX_LINES,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Typography.SetRole(_text, TextRole.Body);
        IdCell.BindHighlight(_text);
        Children.Add(_text);
    }

    #endregion // Constructors


    #region Properties

    public HighlightTextBlock Text => _text;

    #endregion // Properties


    #region Public methods

    public void Update(TableItem item)
    {
        var row = item.Row;
        var state = row?.EditState ?? EditState.None;
        _text.SetText(item.Value, row?.ValueMatches);
        Classes.Set("edited", state is EditState.Edited or EditState.Added);
        Classes.Set("deleted", state == EditState.Deleted);

        if (state == EditState.None)
        {
            if (_badge is not null) _badge.IsVisible = false;
        }
        else
        {
            if (_badge is null)
            {
                _badge = new Badge { Classes = { "table-badge" }, VerticalAlignment = VerticalAlignment.Top };
                Children.Add(_badge);
            }

            _badge.IsVisible = true;
            (_badge.Kind, _badge.Text) = state switch
            {
                EditState.Edited => (BadgeKind.Edited, UiText.BADGE_EDITED),
                EditState.Deleted => (BadgeKind.Delete, UiText.BADGE_DELETE),
                _ => (BadgeKind.New, UiText.BADGE_NEW),
            };
        }

    }

    #endregion // Public methods


    #region Protected methods

    protected override Size MeasureOverride(Size availableSize)
    {
        var badge = _badge is { IsVisible: true } ? _badge : null;
        badge?.Measure(availableSize);
        var reserve = badge is null ? 0 : badge.DesiredSize.Width + badge.Margin.Left;
        _text.Measure(availableSize.WithWidth(Math.Max(0, availableSize.Width - reserve)));

        return new Size(_text.DesiredSize.Width + reserve, Math.Max(_text.DesiredSize.Height, badge?.DesiredSize.Height ?? 0));
    }


    protected override Size ArrangeOverride(Size finalSize)
    {
        var badge = _badge is { IsVisible: true } ? _badge : null;
        var reserve = badge is null ? 0 : badge.DesiredSize.Width + badge.Margin.Left;
        var width = Math.Max(0, finalSize.Width - reserve);
        _text.Arrange(new Rect(0, (finalSize.Height - _text.DesiredSize.Height) / 2, width, _text.DesiredSize.Height));

        // DS 6.9 "value + Edited": after the text on its first line, top-aligned when the value wraps
        if (badge is not null)
        {
            var x = Math.Min(_text.DesiredSize.Width, width) + badge.Margin.Left;
            var y = _text.DesiredSize.Height > badge.DesiredSize.Height * 1.5 ? (_text.DesiredSize.Height - finalSize.Height) / -2 : (finalSize.Height - badge.DesiredSize.Height) / 2;
            badge.Arrange(new Rect(x, Math.Max(0, y), badge.DesiredSize.Width, badge.DesiredSize.Height));
        }

        return finalSize;
    }


    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is TableItem item) Update(item);
    }

    #endregion // Protected methods
}


/// <summary>
/// The actions cell: it holds the copy button, made the first time its row is hovered, selected or copied, as a button in every realized row costs about 65 KB of heap (plan 8).
/// </summary>
public sealed class CopyCell : Panel
{
    #region Fields and constants

    private CopyButton? _button;
    private TableItem? _item;

    #endregion // Fields and constants


    #region Properties

    public bool HasButton => _button is not null;

    #endregion // Properties


    #region Public methods

    public void Update(TableItem item)
    {
        _item = item;
        IsVisible = !item.IsHeader;
        if (_button is not null) _button.Update(item);
        else if (item.Table.IsCopied(item.Key) || item.Table.Selection?.IsSelected(item.Key) == true) Materialize();
    }


    /// <summary>
    /// Makes the button now: it fades in from nothing, as on any later hover.
    /// </summary>
    public void Materialize()
    {
        if (_button is not null || _item is not { IsHeader: false } item) return;

        _button = new CopyButton { Opacity = 0 };
        _button.Update(item);
        Children.Add(_button);
        Avalonia.Threading.Dispatcher.UIThread.Post(() => _button.ClearValue(OpacityProperty), Avalonia.Threading.DispatcherPriority.Render);
    }

    #endregion // Public methods


    #region Protected methods

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is TableItem item) Update(item);
    }

    #endregion // Protected methods
}


/// <summary>
/// The copy button (28): it fades in on row hover or selection (140 ms) and turns into a green check for 1.3 s after a copy.
/// </summary>
public sealed class CopyButton : GlassButton
{
    #region Fields and constants

    private TableItem? _item;

    #endregion // Fields and constants


    #region Constructors

    public CopyButton()
    {
        Classes.Add("ghost");
        Classes.Add("small");
        Classes.Add("table-copy");
        Icon = IconId.Copy;
        IconSize = 15;
        Focusable = false;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        ToolTip.SetTip(this, UiText.COPY_VALUE);
        AutomationProperties.SetName(this, UiText.COPY_VALUE);
    }

    #endregion // Constructors


    #region Properties

    protected override Type StyleKeyOverride => typeof(GlassButton);

    #endregion // Properties


    #region Public methods

    public void Update(TableItem item)
    {
        _item = item;
        var copied = item.Table.IsCopied(item.Key);
        Classes.Set("copied", copied);
        Icon = copied ? IconId.CheckmarkFilled : IconId.Copy;
    }

    #endregion // Public methods


    #region Protected methods

    protected override void OnClick()
    {
        base.OnClick();
        if (_item is { Row: not null } item) item.Table.CopyValue(item);
    }

    #endregion // Protected methods
}
