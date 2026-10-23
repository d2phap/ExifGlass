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
using Avalonia.Media;
using Avalonia.Threading;
using ExifGlass.Core.Ai;
using ExifGlass.Core.Editing;
using ExifGlass.Core.Metadata;
using ExifGlass.UI.Motion;
using ExifGlass.UI.Tokens;
using System.Globalization;
using System.Windows.Input;

namespace ExifGlass.UI.Controls;

/// <summary>
/// What the selected-tag card shows for one tag: the value as staged, the read-only rule and the edit state.
/// </summary>
public sealed record SelectedTagView(
    string Key,
    string Group,
    string TagId,
    string Name,
    string Value,
    string Raw,
    string Format,
    string ArgumentKey,
    bool IsReadOnly,
    EditState State = EditState.None,
    string? OriginalValue = null,
    bool IsAi = false)
{
    #region Properties

    public bool IsDeleted => State == EditState.Deleted;

    public bool IsEdited => State == EditState.Edited;

    /// <summary>
    /// Gets "Was: ..." for an edited value.
    /// </summary>
    public string WasText => string.Format(CultureInfo.InvariantCulture, UiText.WAS, OriginalValue);

    #endregion // Properties
}


/// <summary>
/// The selected-tag card (DS 6.11, S4): group chip, Tag ID, Delete or Undo and Close; the name; the value well with Copy; Raw (-n), Format and Tag key; Edit, and the edit mode's Cancel and Apply.
/// </summary>
public class SelectedTagCard : TemplatedControl
{
    #region Fields and constants

    public static readonly StyledProperty<SelectedTagView?> ViewProperty =
        AvaloniaProperty.Register<SelectedTagCard, SelectedTagView?>(nameof(View));

    public static readonly StyledProperty<bool> IsEditingProperty =
        AvaloniaProperty.Register<SelectedTagCard, bool>(nameof(IsEditing));

    public static readonly StyledProperty<string?> EditTextProperty =
        AvaloniaProperty.Register<SelectedTagCard, string?>(nameof(EditText), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<ICommand?> CloseCommandProperty =
        AvaloniaProperty.Register<SelectedTagCard, ICommand?>(nameof(CloseCommand));

    public static readonly StyledProperty<ICommand?> CopyValueCommandProperty =
        AvaloniaProperty.Register<SelectedTagCard, ICommand?>(nameof(CopyValueCommand));

    public static readonly StyledProperty<ICommand?> CopyKeyCommandProperty =
        AvaloniaProperty.Register<SelectedTagCard, ICommand?>(nameof(CopyKeyCommand));

    public static readonly StyledProperty<ICommand?> DeleteCommandProperty =
        AvaloniaProperty.Register<SelectedTagCard, ICommand?>(nameof(DeleteCommand));

    public static readonly StyledProperty<ICommand?> EditCommandProperty =
        AvaloniaProperty.Register<SelectedTagCard, ICommand?>(nameof(EditCommand));

    public static readonly StyledProperty<ICommand?> ApplyCommandProperty =
        AvaloniaProperty.Register<SelectedTagCard, ICommand?>(nameof(ApplyCommand));

    public static readonly StyledProperty<ICommand?> CancelCommandProperty =
        AvaloniaProperty.Register<SelectedTagCard, ICommand?>(nameof(CancelCommand));

    public static readonly StyledProperty<ICommand?> RevertCommandProperty =
        AvaloniaProperty.Register<SelectedTagCard, ICommand?>(nameof(RevertCommand));

    private TextBox? _editor;
    private Button? _edit;
    private Button? _close;

    #endregion // Fields and constants


    #region Constructors

    public SelectedTagCard()
    {
        UpdatePseudoClasses();
    }

    #endregion // Constructors


    #region Properties

    public SelectedTagView? View
    {
        get => GetValue(ViewProperty);
        set => SetValue(ViewProperty, value);
    }

    public bool IsEditing
    {
        get => GetValue(IsEditingProperty);
        set => SetValue(IsEditingProperty, value);
    }

    public string? EditText
    {
        get => GetValue(EditTextProperty);
        set => SetValue(EditTextProperty, value);
    }

    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    public ICommand? CopyValueCommand
    {
        get => GetValue(CopyValueCommandProperty);
        set => SetValue(CopyValueCommandProperty, value);
    }

    public ICommand? CopyKeyCommand
    {
        get => GetValue(CopyKeyCommandProperty);
        set => SetValue(CopyKeyCommandProperty, value);
    }

    /// <summary>
    /// Stages a delete, or undoes a staged one.
    /// </summary>
    public ICommand? DeleteCommand
    {
        get => GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    public ICommand? EditCommand
    {
        get => GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    public ICommand? ApplyCommand
    {
        get => GetValue(ApplyCommandProperty);
        set => SetValue(ApplyCommandProperty, value);
    }

    public ICommand? CancelCommand
    {
        get => GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }

    public ICommand? RevertCommand
    {
        get => GetValue(RevertCommandProperty);
        set => SetValue(RevertCommandProperty, value);
    }

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Plays the inspector's entry: fade and a 12 px rise in 400 ms (DS 5.2).
    /// </summary>
    public void Appear() => Card.PlayAppear(this);


    /// <summary>
    /// Moves keyboard focus into the card: Edit when there is one, else Close (Appendix B item 10: Enter focuses the inspector).
    /// </summary>
    public bool FocusFirst()
    {
        if (_edit is { IsEffectivelyVisible: true } && _edit.Focus(NavigationMethod.Tab)) return true;

        return _close?.Focus(NavigationMethod.Tab) == true;
    }

    #endregion // Public methods


    #region Protected methods

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        _editor?.RemoveHandler(KeyDownEvent, Editor_KeyDown);

        base.OnApplyTemplate(e);

        _editor = e.NameScope.Find<TextBox>("PART_Editor");
        _editor?.AddHandler(KeyDownEvent, Editor_KeyDown, RoutingStrategies.Tunnel);
        _edit = e.NameScope.Find<Button>("PART_Edit");
        _close = e.NameScope.Find<Button>("PART_Close");
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ViewProperty || change.Property == IsEditingProperty) UpdatePseudoClasses();

        // DS 6.2: the editor starts focused with its text selected
        if (change.Property == IsEditingProperty && IsEditing)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_editor is null || !IsEditing) return;

                _editor.Focus(NavigationMethod.Tab);
                _editor.SelectAll();
            }, DispatcherPriority.Loaded);
        }
    }

    #endregion // Protected methods


    #region Private methods

    private void UpdatePseudoClasses()
    {
        var tag = View;
        PseudoClasses.Set(":readonly", tag?.IsReadOnly ?? true);
        PseudoClasses.Set(":deleted", tag?.IsDeleted ?? false);
        PseudoClasses.Set(":edited", tag?.IsEdited ?? false);
        PseudoClasses.Set(":ai", tag?.IsAi ?? false);
        PseudoClasses.Set(":editing", IsEditing);
    }

    #endregion // Private methods


    #region Event handlers

    // Ctrl+Enter applies; Esc cancels here first, before the window's Esc chain
    private void Editor_KeyDown(object? sender, KeyEventArgs e)
    {
        var command = e.Key switch
        {
            Key.Enter when e.KeyModifiers == (OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control) => ApplyCommand,
            Key.Escape when e.KeyModifiers == KeyModifiers.None => CancelCommand,
            _ => null,
        };
        if (command is null) return;

        if (command.CanExecute(null)) command.Execute(null);
        e.Handled = true;
    }

    #endregion // Event handlers
}


/// <summary>
/// One parameter tile of the AI card: a label and a mono value.
/// </summary>
public sealed record AiParameterView(string Label, string Value);


/// <summary>
/// The AI generation card as the spec draws it (DS 6.11, S4): sparkle tile, generator badge, the prompt clamped to 4 lines with Show more and Copy, the negative prompt in 2, a 2 x N parameter grid and Copy generation parameters. P7 extends it.
/// </summary>
public class AiCard : TemplatedControl
{
    #region Fields and constants

    // DS 6.11: the first eight parameters
    private const int SHOWN_PARAMETERS = 8;

    public static readonly StyledProperty<AiGenerationInfo?> InfoProperty =
        AvaloniaProperty.Register<AiCard, AiGenerationInfo?>(nameof(Info));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<AiCard, bool>(nameof(IsExpanded));

    public static readonly StyledProperty<ICommand?> CopyPromptCommandProperty =
        AvaloniaProperty.Register<AiCard, ICommand?>(nameof(CopyPromptCommand));

    public static readonly StyledProperty<ICommand?> CopyParametersCommandProperty =
        AvaloniaProperty.Register<AiCard, ICommand?>(nameof(CopyParametersCommand));

    public static readonly DirectProperty<AiCard, string?> GeneratorProperty =
        AvaloniaProperty.RegisterDirect<AiCard, string?>(nameof(Generator), c => c.Generator);

    public static readonly DirectProperty<AiCard, string?> PromptProperty =
        AvaloniaProperty.RegisterDirect<AiCard, string?>(nameof(Prompt), c => c.Prompt);

    public static readonly DirectProperty<AiCard, string?> NegativePromptProperty =
        AvaloniaProperty.RegisterDirect<AiCard, string?>(nameof(NegativePrompt), c => c.NegativePrompt);

    public static readonly DirectProperty<AiCard, IReadOnlyList<AiParameterView>> ParametersProperty =
        AvaloniaProperty.RegisterDirect<AiCard, IReadOnlyList<AiParameterView>>(nameof(Parameters), c => c.Parameters);

    private string? _generator;
    private string? _prompt;
    private string? _negative;
    private IReadOnlyList<AiParameterView> _parameters = [];
    private Button? _more;
    private PromptText? _promptText;

    #endregion // Fields and constants


    #region Constructors

    public AiCard()
    {
        UpdatePseudoClasses();
    }

    #endregion // Constructors


    #region Properties

    public AiGenerationInfo? Info
    {
        get => GetValue(InfoProperty);
        set => SetValue(InfoProperty, value);
    }

    /// <summary>
    /// Shows the whole prompt (Show more).
    /// </summary>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public ICommand? CopyPromptCommand
    {
        get => GetValue(CopyPromptCommandProperty);
        set => SetValue(CopyPromptCommandProperty, value);
    }

    public ICommand? CopyParametersCommand
    {
        get => GetValue(CopyParametersCommandProperty);
        set => SetValue(CopyParametersCommandProperty, value);
    }

    /// <summary>
    /// Gets the badge: "SD WebUI v1.10.1".
    /// </summary>
    public string? Generator
    {
        get => _generator;
        private set => SetAndRaise(GeneratorProperty, ref _generator, value);
    }

    public string? Prompt
    {
        get => _prompt;
        private set => SetAndRaise(PromptProperty, ref _prompt, value);
    }

    public string? NegativePrompt
    {
        get => _negative;
        private set => SetAndRaise(NegativePromptProperty, ref _negative, value);
    }

    public IReadOnlyList<AiParameterView> Parameters
    {
        get => _parameters;
        private set => SetAndRaise(ParametersProperty, ref _parameters, value);
    }

    #endregion // Properties


    #region Public methods

    public void Appear() => Card.PlayAppear(this);


    /// <summary>
    /// The generator badge's text: the name and its version.
    /// </summary>
    public static string GeneratorLabel(AiGenerationInfo info)
    {
        var name = info.Generator.Length > 0 ? info.Generator : AiGenerationInfo.KindLabel(info.Kind);
        return info.GeneratorVersion is { Length: > 0 } version ? $"{name} {version}" : name;
    }

    #endregion // Public methods


    #region Protected methods

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_more is not null) _more.Click -= More_Click;
        if (_promptText is not null) _promptText.PropertyChanged -= Prompt_PropertyChanged;

        base.OnApplyTemplate(e);

        _more = e.NameScope.Find<Button>("PART_More");
        _promptText = e.NameScope.Find<PromptText>("PART_Prompt");
        if (_more is not null) _more.Click += More_Click;
        if (_promptText is not null) _promptText.PropertyChanged += Prompt_PropertyChanged;
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsExpandedProperty) PseudoClasses.Set(":expanded", IsExpanded);
        else if (change.Property == InfoProperty)
        {
            var info = Info;
            Generator = info is null ? null : GeneratorLabel(info);
            Prompt = info?.Prompt?.Text;
            NegativePrompt = info?.NegativePrompt?.Text;
            Parameters = info is null ? [] : [.. info.Parameters.Take(SHOWN_PARAMETERS).Select(p => new AiParameterView(p.Label, p.Value))];
            IsExpanded = false;
            UpdatePseudoClasses();
        }
    }

    #endregion // Protected methods


    #region Private methods

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":prompt", !string.IsNullOrEmpty(Prompt));
        PseudoClasses.Set(":negative", !string.IsNullOrEmpty(NegativePrompt));
        PseudoClasses.Set(":parameters", Parameters.Count > 0);

        // provenance alone (a C2PA manifest) has no generation parameters to copy; P7 shows it
        PseudoClasses.Set(":copyable", !string.IsNullOrEmpty(Prompt) || Parameters.Count > 0);
    }

    #endregion // Private methods


    #region Event handlers

    private void More_Click(object? sender, RoutedEventArgs e) => IsExpanded = !IsExpanded;


    // Show more only when the clamp hides text (PromptText measures it)
    private void Prompt_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == PromptText.IsClampedProperty) PseudoClasses.Set(":clamped", _promptText?.IsClamped == true);
    }

    #endregion // Event handlers
}


/// <summary>
/// Tiles in two columns with 1 px gaps that show the panel's background as lines (DS 6.11 parameter grid, Exposure); a lone last tile takes the whole row.
/// </summary>
public class TileGrid : Panel
{
    #region Fields and constants

    private const double GAP = 1;

    #endregion // Fields and constants


    #region Protected methods

    protected override Size MeasureOverride(Size availableSize)
    {
        var half = Half(availableSize.Width);
        double height = 0;
        for (var i = 0; i < Children.Count; i += 2)
        {
            var lone = i == Children.Count - 1;
            Children[i].Measure(new Size(lone ? availableSize.Width : half, double.PositiveInfinity));
            var row = Children[i].DesiredSize.Height;
            if (!lone)
            {
                Children[i + 1].Measure(new Size(half, double.PositiveInfinity));
                row = Math.Max(row, Children[i + 1].DesiredSize.Height);
            }

            height += row + (i > 0 ? GAP : 0);
        }

        return new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, height);
    }


    protected override Size ArrangeOverride(Size finalSize)
    {
        var half = Half(finalSize.Width);
        double y = 0;
        for (var i = 0; i < Children.Count; i += 2)
        {
            var lone = i == Children.Count - 1;
            var row = lone ? Children[i].DesiredSize.Height : Math.Max(Children[i].DesiredSize.Height, Children[i + 1].DesiredSize.Height);
            Children[i].Arrange(new Rect(0, y, lone ? finalSize.Width : half, row));
            if (!lone) Children[i + 1].Arrange(new Rect(half + GAP, y, finalSize.Width - half - GAP, row));
            y += row + GAP;
        }

        return finalSize;
    }

    #endregion // Protected methods


    #region Private methods

    // whole pixels on the left so the 1 px line stays crisp
    private static double Half(double width) => double.IsInfinity(width) ? double.PositiveInfinity : Math.Floor((width - GAP) / 2);

    #endregion // Private methods
}


/// <summary>
/// The Frame card's box (Proto): min(MaxBoxWidth, MaxBoxHeight x aspect) wide, never wider than its room, with the aspect's height; a child with an Anchor is centred on that point of the box.
/// </summary>
public class FrameBox : Panel
{
    #region Fields and constants

    public static readonly StyledProperty<double> AspectProperty =
        AvaloniaProperty.Register<FrameBox, double>(nameof(Aspect), 1.5);

    public static readonly StyledProperty<double> MaxBoxWidthProperty =
        AvaloniaProperty.Register<FrameBox, double>(nameof(MaxBoxWidth), 278);

    public static readonly StyledProperty<double> MaxBoxHeightProperty =
        AvaloniaProperty.Register<FrameBox, double>(nameof(MaxBoxHeight), 220);

    /// <summary>
    /// A point of the box in 0..1 that the child centres on: the autofocus point.
    /// </summary>
    public static readonly AttachedProperty<Point?> AnchorProperty =
        AvaloniaProperty.RegisterAttached<FrameBox, Control, Point?>("Anchor");

    #endregion // Fields and constants


    #region Constructors

    static FrameBox()
    {
        AffectsMeasure<FrameBox>(AspectProperty, MaxBoxWidthProperty, MaxBoxHeightProperty);
        AffectsParentArrange<FrameBox>(AnchorProperty);
    }

    #endregion // Constructors


    #region Properties

    public double Aspect
    {
        get => GetValue(AspectProperty);
        set => SetValue(AspectProperty, value);
    }

    public double MaxBoxWidth
    {
        get => GetValue(MaxBoxWidthProperty);
        set => SetValue(MaxBoxWidthProperty, value);
    }

    public double MaxBoxHeight
    {
        get => GetValue(MaxBoxHeightProperty);
        set => SetValue(MaxBoxHeightProperty, value);
    }

    #endregion // Properties


    #region Public methods

    public static Point? GetAnchor(Control element) => element.GetValue(AnchorProperty);

    public static void SetAnchor(Control element, Point? value) => element.SetValue(AnchorProperty, value);

    #endregion // Public methods


    #region Protected methods

    protected override Size MeasureOverride(Size availableSize)
    {
        var aspect = Aspect > 0 && double.IsFinite(Aspect) ? Aspect : 1.5;
        var width = Math.Min(Math.Min(MaxBoxWidth, MaxBoxHeight * aspect), availableSize.Width);
        var size = new Size(width, width / aspect);
        foreach (var child in Children) child.Measure(GetAnchor(child) is null ? size : Size.Infinity);

        return size;
    }


    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            if (GetAnchor(child) is { } anchor)
            {
                var desired = child.DesiredSize;
                child.Arrange(new Rect(new Point(anchor.X * finalSize.Width - desired.Width / 2, anchor.Y * finalSize.Height - desired.Height / 2), desired));
            }
            else child.Arrange(new Rect(finalSize));
        }

        return finalSize;
    }

    #endregion // Protected methods
}


/// <summary>
/// Rule-of-thirds lines (Proto Frame placeholder): two vertical and two horizontal 1 px lines.
/// </summary>
public class ThirdsLines : Control
{
    #region Fields and constants

    public static readonly StyledProperty<IBrush?> BrushProperty =
        AvaloniaProperty.Register<ThirdsLines, IBrush?>(nameof(Brush));

    #endregion // Fields and constants


    #region Constructors

    static ThirdsLines()
    {
        AffectsRender<ThirdsLines>(BrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ThirdsLines>(false);
    }

    #endregion // Constructors


    #region Properties

    public IBrush? Brush
    {
        get => GetValue(BrushProperty);
        set => SetValue(BrushProperty, value);
    }

    #endregion // Properties


    #region Public methods

    public override void Render(DrawingContext context)
    {
        if (Brush is not { } brush) return;

        var (w, h) = (Bounds.Width, Bounds.Height);
        for (var i = 1; i <= 2; i++)
        {
            context.FillRectangle(brush, new Rect(Math.Round(w * i / 3), 0, 1, h));
            context.FillRectangle(brush, new Rect(0, Math.Round(h * i / 3), w, 1));
        }
    }

    #endregion // Public methods
}


/// <summary>
/// The Frame card (DS 6.11): the size, then the preview as cover, or a rule-of-thirds placeholder with the autofocus point and the ratio and megapixel chips.
/// </summary>
public class FrameCard : TemplatedControl
{
    #region Fields and constants

    public static readonly StyledProperty<FrameInfo?> FrameProperty =
        AvaloniaProperty.Register<FrameCard, FrameInfo?>(nameof(Frame));

    public static readonly StyledProperty<IImage?> PreviewProperty =
        AvaloniaProperty.Register<FrameCard, IImage?>(nameof(Preview));

    public static readonly DirectProperty<FrameCard, string> DimensionsProperty =
        AvaloniaProperty.RegisterDirect<FrameCard, string>(nameof(Dimensions), c => c.Dimensions);

    public static readonly DirectProperty<FrameCard, double> AspectProperty =
        AvaloniaProperty.RegisterDirect<FrameCard, double>(nameof(Aspect), c => c.Aspect);

    public static readonly DirectProperty<FrameCard, string> RatioProperty =
        AvaloniaProperty.RegisterDirect<FrameCard, string>(nameof(Ratio), c => c.Ratio);

    public static readonly DirectProperty<FrameCard, string> MegapixelsProperty =
        AvaloniaProperty.RegisterDirect<FrameCard, string>(nameof(Megapixels), c => c.Megapixels);

    public static readonly DirectProperty<FrameCard, Point?> FocusPointProperty =
        AvaloniaProperty.RegisterDirect<FrameCard, Point?>(nameof(FocusPoint), c => c.FocusPoint);

    private string _dimensions = TagIdRule.NONE;
    private double _aspect = 1.5;
    private string _ratio = TagIdRule.NONE;
    private string _megapixels = "— MP";
    private Point? _focusPoint;
    private Control? _image;

    #endregion // Fields and constants


    #region Constructors

    public FrameCard()
    {
        UpdatePseudoClasses();
    }

    #endregion // Constructors


    #region Properties

    public FrameInfo? Frame
    {
        get => GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    /// <summary>
    /// The preview, already upright; it sets the box's shape.
    /// </summary>
    public IImage? Preview
    {
        get => GetValue(PreviewProperty);
        set => SetValue(PreviewProperty, value);
    }

    /// <summary>
    /// Gets "7728 × 5152", "Unknown size", or "—" without a file.
    /// </summary>
    public string Dimensions
    {
        get => _dimensions;
        private set => SetAndRaise(DimensionsProperty, ref _dimensions, value);
    }

    public double Aspect
    {
        get => _aspect;
        private set => SetAndRaise(AspectProperty, ref _aspect, value);
    }

    public string Ratio
    {
        get => _ratio;
        private set => SetAndRaise(RatioProperty, ref _ratio, value);
    }

    public string Megapixels
    {
        get => _megapixels;
        private set => SetAndRaise(MegapixelsProperty, ref _megapixels, value);
    }

    /// <summary>
    /// Gets the autofocus point in 0..1 of the frame, or null.
    /// </summary>
    public Point? FocusPoint
    {
        get => _focusPoint;
        private set => SetAndRaise(FocusPointProperty, ref _focusPoint, value);
    }

    #endregion // Properties


    #region Protected methods

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _image = e.NameScope.Find<Control>("PART_Image");
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != FrameProperty && change.Property != PreviewProperty) return;

        Update();
        if (change.Property == PreviewProperty && change.NewValue is not null && _image is not null)
        {
            CompositionMotion.Fade(_image, 0, 1, MotionTokens.Fade, MotionTokens.FadeEasing);
        }
    }

    #endregion // Protected methods


    #region Private methods

    private void Update()
    {
        var frame = Frame;
        var (w, h) = (frame?.Width ?? 0, frame?.Height ?? 0);

        Dimensions = frame is null ? TagIdRule.NONE : w > 0 && h > 0 ? string.Create(CultureInfo.InvariantCulture, $"{w} × {h}") : "Unknown size";
        Ratio = frame?.RatioLabel ?? TagIdRule.NONE;
        Megapixels = frame is { Megapixels: > 0 } ? frame.Megapixels.ToString("0.0", CultureInfo.InvariantCulture) + " MP" : "— MP";
        FocusPoint = frame is { FocusX: { } fx, FocusY: { } fy } ? new Point(Math.Clamp(fx / 100, 0, 1), Math.Clamp(fy / 100, 0, 1)) : null;

        // the preview is upright, so its own shape wins over the stored size
        Aspect = Preview is { Size: { Width: > 0, Height: > 0 } size } ? size.Width / size.Height : w > 0 && h > 0 ? (double)w / h : 1.5;
        UpdatePseudoClasses();
    }


    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":preview", Preview is not null);
        PseudoClasses.Set(":focus-point", FocusPoint is not null);
    }

    #endregion // Private methods
}


/// <summary>
/// One Exposure readout (DS 6.11): its label, the number that counts up, and a sub-label; "—" when the file has none.
/// </summary>
public sealed record ExposureCellView(string Label, double Value, CountUpFormat Format, string? Prefix, string? Suffix, string Sub)
{
    #region Properties

    public bool HasValue => double.IsFinite(Value);

    #endregion // Properties
}


/// <summary>
/// The Exposure card (DS 6.11): Aperture, Shutter, ISO and Focal in a 2 x 2 grid, Display 22 Medium, counting up over 1100 ms.
/// </summary>
public class ExposureCard : TemplatedControl
{
    #region Fields and constants

    private static readonly string[] LABELS = ["Aperture", "Shutter", "ISO", "Focal"];

    public static readonly StyledProperty<IReadOnlyList<ExposureItem>?> ItemsProperty =
        AvaloniaProperty.Register<ExposureCard, IReadOnlyList<ExposureItem>?>(nameof(Items));

    public static readonly DirectProperty<ExposureCard, IReadOnlyList<ExposureCellView>> CellsProperty =
        AvaloniaProperty.RegisterDirect<ExposureCard, IReadOnlyList<ExposureCellView>>(nameof(Cells), c => c.Cells);

    private IReadOnlyList<ExposureCellView> _cells = BuildCells(null);

    #endregion // Fields and constants


    #region Properties

    public IReadOnlyList<ExposureItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>
    /// Gets the four readouts; a new list makes new cells, so a new file counts up from zero.
    /// </summary>
    public IReadOnlyList<ExposureCellView> Cells
    {
        get => _cells;
        private set => SetAndRaise(CellsProperty, ref _cells, value);
    }

    #endregion // Properties


    #region Public methods

    public static IReadOnlyList<ExposureCellView> BuildCells(IReadOnlyList<ExposureItem>? items)
    {
        var cells = new ExposureCellView[LABELS.Length];
        for (var i = 0; i < LABELS.Length; i++)
        {
            var item = items?.FirstOrDefault(x => x.Label == LABELS[i]);
            if (item is null || !double.IsFinite(item.Number))
            {
                cells[i] = new ExposureCellView(LABELS[i].ToUpperInvariant(), double.NaN, CountUpFormat.Integer, null, null, item?.Sub ?? string.Empty);
                continue;
            }

            // DerivedInfo writes a shutter as "1/" and a whole denominator, which counts as 1/N
            var format = item.Prefix == "1/" ? CountUpFormat.Shutter : item.Decimals > 0 ? CountUpFormat.OneDecimal : CountUpFormat.Integer;
            var prefix = format == CountUpFormat.Shutter ? null : item.Prefix;
            cells[i] = new ExposureCellView(LABELS[i].ToUpperInvariant(), item.Number, format, prefix, item.Suffix, item.Sub);
        }

        return cells;
    }

    #endregion // Public methods


    #region Protected methods

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsProperty) Cells = BuildCells(Items);
    }

    #endregion // Protected methods
}


/// <summary>
/// One Details row: icon, label, value, an optional sub-label and link; the first has no line above it.
/// </summary>
public sealed record DetailRowView(IconId Icon, string Label, string Value, string Sub, string? Link, bool IsFirst)
{
    #region Properties

    public bool HasSub => Sub.Length > 0;

    public bool HasLink => Link is not null;

    #endregion // Properties
}


/// <summary>
/// The Details card (DS 6.11): Camera, Lens, Captured, Location (+ Open in OpenStreetMap) and Software; skeleton rows without a file; "No camera data in this file" when there are none.
/// </summary>
public class DetailsCard : TemplatedControl
{
    #region Fields and constants

    private static readonly (string Label, IconId Icon)[] ROWS =
    [
        ("Camera", IconId.Camera), ("Lens", IconId.Aperture), ("Captured", IconId.Clock), ("Location", IconId.Location), ("Software", IconId.WindowConsole),
    ];

    public static readonly StyledProperty<IReadOnlyList<DetailItem>?> ItemsProperty =
        AvaloniaProperty.Register<DetailsCard, IReadOnlyList<DetailItem>?>(nameof(Items));

    public static readonly StyledProperty<ICommand?> OpenLinkCommandProperty =
        AvaloniaProperty.Register<DetailsCard, ICommand?>(nameof(OpenLinkCommand));

    public static readonly DirectProperty<DetailsCard, IReadOnlyList<DetailRowView>> RowsProperty =
        AvaloniaProperty.RegisterDirect<DetailsCard, IReadOnlyList<DetailRowView>>(nameof(Rows), c => c.Rows);

    private IReadOnlyList<DetailRowView> _rows = [];

    #endregion // Fields and constants


    #region Constructors

    public DetailsCard()
    {
        UpdatePseudoClasses();
    }

    #endregion // Constructors


    #region Properties

    /// <summary>
    /// The document's details; null without a file, which shows the skeleton rows.
    /// </summary>
    public IReadOnlyList<DetailItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>
    /// Opens a row's link (the map), with the link as its parameter.
    /// </summary>
    public ICommand? OpenLinkCommand
    {
        get => GetValue(OpenLinkCommandProperty);
        set => SetValue(OpenLinkCommandProperty, value);
    }

    public IReadOnlyList<DetailRowView> Rows
    {
        get => _rows;
        private set => SetAndRaise(RowsProperty, ref _rows, value);
    }

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// The rows the card shows (Proto): known labels in the spec's order with their icons, else one "no camera data" row.
    /// </summary>
    public static IReadOnlyList<DetailRowView> BuildRows(IReadOnlyList<DetailItem> items)
    {
        var rows = new List<DetailRowView>(ROWS.Length);
        foreach (var item in items)
        {
            var icon = Array.FindIndex(ROWS, r => r.Label == item.Label);
            if (icon >= 0) rows.Add(new DetailRowView(ROWS[icon].Icon, item.Label, item.Value, item.Sub, item.Link, rows.Count == 0));
        }

        return rows.Count > 0 ? rows : [new DetailRowView(IconId.Camera, ROWS[0].Label, UiText.NO_CAMERA_DATA, string.Empty, null, true)];
    }

    #endregion // Public methods


    #region Protected methods

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != ItemsProperty) return;

        Rows = Items is { } items ? BuildRows(items) : [];
        UpdatePseudoClasses();
    }

    #endregion // Protected methods


    #region Private methods

    private void UpdatePseudoClasses() => PseudoClasses.Set(":loading", Items is null);

    #endregion // Private methods
}
