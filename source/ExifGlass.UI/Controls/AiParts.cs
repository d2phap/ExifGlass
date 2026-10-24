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
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ExifGlass.Core.Ai;
using ExifGlass.UI.Tokens;
using System.Globalization;
using System.Windows.Input;

namespace ExifGlass.UI.Controls;

/// <summary>
/// What the AI surfaces ask the app to do: copy, save, show a source tag.
/// </summary>
public enum AiAction
{
    CopyPrompt,
    CopyNegativePrompt,

    /// <summary>
    /// Copies the request's text: a parameter, a character prompt, a hash, the raw text.
    /// </summary>
    CopyText,

    CopyGenerationParameters,
    CopyJson,
    CopyWorkflowJson,
    SaveWorkflow,

    /// <summary>
    /// Selects the tag the request's key names in the table.
    /// </summary>
    ShowSourceTag,

    ShowSourceTags,
    JumpToAiGroup,
    UseBundledExifTool,
    OpenSettings,
    CopyCharacterJson,
    SaveCharacterJson,
    CopyTagKey,
}


/// <summary>
/// One AI action with what it acts on: the text to copy and its label for the toast, or the key of the source tag.
/// </summary>
public sealed record AiActionRequest(AiAction Action, string? Text = null, string? Label = null, string? Key = null);


/// <summary>
/// The quiet mono link to the tag an AI field came from (AI addendum): source icon, "PNG:Parameters", "+1" when more tags feed it.
/// </summary>
public class SourceLink : GlassButton
{
    #region Fields and constants

    // GlassButton's gap between its icon and content
    private const double ICON_GAP = 6;

    public static readonly StyledProperty<string?> SourceKeyProperty =
        AvaloniaProperty.Register<SourceLink, string?>(nameof(SourceKey));

    public static readonly StyledProperty<IReadOnlyList<string>?> OtherSourcesProperty =
        AvaloniaProperty.Register<SourceLink, IReadOnlyList<string>?>(nameof(OtherSources));

    private readonly TextBlock _label = new() { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _more = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly SquircleBorder _moreChip;

    #endregion // Fields and constants


    #region Constructors

    // its look is set here, not by a class style, which every GlassButton would carry (G6 finding 2)
    public SourceLink()
    {
        Icon = IconId.AiSource;
        IconSize = 11;
        Height = 20;
        MinWidth = 0;
        Padding = new Thickness(5, 0, 6, 0);
        CornerRadius = new CornerRadius(9);
        BorderThickness = default;
        FontWeight = FontWeight.Normal;
        HorizontalAlignment = HorizontalAlignment.Left;
        SetForeground(false);

        Typography.SetRole(_label, TextRole.MonoBadge);
        _label.FontWeight = FontWeight.Normal;
        Typography.SetRole(_more, TextRole.Badge);
        _more[!TextBlock.ForegroundProperty] = _more.GetResourceObservable(TokenKeys.TEXT_SECONDARY_BRUSH).ToBinding();
        _moreChip = new SquircleBorder { Child = _more, Padding = new Thickness(4, 0), Height = 16, CornerRadius = new CornerRadius(7), IsVisible = false, VerticalAlignment = VerticalAlignment.Center };
        _moreChip[!SquircleBorder.BackgroundProperty] = _moreChip.GetResourceObservable(TokenKeys.EMPHASIS_A06_BRUSH).ToBinding();
        Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { _label, _moreChip } };
    }

    #endregion // Constructors


    #region Properties

    /// <summary>
    /// The table key of the source tag: "PNG:Parameters", "XMP:XMP-iptcExt:DigitalSourceType".
    /// </summary>
    public string? SourceKey
    {
        get => GetValue(SourceKeyProperty);
        set => SetValue(SourceKeyProperty, value);
    }

    public IReadOnlyList<string>? OtherSources
    {
        get => GetValue(OtherSourcesProperty);
        set => SetValue(OtherSourcesProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(GlassButton);

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// The tooltip: "Show source tag · -PNG:Parameters", then the other tags.
    /// </summary>
    public static string Tip(string key, IReadOnlyList<string>? others)
    {
        var tip = $"{UiText.SHOW_SOURCE_TAG} · -{AiRows.SourceLabel(key)}";
        return others is { Count: > 0 } ? tip + "\n" + UiText.ALSO + string.Join(", ", others.Select(AiRows.SourceLabel)) : tip;
    }

    #endregion // Public methods


    #region Protected methods

    // the button lays its icon and content out in a stack, which never trims: the label gets the room that is left
    protected override Size MeasureOverride(Size availableSize)
    {
        var room = double.PositiveInfinity;
        if (double.IsFinite(availableSize.Width))
        {
            _moreChip.Measure(Size.Infinity);
            var more = _moreChip.IsVisible ? _moreChip.DesiredSize.Width + 4 : 0;
            room = Math.Max(0, availableSize.Width - Padding.Left - Padding.Right - IconSize - ICON_GAP - more);
        }

        if (_label.MaxWidth != room) _label.MaxWidth = room;
        return base.MeasureOverride(availableSize);
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsPointerOverProperty || change.Property == InteractionOverlay.ForcedStateProperty)
        {
            SetForeground(IsPointerOver || InteractionOverlay.GetForcedState(this) is InteractionState.Hover or InteractionState.Pressed);
        }

        if (change.Property != SourceKeyProperty && change.Property != OtherSourcesProperty) return;

        var key = SourceKey;
        var more = OtherSources?.Count ?? 0;
        _label.Text = key is null ? null : AiRows.SourceLabel(key);
        _moreChip.IsVisible = more > 0;
        _more.Text = more > 0 ? "+" + more.ToString(CultureInfo.InvariantCulture) : null;
        ToolTip.SetTip(this, key is null ? null : Tip(key, OtherSources));
        Tag = key is null ? null : new AiActionRequest(AiAction.ShowSourceTag, Key: key);
    }

    #endregion // Protected methods


    #region Private methods

    // TextTertiary at rest, AccentText on hover (AI addendum); the theme's brush transition fades it
    private void SetForeground(bool hover) =>
        this[!ForegroundProperty] = this.GetResourceObservable(hover ? TokenKeys.ACCENT_TEXT_BRUSH : TokenKeys.TEXT_TERTIARY_BRUSH).ToBinding();

    #endregion // Private methods
}


/// <summary>
/// How an InlineNote looks: plain text, or an info, warning or danger panel (AI addendum).
/// </summary>
public enum NoteKind
{
    Plain,
    Info,
    Warning,
    Danger,
}


/// <summary>
/// A note in a card (AI addendum): an icon 14 and Meta text, on Emphasis 3.5% (info), orange 10% (warning) or Danger 8%, radius 12; its content holds the note's buttons.
/// </summary>
public class InlineNote : ContentControl
{
    #region Fields and constants

    public static readonly StyledProperty<NoteKind> KindProperty =
        AvaloniaProperty.Register<InlineNote, NoteKind>(nameof(Kind));

    public static readonly StyledProperty<IconId?> IconProperty =
        AvaloniaProperty.Register<InlineNote, IconId?>(nameof(Icon), IconId.Info);

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<InlineNote, string?>(nameof(Text));

    #endregion // Fields and constants


    #region Constructors

    public InlineNote()
    {
        UpdatePseudoClasses();
    }

    #endregion // Constructors


    #region Properties

    public NoteKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public IconId? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    #endregion // Properties


    #region Protected methods

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == KindProperty || change.Property == ContentProperty) UpdatePseudoClasses();
    }

    #endregion // Protected methods


    #region Private methods

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":info", Kind == NoteKind.Info);
        PseudoClasses.Set(":warning", Kind == NoteKind.Warning);
        PseudoClasses.Set(":danger", Kind == NoteKind.Danger);
        PseudoClasses.Set(":actions", Content is not null);
    }

    #endregion // Private methods
}


/// <summary>
/// One line of a key and value list: the value may be mono, quiet (TextTertiary), or carry a mono sub-line such as the raw term.
/// </summary>
public sealed record AiKeyValue(string Key, string Value, string? Sub = null, bool IsMono = false, bool IsQuiet = false);


/// <summary>
/// Keys and values in two columns (AI addendum kvlist): keys Caption in TextSecondary, at least 92 wide; values wrap; rows 5 apart, columns 12.
/// </summary>
public class KeyValueList : Panel
{
    #region Fields and constants

    private const double MIN_KEY = 92;
    private const double COLUMN_GAP = 12;
    private const double ROW_GAP = 5;

    public static readonly StyledProperty<IReadOnlyList<AiKeyValue>?> ItemsProperty =
        AvaloniaProperty.Register<KeyValueList, IReadOnlyList<AiKeyValue>?>(nameof(Items));

    private double _keyWidth;

    #endregion // Fields and constants


    #region Properties

    public IReadOnlyList<AiKeyValue>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    #endregion // Properties


    #region Protected methods

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsProperty) Build();
    }


    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 280 : availableSize.Width;
        _keyWidth = MIN_KEY;
        for (var i = 0; i < Children.Count; i += 2)
        {
            Children[i].Measure(Size.Infinity);
            _keyWidth = Math.Max(_keyWidth, Children[i].DesiredSize.Width);
        }

        _keyWidth = Math.Min(_keyWidth, Math.Floor(width * 0.45));
        var valueWidth = Math.Max(0, width - _keyWidth - COLUMN_GAP);
        double height = 0;
        for (var i = 0; i + 1 < Children.Count; i += 2)
        {
            Children[i].Measure(new Size(_keyWidth, double.PositiveInfinity));
            Children[i + 1].Measure(new Size(valueWidth, double.PositiveInfinity));
            height += Math.Max(Children[i].DesiredSize.Height, Children[i + 1].DesiredSize.Height) + (i > 0 ? ROW_GAP : 0);
        }

        return new Size(width, height);
    }


    protected override Size ArrangeOverride(Size finalSize)
    {
        var valueWidth = Math.Max(0, finalSize.Width - _keyWidth - COLUMN_GAP);
        double y = 0;
        for (var i = 0; i + 1 < Children.Count; i += 2)
        {
            var row = Math.Max(Children[i].DesiredSize.Height, Children[i + 1].DesiredSize.Height);
            Children[i].Arrange(new Rect(0, y, _keyWidth, Children[i].DesiredSize.Height));
            Children[i + 1].Arrange(new Rect(_keyWidth + COLUMN_GAP, y, valueWidth, row));
            y += row + ROW_GAP;
        }

        return finalSize;
    }

    #endregion // Protected methods


    #region Private methods

    // built in code with typed setters: a class style per text block would cost every TextBlock in the app (G6)
    private void Build()
    {
        Children.Clear();
        foreach (var item in Items ?? [])
        {
            var key = new TextBlock { Text = item.Key, TextTrimming = TextTrimming.CharacterEllipsis };
            Typography.SetRole(key, TextRole.Caption);
            key[!TextBlock.ForegroundProperty] = key.GetResourceObservable(TokenKeys.TEXT_SECONDARY_BRUSH).ToBinding();

            var value = new SelectableTextBlock { Text = item.Value, TextWrapping = TextWrapping.Wrap };
            Typography.SetRole(value, item.IsMono ? TextRole.Mono : TextRole.Caption);
            if (item.IsQuiet) value[!TextBlock.ForegroundProperty] = value.GetResourceObservable(TokenKeys.TEXT_TERTIARY_BRUSH).ToBinding();

            Control cell = value;
            if (item.Sub is { Length: > 0 } sub)
            {
                var line = new SelectableTextBlock { Text = sub, TextWrapping = TextWrapping.Wrap };
                Typography.SetRole(line, TextRole.MonoBadge);
                line.FontWeight = FontWeight.Normal;
                line[!TextBlock.ForegroundProperty] = line.GetResourceObservable(TokenKeys.TEXT_TERTIARY_BRUSH).ToBinding();
                cell = new StackPanel { Spacing = 1, Children = { value, line } };
            }

            Children.Add(key);
            Children.Add(cell);
        }
    }

    #endregion // Private methods
}


/// <summary>
/// Makes a control's copy button on its first hover and fades it with the hover (AI addendum: copy next to the data it copies); a button in every tile would cost heap for nothing.
/// </summary>
internal sealed class HoverCopy
{
    #region Fields and constants

    private readonly Panel _host;
    private readonly Func<AiActionRequest?> _request;
    private readonly string _tip;
    private readonly bool _centred;
    private GlassButton? _button;

    #endregion // Fields and constants


    #region Constructors

    public HoverCopy(Panel host, string tip, Func<AiActionRequest?> request, bool centred = false)
    {
        _host = host;
        _tip = tip;
        _request = request;
        _centred = centred;
        host.PointerEntered += (_, _) => Show(true);
        host.PointerExited += (_, _) => Show(false);
    }

    #endregion // Constructors


    #region Public methods

    public void Show(bool shown)
    {
        // nothing to copy (a model without a hash): no button
        var request = shown ? _request() : null;
        if (request is null)
        {
            if (_button is null) return;

            _button.Opacity = 0;
            _button.IsHitTestVisible = false;
            return;
        }

        if (_button is null)
        {
            // the selected tag card's copy size, so every copy in the inspector matches (owner after G7)
            _button = new GlassButton
            {
                Icon = IconId.Copy,
                Margin = _centred ? new Thickness(0, 0, 4, 0) : new Thickness(0, 4, 4, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = _centred ? VerticalAlignment.Center : VerticalAlignment.Top,
                Classes = { "ghost", "small" },
                Focusable = false,
                Opacity = 0,
                Transitions = [new DoubleTransition { Property = Visual.OpacityProperty, Duration = MotionTokens.Hover, Easing = MotionTokens.HoverEasing }],
            };
            ToolTip.SetTip(_button, _tip);
            _button[!TemplatedControl.ForegroundProperty] = _button.GetResourceObservable(TokenKeys.TEXT_SECONDARY_BRUSH).ToBinding();

            // the card's click handler reads the request from the tag; this one runs first, on the button itself
            _button.Click += (_, _) => _button.Tag = _request();
            _host.Children.Add(_button);
        }

        _button.Tag = request;
        _button.IsHitTestVisible = true;
        _button.Opacity = 1;
    }

    #endregion // Public methods
}


/// <summary>
/// A panel that shows a copy button on hover for one value (owner after G7: the camera cards copy as the AI card's tiles do); its card routes the click to its copy command.
/// </summary>
public sealed class CopyHost : Panel
{
    #region Fields and constants

    public static readonly StyledProperty<string?> CopyTextProperty =
        AvaloniaProperty.Register<CopyHost, string?>(nameof(CopyText));

    public static readonly StyledProperty<string?> CopyLabelProperty =
        AvaloniaProperty.Register<CopyHost, string?>(nameof(CopyLabel));

    #endregion // Fields and constants


    #region Constructors

    public CopyHost()
    {
        Background = Brushes.Transparent;
        _ = new HoverCopy(this, UiText.COPY_VALUE, () => CopyText is { Length: > 0 } text ? new AiActionRequest(AiAction.CopyText, text, CopyLabel) : null);
    }

    #endregion // Constructors


    #region Properties

    public string? CopyText
    {
        get => GetValue(CopyTextProperty);
        set => SetValue(CopyTextProperty, value);
    }

    /// <summary>
    /// Gets or sets the value's name, for its toast: "Copied lens".
    /// </summary>
    public string? CopyLabel
    {
        get => GetValue(CopyLabelProperty);
        set => SetValue(CopyLabelProperty, value);
    }

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Runs a card's command for the copy buttons inside it.
    /// </summary>
    public static void Route(Control card, Func<ICommand?> command) => card.AddHandler(Button.ClickEvent, (_, e) =>
    {
        if (e.Source is not Button { Tag: AiActionRequest request } || command() is not { } run || !run.CanExecute(request)) return;

        run.Execute(request);
        e.Handled = true;
    });

    #endregion // Public methods
}


/// <summary>
/// One tile of the AI card's parameter grid: label 10.5 in TextSecondary, the value in mono; a copy button on hover (AI addendum: copy seed next to the seed).
/// </summary>
public sealed class ParameterTile : Panel
{
    #region Fields and constants

    public static readonly StyledProperty<AiParameterView?> ViewProperty =
        AvaloniaProperty.Register<ParameterTile, AiParameterView?>(nameof(View));

    private readonly TextBlock _label = new() { TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _value = new() { TextTrimming = TextTrimming.CharacterEllipsis };

    #endregion // Fields and constants


    #region Constructors

    public ParameterTile()
    {
        Typography.SetRole(_label, TextRole.Badge);
        _label.FontWeight = FontWeight.Normal;
        _label[!TextBlock.ForegroundProperty] = _label.GetResourceObservable(TokenKeys.TEXT_SECONDARY_BRUSH).ToBinding();
        Typography.SetRole(_value, TextRole.Mono);

        Children.Add(new StackPanel { Spacing = 1, Margin = new Thickness(10, 7), Children = { _label, _value } });
        this[!BackgroundProperty] = this.GetResourceObservable(TokenKeys.BG_PANEL_BRUSH).ToBinding();
        _ = new HoverCopy(this, UiText.COPY_VALUE, () => View is { } v ? new AiActionRequest(AiAction.CopyText, v.Value, v.Label) : null);
    }

    #endregion // Constructors


    #region Properties

    public AiParameterView? View
    {
        get => GetValue(ViewProperty);
        set => SetValue(ViewProperty, value);
    }

    #endregion // Properties


    #region Protected methods

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != ViewProperty) return;

        _label.Text = View?.Label;
        _value.Text = View?.Value;
        ToolTip.SetTip(_value, View?.Value);
    }

    #endregion // Protected methods
}


/// <summary>
/// One model of the AI card (AI addendum mrow): tile 30, name Medium, kind and hash, the weight chip, a copy button on hover for the hash; a line above every row but the first.
/// </summary>
public sealed class ModelRow : Panel
{
    #region Fields and constants

    public static readonly StyledProperty<AiModelView?> ViewProperty =
        AvaloniaProperty.Register<ModelRow, AiModelView?>(nameof(View));

    private readonly SquircleBorder _tile;
    private readonly GlassIcon _icon = new() { Width = 16, Height = 16 };
    private readonly TextBlock _name = new() { TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _meta = new() { TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _weight = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly SquircleBorder _weightChip;
    private readonly Border _line = new() { Height = 1, VerticalAlignment = VerticalAlignment.Top };

    #endregion // Fields and constants


    #region Constructors

    public ModelRow()
    {
        MinHeight = 44;
        _tile = new SquircleBorder { Width = 30, Height = 30, CornerRadius = new CornerRadius(12), Child = _icon, VerticalAlignment = VerticalAlignment.Center };
        Typography.SetRole(_name, TextRole.Caption);
        _name.FontWeight = FontWeight.Medium;
        Typography.SetRole(_meta, TextRole.Badge);
        _meta.FontWeight = FontWeight.Normal;
        _meta[!TextBlock.ForegroundProperty] = _meta.GetResourceObservable(TokenKeys.TEXT_SECONDARY_BRUSH).ToBinding();
        Typography.SetRole(_weight, TextRole.Mono);
        _weight[!TextBlock.ForegroundProperty] = _weight.GetResourceObservable(TokenKeys.ACCENT2_TEXT_BRUSH).ToBinding();
        _weightChip = new SquircleBorder { Height = 20, Padding = new Thickness(6, 0), CornerRadius = new CornerRadius(9), Child = _weight, VerticalAlignment = VerticalAlignment.Center };
        _weightChip[!SquircleBorder.BackgroundProperty] = _weightChip.GetResourceObservable(TokenKeys.ACCENT2_A08_BRUSH).ToBinding();

        var text = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center, Children = { _name, _meta } };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("30,*,Auto"), ColumnSpacing = 10, Margin = new Thickness(8, 6, 32, 6) };
        Grid.SetColumn(text, 1);
        Grid.SetColumn(_weightChip, 2);
        grid.Children.AddRange([_tile, text, _weightChip]);
        _line[!Border.BackgroundProperty] = _line.GetResourceObservable(TokenKeys.EMPHASIS_A07_BRUSH).ToBinding();
        Children.AddRange([_line, grid]);

        Background = Brushes.Transparent;
        _ = new HoverCopy(this, UiText.COPY_HASH, () => View is { Hash: { } hash } v ? new AiActionRequest(AiAction.CopyText, hash, UiText.CopiedHash(v.Name)) : null, centred: true);
    }

    #endregion // Constructors


    #region Properties

    public AiModelView? View
    {
        get => GetValue(ViewProperty);
        set => SetValue(ViewProperty, value);
    }

    #endregion // Properties


    #region Protected methods

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != ViewProperty || View is not { } view) return;

        _name.Text = view.Name;
        ToolTip.SetTip(_name, view.Name);
        _meta.Text = view.Meta;
        _icon.Icon = view.Icon;
        _weight.Text = view.Weight;
        _weightChip.IsVisible = view.Weight is not null;
        _line.IsVisible = !view.IsFirst;

        // LoRAs and embeddings in Accent2, the others neutral (AI addendum)
        _tile[!SquircleBorder.BackgroundProperty] = _tile.GetResourceObservable(view.IsAccent ? TokenKeys.ACCENT2_A10_BRUSH : TokenKeys.EMPHASIS_A06_BRUSH).ToBinding();
        _icon[!GlassIcon.ForegroundProperty] = _icon.GetResourceObservable(view.IsAccent ? TokenKeys.ACCENT2_TEXT_BRUSH : TokenKeys.TEXT_SECONDARY_BRUSH).ToBinding();
    }

    #endregion // Protected methods
}
