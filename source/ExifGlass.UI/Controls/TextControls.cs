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
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using ExifGlass.Core.Query;
using ExifGlass.UI.Tokens;
using System.Text.RegularExpressions;

namespace ExifGlass.UI.Controls;

/// <summary>
/// How much of a long value the UI lays out; copy and search still use all of it.
/// </summary>
public static class ShownText
{
    #region Fields and constants

    // a 2 MB ComfyUI workflow laid out whole blocked the UI thread for 330 ms per cell (G7)
    public const int CELL = 2_048;
    public const int WELL = 10_000;

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// The text's first characters and an ellipsis, or the text when it is short.
    /// </summary>
    public static string Cut(string text, int max) => text.Length <= max ? text : text[..End(text, max)] + "…";


    /// <summary>
    /// The text's first characters for a value well, with how many more there are.
    /// </summary>
    public static string Well(string text) => text.Length <= WELL ? text : text[..End(text, WELL)] + "…\n\n" + UiText.MoreCharacters(text.Length - End(text, WELL));


    /// <summary>
    /// The match ranges that fall inside a cut text.
    /// </summary>
    public static IReadOnlyList<TextRange>? Matches(IReadOnlyList<TextRange>? matches, int length)
    {
        if (matches is null || matches.Count == 0 || matches[^1].Start + matches[^1].Length <= length) return matches;

        return [.. matches.Where(m => m.Start + m.Length <= length)];
    }

    #endregion // Public methods


    #region Private methods

    private static int End(string text, int max) => char.IsHighSurrogate(text[max - 1]) ? max - 1 : max;

    #endregion // Private methods
}


/// <summary>
/// Text with every search match on a rounded Highlight background (DS 6.9, plan Appendix A item 18), drawn from TextLayout hit-test rectangles.
/// </summary>
public class HighlightTextBlock : TextBlock
{
    #region Fields and constants

    private const double HIGHLIGHT_RADIUS = 3;

    public static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<HighlightTextBlock, IBrush?>(nameof(HighlightBrush));

    public static readonly StyledProperty<string?> QueryProperty =
        AvaloniaProperty.Register<HighlightTextBlock, string?>(nameof(Query));

    private int[]? _ranges;
    private IReadOnlyList<TextRange>? _matches;

    #endregion // Fields and constants


    #region Properties

    public IBrush? HighlightBrush
    {
        get => GetValue(HighlightBrushProperty);
        set => SetValue(HighlightBrushProperty, value);
    }

    /// <summary>
    /// Text to find, case-insensitive; the query engine's own ranges go through SetText instead.
    /// </summary>
    public string? Query
    {
        get => GetValue(QueryProperty);
        set => SetValue(QueryProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(TextBlock);

    #endregion // Properties


    #region Constructors

    static HighlightTextBlock()
    {
        AffectsRender<HighlightTextBlock>(HighlightBrushProperty);
    }

    #endregion // Constructors


    #region Public methods

    /// <summary>
    /// Sets the text and the query engine's match ranges; the same list again redraws nothing.
    /// </summary>
    public void SetText(string text, IReadOnlyList<TextRange>? matches)
    {
        if (text.Length > ShownText.CELL)
        {
            text = ShownText.Cut(text, ShownText.CELL);
            matches = ShownText.Matches(matches, ShownText.CELL);
        }

        var redraw = !ReferenceEquals(_matches, matches);
        _matches = matches is { Count: > 0 } ? matches : null;
        if (Text != text) Text = text;
        else if (redraw) InvalidateVisual();
    }


    /// <summary>
    /// Finds every case-insensitive match of a query as (start, length) pairs.
    /// </summary>
    public static int[]? FindRanges(string? text, string? query)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query)) return null;

        var ranges = new List<int>();
        var at = 0;
        while ((at = text.IndexOf(query, at, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            ranges.Add(at);
            ranges.Add(query.Length);
            at += query.Length;
        }

        return ranges.Count > 0 ? [.. ranges] : null;
    }

    #endregion // Public methods


    #region Protected methods

    /// <summary>
    /// Sets the match ranges alone, for text built from inlines.
    /// </summary>
    protected void SetMatches(IReadOnlyList<TextRange>? matches)
    {
        var redraw = !ReferenceEquals(_matches, matches);
        _matches = matches is { Count: > 0 } ? matches : null;
        if (redraw) InvalidateVisual();
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == QueryProperty || (change.Property == TextProperty && Query is not null))
        {
            _ranges = FindRanges(Text, Query);
            InvalidateVisual();
        }
    }


    // TextBlock.Render is sealed in Avalonia 12; this hook runs after the background, before the text (S2)
    protected override void RenderTextLayout(DrawingContext context, Point origin)
    {
        if (HighlightBrush is { } brush)
        {
            if (_matches is { } matches)
            {
                foreach (var match in matches) DrawMatch(context, brush, origin, match.Start, match.Length);
            }
            else if (_ranges is { Length: > 1 } ranges)
            {
                for (var i = 0; i + 1 < ranges.Length; i += 2) DrawMatch(context, brush, origin, ranges[i], ranges[i + 1]);
            }
        }

        base.RenderTextLayout(context, origin);
    }

    #endregion // Protected methods


    #region Private methods

    // a match past a clamped or trimmed end has no rectangles, so nothing is drawn for it
    private void DrawMatch(DrawingContext context, IBrush brush, Point origin, int start, int length)
    {
        foreach (var rect in TextLayout.HitTestTextRange(start, length))
        {
            context.DrawRectangle(brush, null, new RoundedRect(rect.Translate(origin), HIGHLIGHT_RADIUS));
        }
    }

    #endregion // Private methods
}


/// <summary>
/// A prompt with its syntax tinted (AI addendum): weights in AccentText, LoRA tags in Accent2 mono on a tint, embeddings dotted, BREAK and AND in Accent2, Midjourney flags in mono; selectable and clamped.
/// </summary>
public partial class PromptText : SelectableTextBlock
{
    #region Fields and constants

    private const double TAG_RADIUS = 5;
    private const double MONO_SCALE = 0.92;

    public static readonly StyledProperty<string?> PromptProperty =
        AvaloniaProperty.Register<PromptText, string?>(nameof(Prompt));

    public static readonly StyledProperty<int> ClampLinesProperty =
        AvaloniaProperty.Register<PromptText, int>(nameof(ClampLines), 4);

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<PromptText, bool>(nameof(IsExpanded));

    public static readonly StyledProperty<IReadOnlyCollection<string>?> EmbeddingsProperty =
        AvaloniaProperty.Register<PromptText, IReadOnlyCollection<string>?>(nameof(Embeddings));

    public static readonly DirectProperty<PromptText, bool> IsClampedProperty =
        AvaloniaProperty.RegisterDirect<PromptText, bool>(nameof(IsClamped), t => t.IsClamped);

    private readonly List<(int Start, int Length)> _tags = [];
    private bool _isClamped;

    #endregion // Fields and constants


    #region Properties

    public string? Prompt
    {
        get => GetValue(PromptProperty);
        set => SetValue(PromptProperty, value);
    }

    /// <summary>
    /// Lines shown while collapsed: 4 for a prompt, 2 for a negative prompt (DS 6.11).
    /// </summary>
    public int ClampLines
    {
        get => GetValue(ClampLinesProperty);
        set => SetValue(ClampLinesProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>
    /// Embedding names the AI engine found, tinted where the prompt names them.
    /// </summary>
    public IReadOnlyCollection<string>? Embeddings
    {
        get => GetValue(EmbeddingsProperty);
        set => SetValue(EmbeddingsProperty, value);
    }

    /// <summary>
    /// Gets whether the clamp hides text, so Show more is worth showing.
    /// </summary>
    public bool IsClamped
    {
        get => _isClamped;
        private set => SetAndRaise(IsClampedProperty, ref _isClamped, value);
    }

    protected override Type StyleKeyOverride => typeof(SelectableTextBlock);

    #endregion // Properties


    #region Constructors

    public PromptText()
    {
        TextWrapping = TextWrapping.Wrap;
        TextTrimming = TextTrimming.CharacterEllipsis;
        MaxLines = ClampLines;
        ActualThemeVariantChanged += (_, _) => BuildInlines();
    }

    #endregion // Constructors


    #region Protected methods

    // tints come from the theme's resources, which resolve only once in a tree
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        BuildInlines();
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PromptProperty || change.Property == EmbeddingsProperty) BuildInlines();
        else if (change.Property == ClampLinesProperty || change.Property == IsExpandedProperty) MaxLines = IsExpanded ? 0 : ClampLines;
    }


    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);

        // clamped when the last laid-out line stops short of the text
        var text = Prompt ?? string.Empty;
        if (MaxLines > 0 && TextLayout.TextLines.Count > 0)
        {
            var last = TextLayout.TextLines[^1];
            IsClamped = last.FirstTextSourceIndex + last.Length < text.Length;
        }
        else if (!IsExpanded) IsClamped = false;

        return size;
    }


    protected override void RenderTextLayout(DrawingContext context, Point origin)
    {
        if (_tags.Count > 0 && this.TryFindResource(TokenKeys.ACCENT2_A10_BRUSH, ActualThemeVariant, out var value) && value is IBrush brush)
        {
            foreach (var (start, length) in _tags)
            {
                foreach (var rect in TextLayout.HitTestTextRange(start, length))
                {
                    context.DrawRectangle(brush, null, new RoundedRect(rect.Translate(origin).Inflate(new Thickness(2, 0)), TAG_RADIUS));
                }
            }
        }

        base.RenderTextLayout(context, origin);
    }

    #endregion // Protected methods


    #region Private methods

    [GeneratedRegex(@"<(?:lora|lyco|hypernet):[^:>]+:[\d.]+>|\(([^()]+):([\d.]+)\)|--[a-z]+ [\w:.]+|\b(?:BREAK|AND)\b", RegexOptions.CultureInvariant)]
    private static partial Regex SyntaxRegex();


    private void BuildInlines()
    {
        _tags.Clear();
        var text = Prompt ?? string.Empty;
        var inlines = new InlineCollection();
        var mono = this.TryFindResource(TokenKeys.FONT_MONO, out var m) && m is FontFamily family ? family : FontFamily.Default;
        var accent = Brush(TokenKeys.ACCENT_TEXT_BRUSH);
        var accent2 = Brush(TokenKeys.ACCENT2_TEXT_BRUSH);

        var at = 0;
        foreach (var span in Tokenize(text))
        {
            if (span.Start > at) AddEmbeddingRuns(inlines, text[at..span.Start], accent2);

            var piece = text.Substring(span.Start, span.Length);
            var run = new Run(piece);
            switch (span.Kind)
            {
                case SpanKind.Lora:
                    run.Foreground = accent2;
                    run.FontFamily = mono;
                    run.FontSize = FontSize * MONO_SCALE;
                    _tags.Add((span.Start, span.Length));
                    break;
                case SpanKind.Weight:
                    run.Foreground = accent;
                    break;
                case SpanKind.Flag:
                    run.Foreground = accent;
                    run.FontFamily = mono;
                    run.FontSize = FontSize * MONO_SCALE;
                    break;
                case SpanKind.Keyword:
                    run.Foreground = accent2;
                    run.FontWeight = FontWeight.Medium;
                    break;
            }

            inlines.Add(run);
            at = span.Start + span.Length;
        }

        if (at < text.Length) AddEmbeddingRuns(inlines, text[at..], accent2);

        Inlines = inlines;
        InvalidateVisual();
    }


    private void AddEmbeddingRuns(InlineCollection inlines, string text, IBrush? accent2)
    {
        if (Embeddings is not { Count: > 0 } names)
        {
            inlines.Add(new Run(text));
            return;
        }

        var at = 0;
        while (at < text.Length)
        {
            var (hit, length) = NextEmbedding(text, at, names);
            if (hit < 0)
            {
                inlines.Add(new Run(text[at..]));
                return;
            }

            if (hit > at) inlines.Add(new Run(text[at..hit]));

            var dotted = new TextDecorationCollection { new TextDecoration { Location = TextDecorationLocation.Underline, StrokeDashArray = new AvaloniaList<double> { 1, 2 }, StrokeOffset = 2 } };
            inlines.Add(new Run(text.Substring(hit, length)) { Foreground = accent2, TextDecorations = dotted });
            at = hit + length;
        }
    }


    private static (int Index, int Length) NextEmbedding(string text, int from, IReadOnlyCollection<string> names)
    {
        var best = (-1, 0);
        foreach (var name in names)
        {
            if (name.Length == 0) continue;

            var i = text.IndexOf(name, from, StringComparison.OrdinalIgnoreCase);
            if (i >= 0 && (best.Item1 < 0 || i < best.Item1)) best = (i, name.Length);
        }

        return best;
    }


    private static IEnumerable<(int Start, int Length, SpanKind Kind)> Tokenize(string text)
    {
        foreach (Match match in SyntaxRegex().Matches(text))
        {
            var kind = match.Value[0] switch
            {
                '<' => SpanKind.Lora,
                '(' => SpanKind.Weight,
                '-' => SpanKind.Flag,
                _ => SpanKind.Keyword,
            };

            yield return (match.Index, match.Length, kind);
        }
    }


    private IBrush? Brush(string key) => this.TryFindResource(key, ActualThemeVariant, out var value) ? value as IBrush : null;

    #endregion // Private methods


    #region Nested types

    private enum SpanKind
    {
        Lora,
        Weight,
        Flag,
        Keyword,
    }

    #endregion // Nested types
}
