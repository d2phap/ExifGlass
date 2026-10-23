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
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using ExifGlass.Core.Ai;
using ExifGlass.Core.Editing;
using ExifGlass.Core.Export;
using ExifGlass.Core.Metadata;
using ExifGlass.Core.Query;
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
/// The Tag name cell: one line with an ellipsis, the whole name in its tooltip (Proto); an AI field from another tag than its group's names it under (AI addendum question 1), and a tag the AI group was read from carries a sparkle that jumps there.
/// </summary>
public sealed class NameCell : Panel
{
    #region Fields and constants

    // the link's icon lines up with the name; its padding is 5
    private const double LINK_INDENT = -5;
    private const double MARK_GAP = 2;

    private readonly HighlightTextBlock _text;
    private SourceLink? _link;
    private GlassButton? _mark;

    #endregion // Fields and constants


    #region Constructors

    public NameCell()
    {
        _text = new HighlightTextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
        Typography.SetRole(_text, TextRole.Body);
        IdCell.BindHighlight(_text);
        _text[!TextBlock.ForegroundProperty] = _text.GetResourceObservable(TokenKeys.TEXT_SECONDARY_BRUSH).ToBinding();
        Children.Add(_text);
    }

    #endregion // Constructors


    #region Properties

    public string? Text => _text.Text;

    #endregion // Properties


    #region Public methods

    public void Update(TableItem item)
    {
        var name = item.TagName;
        _text.SetText(name, item.Row?.NameMatches);
        if (!ReferenceEquals(ToolTip.GetTip(_text), name)) ToolTip.SetTip(_text, name.Length > 0 ? name : null);

        // made the first time a recycled cell needs them, so most cells never carry a button
        if (item.Row?.Ai is { ShowsSource: true, SourceKey: { } key } ai)
        {
            _link ??= AddChild(new SourceLink { Focusable = false });
            _link.SourceKey = key;
            _link.OtherSources = ai.OtherSources;
            _link.IsVisible = true;
        }
        else if (_link is not null) _link.IsVisible = false;

        if (item.Row?.Tag?.IsAiSource == true)
        {
            _mark ??= AddChild(Mark());
            _mark.IsVisible = true;
        }
        else if (_mark is not null) _mark.IsVisible = false;
    }

    #endregion // Public methods


    #region Protected methods

    protected override Size MeasureOverride(Size availableSize)
    {
        var mark = _mark is { IsVisible: true } ? _mark : null;
        mark?.Measure(Size.Infinity);
        var reserve = mark is null ? 0 : mark.DesiredSize.Width + MARK_GAP;
        _text.Measure(availableSize.WithWidth(Math.Max(0, availableSize.Width - reserve)));

        var link = _link is { IsVisible: true } ? _link : null;
        link?.Measure(availableSize.WithWidth(availableSize.Width - LINK_INDENT));

        var width = Math.Max(_text.DesiredSize.Width + reserve, link?.DesiredSize.Width ?? 0);
        return new Size(width, _text.DesiredSize.Height + (link?.DesiredSize.Height ?? 0));
    }


    protected override Size ArrangeOverride(Size finalSize)
    {
        var link = _link is { IsVisible: true } ? _link : null;
        var mark = _mark is { IsVisible: true } ? _mark : null;
        var reserve = mark is null ? 0 : mark.DesiredSize.Width + MARK_GAP;
        var textHeight = _text.DesiredSize.Height;
        var top = (finalSize.Height - textHeight - (link?.DesiredSize.Height ?? 0)) / 2;

        _text.Arrange(new Rect(0, top, Math.Max(0, finalSize.Width - reserve), textHeight));
        link?.Arrange(new Rect(LINK_INDENT, top + textHeight, Math.Min(link.DesiredSize.Width, finalSize.Width - LINK_INDENT), link.DesiredSize.Height));
        if (mark is not null)
        {
            var x = Math.Min(_text.DesiredSize.Width, finalSize.Width - reserve) + MARK_GAP;
            mark.Arrange(new Rect(x, top + (textHeight - mark.DesiredSize.Height) / 2, mark.DesiredSize.Width, mark.DesiredSize.Height));
        }

        return finalSize;
    }


    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is TableItem item) Update(item);
    }

    #endregion // Protected methods


    #region Private methods

    private T AddChild<T>(T child) where T : Control
    {
        Children.Add(child);
        return child;
    }


    // the AI addendum's mark on a source tag: a 22 sparkle in Accent2 that jumps to the AI group
    private static GlassButton Mark()
    {
        var mark = new GlassButton
        {
            Icon = IconId.Sparkle,
            IconSize = 12,
            Width = 22,
            Height = 22,
            MinWidth = 22,
            Classes = { "ghost", "tiny" },
            Focusable = false,
            Tag = new AiActionRequest(AiAction.JumpToAiGroup),
        };
        mark[!TemplatedControl.ForegroundProperty] = mark.GetResourceObservable(TokenKeys.ACCENT2_TEXT_BRUSH).ToBinding();
        ToolTip.SetTip(mark, UiText.PARSED_INTO_TIP);
        return mark;
    }

    #endregion // Private methods
}


/// <summary>
/// An AI field's value with its spans (AI addendum): prompt tints, mono chips on a tint after the value, quiet notes; the search highlight over it all.
/// </summary>
public sealed class AiValueText : HighlightTextBlock
{
    #region Fields and constants

    private const double CHIP_RADIUS = 8;
    private const double CHIP_PAD = 5;
    private const double MONO_SCALE = 0.92;

    private readonly List<(int Start, int Length, string BrushKey)> _fills = [];
    private AiRow? _row;

    #endregion // Fields and constants


    #region Constructors

    public AiValueText()
    {
        ActualThemeVariantChanged += (_, _) => Build();
    }

    #endregion // Constructors


    #region Public methods

    public void Show(AiRow row, IReadOnlyList<TextRange>? matches)
    {
        SetMatches(matches);
        if (ReferenceEquals(row, _row)) return;

        _row = row;
        Build();
    }

    #endregion // Public methods


    #region Protected methods

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Build();
    }


    // chip fills go under the highlight and the text
    protected override void RenderTextLayout(DrawingContext context, Point origin)
    {
        foreach (var (start, length, key) in _fills)
        {
            if (!this.TryFindResource(key, ActualThemeVariant, out var value) || value is not IBrush brush) continue;

            foreach (var rect in TextLayout.HitTestTextRange(start, length))
            {
                context.DrawRectangle(brush, null, new RoundedRect(rect.Translate(origin).Inflate(new Thickness(CHIP_PAD, 0)), CHIP_RADIUS));
            }
        }

        base.RenderTextLayout(context, origin);
    }

    #endregion // Protected methods


    #region Private methods

    private void Build()
    {
        _fills.Clear();
        if (_row is not { } row) return;

        var text = ShownText.Cut(row.Display, ShownText.CELL);
        var inlines = new InlineCollection();
        var mono = this.TryFindResource(TokenKeys.FONT_MONO, out var m) && m is FontFamily family ? family : FontFamily.Default;
        var badge = Typography.Spec(TextRole.MonoBadge).Size - AppFonts.SizeOffset;
        var at = 0;
        foreach (var span in row.Spans)
        {
            if (span.Start < at || span.Start + span.Length > text.Length) continue;
            if (span.Start > at) inlines.Add(new Run(text[at..span.Start]));

            var run = new Run(text.Substring(span.Start, span.Length));
            switch (span.Kind)
            {
                case AiSpanKind.Weight:
                    run.Foreground = Brush(TokenKeys.ACCENT_TEXT_BRUSH);
                    break;
                case AiSpanKind.Lora:
                    run.Foreground = Brush(TokenKeys.ACCENT2_TEXT_BRUSH);
                    run.FontFamily = mono;
                    run.FontSize = FontSize * MONO_SCALE;
                    _fills.Add((span.Start, span.Length, TokenKeys.ACCENT2_A10_BRUSH));
                    break;
                case AiSpanKind.Flag:
                    run.Foreground = Brush(TokenKeys.ACCENT_TEXT_BRUSH);
                    run.FontFamily = mono;
                    run.FontSize = FontSize * MONO_SCALE;
                    break;
                case AiSpanKind.Keyword:
                    run.Foreground = Brush(TokenKeys.ACCENT2_TEXT_BRUSH);
                    run.FontWeight = FontWeight.Medium;
                    break;
                case AiSpanKind.Embedding:
                    run.Foreground = Brush(TokenKeys.ACCENT2_TEXT_BRUSH);
                    run.TextDecorations = [new TextDecoration { Location = TextDecorationLocation.Underline, StrokeDashArray = [1, 2], StrokeOffset = 2 }];
                    break;
                case AiSpanKind.Chip:
                    run.Foreground = Brush(TokenKeys.TEXT_SECONDARY_BRUSH);
                    run.FontFamily = mono;
                    run.FontSize = badge;
                    _fills.Add((span.Start, span.Length, TokenKeys.EMPHASIS_A06_BRUSH));
                    break;
                case AiSpanKind.WeightChip:
                    run.Foreground = Brush(TokenKeys.ACCENT2_TEXT_BRUSH);
                    run.FontFamily = mono;
                    run.FontSize = badge;
                    _fills.Add((span.Start, span.Length, TokenKeys.ACCENT2_A08_BRUSH));
                    break;
                case AiSpanKind.Note:
                    run.Foreground = Brush(TokenKeys.TEXT_TERTIARY_BRUSH);
                    run.FontSize = Typography.Spec(TextRole.Meta).Size;
                    break;
            }

            inlines.Add(run);
            at = span.Start + span.Length;
        }

        if (at < text.Length) inlines.Add(new Run(text[at..]));
        Inlines = inlines;
        InvalidateVisual();
    }


    private IBrush? Brush(string key) => this.TryFindResource(key, ActualThemeVariant, out var value) ? value as IBrush : null;

    #endregion // Private methods
}


/// <summary>
/// The Value cell: wrapped and clamped at 6 lines (Appendix B item 7), with the staged-edit badge after its first line (S2 fallback 5); an AI field draws its spans, and the Kind field its kind chip.
/// </summary>
public sealed class ValueCell : Panel
{
    #region Fields and constants

    private const int MAX_LINES = 6;

    private readonly HighlightTextBlock _text;
    private AiValueText? _rich;
    private Badge? _kind;
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

    // what the cell shows now: the plain text, an AI field's spans, or the kind chip
    private Control Shown => _kind is { IsVisible: true } ? _kind : _rich is { IsVisible: true } ? _rich : _text;

    #endregion // Properties


    #region Public methods

    public void Update(TableItem item)
    {
        var row = item.Row;
        var state = row?.EditState ?? EditState.None;
        var ai = row?.Ai;

        if (ai?.KindChip is { } kind)
        {
            _kind ??= AddChild(new Badge { VerticalAlignment = VerticalAlignment.Center });
            (_kind.Kind, _kind.Icon, _kind.Text) = (AiCard.KindBadgeOf(kind), AiCard.KindIconOf(kind), ai.Value);
            Show(_kind);
        }
        else if (ai is { Spans.Count: > 0 })
        {
            _rich ??= AddChild(Rich());
            _rich.Show(ai, ShownText.Matches(row!.ValueMatches, ShownText.CELL));
            Show(_rich);
        }
        else
        {
            _text.SetText(item.Value, row?.ValueMatches);
            Show(_text);
        }

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
        var content = Shown;
        var badge = _badge is { IsVisible: true } ? _badge : null;
        badge?.Measure(availableSize);
        var reserve = badge is null ? 0 : badge.DesiredSize.Width + badge.Margin.Left;
        content.Measure(availableSize.WithWidth(Math.Max(0, availableSize.Width - reserve)));

        return new Size(content.DesiredSize.Width + reserve, Math.Max(content.DesiredSize.Height, badge?.DesiredSize.Height ?? 0));
    }


    protected override Size ArrangeOverride(Size finalSize)
    {
        var content = Shown;
        var badge = _badge is { IsVisible: true } ? _badge : null;
        var reserve = badge is null ? 0 : badge.DesiredSize.Width + badge.Margin.Left;
        var width = Math.Max(0, finalSize.Width - reserve);
        content.Arrange(new Rect(0, (finalSize.Height - content.DesiredSize.Height) / 2, content == _kind ? content.DesiredSize.Width : width, content.DesiredSize.Height));

        // DS 6.9 "value + Edited": after the text on its first line, top-aligned when the value wraps
        if (badge is not null)
        {
            var x = Math.Min(content.DesiredSize.Width, width) + badge.Margin.Left;
            var y = content.DesiredSize.Height > badge.DesiredSize.Height * 1.5 ? (content.DesiredSize.Height - finalSize.Height) / -2 : (finalSize.Height - badge.DesiredSize.Height) / 2;
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


    #region Private methods

    // after the text and before the edit badge, so the badge keeps drawing on top
    private T AddChild<T>(T child) where T : Control
    {
        Children.Insert(1, child);
        return child;
    }


    private void Show(Control content)
    {
        _text.IsVisible = content == _text;
        if (_rich is not null) _rich.IsVisible = content == _rich;
        if (_kind is not null) _kind.IsVisible = content == _kind;
    }


    private static AiValueText Rich()
    {
        var rich = new AiValueText
        {
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = MAX_LINES,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Typography.SetRole(rich, TextRole.Body);
        IdCell.BindHighlight(rich);
        return rich;
    }

    #endregion // Private methods
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
