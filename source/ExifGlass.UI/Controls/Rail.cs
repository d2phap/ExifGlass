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
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using ExifGlass.UI.Motion;
using ExifGlass.UI.Tokens;

namespace ExifGlass.UI.Controls;

/// <summary>
/// One group in the composition bar: its tag count, or nothing while it is hidden.
/// </summary>
public sealed record GroupSegment(string Group, int Count, bool IsShown);


/// <summary>
/// The rail's composition bar (DS 6.10): 6 px, segments by tag count with 2 px gaps in a track clipped to radius 2; they grow over 1100 ms, staggered on a new file.
/// </summary>
public class CompositionBar : Control
{
    #region Fields and constants

    private const double GAP = 2;
    private const double RADIUS = 2;

    // Proto: a hidden group keeps a sliver of weight; the gap closes with it
    private const double MIN_WEIGHT = 0.5;

    private static readonly TimeSpan STAGGER_START = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan STAGGER_STEP = TimeSpan.FromMilliseconds(60);

    public static readonly StyledProperty<IReadOnlyList<GroupSegment>?> SegmentsProperty =
        AvaloniaProperty.Register<CompositionBar, IReadOnlyList<GroupSegment>?>(nameof(Segments));

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<CompositionBar, IBrush?>(nameof(TrackBrush));

    private string[] _groups = [];
    private double[] _weights = [];
    private Choreo? _motion;
    private Geometry? _clip;
    private Size _clipSize;

    #endregion // Fields and constants


    #region Constructors

    static CompositionBar()
    {
        AffectsRender<CompositionBar>(TrackBrushProperty);
        HeightProperty.OverrideDefaultValue<CompositionBar>(6);
    }


    public CompositionBar()
    {
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    #endregion // Constructors


    #region Properties

    public IReadOnlyList<GroupSegment>? Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    /// <summary>
    /// Gets the segments' current weights, for tests.
    /// </summary>
    public IReadOnlyList<double> Weights => _weights;

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Grows every segment from nothing again, staggered (a new read of the file).
    /// </summary>
    public void Replay() => Animate(new double[_groups.Length], stagger: true);


    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0) return;

        if (_clip is null || _clipSize != size)
        {
            var box = new Rect(size);
            _clip = Squircle.Geometry(box, Squircle.Radii(box, new CornerRadius(RADIUS)), CornerShape.Squircle);
            _clipSize = size;
        }

        using var clip = context.PushGeometryClip(_clip);
        if (TrackBrush is { } track) context.FillRectangle(track, new Rect(size));

        double total = 0, gaps = 0;
        var drawn = false;
        for (var i = 0; i < _weights.Length; i++)
        {
            if (_weights[i] <= 0) continue;

            total += _weights[i];
            if (drawn) gaps += GAP * Presence(i);
            drawn = true;
        }

        if (total <= 0) return;

        var room = Math.Max(0, size.Width - gaps);
        var dark = ActualThemeVariant == ThemeVariant.Dark;
        var x = 0.0;
        drawn = false;
        for (var i = 0; i < _weights.Length; i++)
        {
            if (_weights[i] <= 0) continue;

            if (drawn) x += GAP * Presence(i);
            drawn = true;
            var width = room * _weights[i] / total;
            context.FillRectangle(new ImmutableSolidColorBrush(GroupPalette.Get(_groups[i], dark)), new Rect(x, 0, width, size.Height));
            x += width;
        }
    }

    #endregion // Public methods


    #region Protected methods

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SegmentsProperty) Update();
    }

    #endregion // Protected methods


    #region Private methods

    private double Presence(int i) => Math.Clamp(_weights[i] / MIN_WEIGHT, 0, 1);


    private static double Target(GroupSegment segment) => segment.IsShown ? Math.Max(segment.Count, MIN_WEIGHT) : 0;


    // the same groups glide to their new weights; a new set grows from nothing, staggered (Proto)
    private void Update()
    {
        var segments = Segments ?? [];
        var same = segments.Count == _groups.Length;
        for (var i = 0; same && i < segments.Count; i++) same = segments[i].Group == _groups[i];

        if (same)
        {
            Animate((double[])_weights.Clone(), stagger: false);
            return;
        }

        _groups = [.. segments.Select(s => s.Group)];
        _weights = new double[_groups.Length];
        Animate(new double[_groups.Length], stagger: true);
    }


    private void Animate(double[] from, bool stagger)
    {
        var segments = Segments ?? [];
        if (segments.Count != _groups.Length) return;

        _motion?.Stop(jumpToEnd: false);
        var motion = _motion = new Choreo();
        for (var i = 0; i < segments.Count; i++)
        {
            var index = i;
            var start = from[i];
            var end = Target(segments[i]);
            var begin = stagger ? STAGGER_START + STAGGER_STEP * i : TimeSpan.Zero;
            motion.Add(begin, MotionTokens.CompositionGrow, MotionTokens.CompositionGrowEasing, p =>
            {
                _weights[index] = start + (end - start) * p;
                InvalidateVisual();
            });
        }

        _ = motion.PlayAsync(this);
    }

    #endregion // Private methods
}


/// <summary>
/// The rail's file card (DS 6.10): a 38 px thumbnail (the preview, the extension, or a dashed outline with no file), name, meta and the composition bar.
/// </summary>
public class FileCard : TemplatedControl
{
    #region Fields and constants

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<FileCard, string?>(nameof(Title));

    public static readonly StyledProperty<string?> MetaProperty =
        AvaloniaProperty.Register<FileCard, string?>(nameof(Meta));

    public static readonly StyledProperty<IImage?> PreviewProperty =
        AvaloniaProperty.Register<FileCard, IImage?>(nameof(Preview));

    /// <summary>
    /// The thumbnail's label while there is no preview: "JPEG"; with neither, the outline is dashed.
    /// </summary>
    public static readonly StyledProperty<string?> ExtensionProperty =
        AvaloniaProperty.Register<FileCard, string?>(nameof(Extension));

    public static readonly StyledProperty<IReadOnlyList<GroupSegment>?> SegmentsProperty =
        AvaloniaProperty.Register<FileCard, IReadOnlyList<GroupSegment>?>(nameof(Segments));

    private CompositionBar? _bar;
    private Control? _image;

    #endregion // Fields and constants


    #region Properties

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Meta
    {
        get => GetValue(MetaProperty);
        set => SetValue(MetaProperty, value);
    }

    public IImage? Preview
    {
        get => GetValue(PreviewProperty);
        set => SetValue(PreviewProperty, value);
    }

    public string? Extension
    {
        get => GetValue(ExtensionProperty);
        set => SetValue(ExtensionProperty, value);
    }

    public IReadOnlyList<GroupSegment>? Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    #endregion // Properties


    #region Constructors

    public FileCard()
    {
        UpdatePseudoClasses();
    }

    #endregion // Constructors


    #region Public methods

    /// <summary>
    /// Grows the composition bar again (a new read of the file).
    /// </summary>
    public void Replay() => _bar?.Replay();

    #endregion // Public methods


    #region Protected methods

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _bar = e.NameScope.Find<CompositionBar>("PART_Bar");
        _image = e.NameScope.Find<Control>("PART_Image");
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PreviewProperty || change.Property == ExtensionProperty) UpdatePseudoClasses();

        // a preview arrives after the table: it fades in rather than pop over the label
        if (change.Property == PreviewProperty && change.NewValue is not null && _image is not null)
        {
            CompositionMotion.Fade(_image, 0, 1, MotionTokens.Fade, MotionTokens.FadeEasing);
        }
    }

    #endregion // Protected methods


    #region Private methods

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":preview", Preview is not null);
        PseudoClasses.Set(":empty", Preview is null && string.IsNullOrEmpty(Extension));
    }

    #endregion // Private methods
}


/// <summary>
/// A rail group row (DS 6.10, S2): the checkbox's 36 x 32 area shows or hides the group, the rest jumps to it; one state layer for the whole row.
/// </summary>
public class RailGroupRow : TemplatedControl
{
    #region Fields and constants

    public static readonly StyledProperty<string?> GroupProperty =
        AvaloniaProperty.Register<RailGroupRow, string?>(nameof(Group));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<RailGroupRow, string?>(nameof(Text));

    public static readonly StyledProperty<string?> CountProperty =
        AvaloniaProperty.Register<RailGroupRow, string?>(nameof(Count));

    public static readonly StyledProperty<bool> IsShownProperty =
        AvaloniaProperty.Register<RailGroupRow, bool>(nameof(IsShown), true);

    /// <summary>
    /// The synthetic AI generation group: the sparkle instead of a dot (D22).
    /// </summary>
    public static readonly StyledProperty<bool> IsAiProperty =
        AvaloniaProperty.Register<RailGroupRow, bool>(nameof(IsAi));

    public static readonly StyledProperty<string?> ToggleTipProperty =
        AvaloniaProperty.Register<RailGroupRow, string?>(nameof(ToggleTip));

    public static readonly StyledProperty<string?> JumpTipProperty =
        AvaloniaProperty.Register<RailGroupRow, string?>(nameof(JumpTip));

    public static readonly RoutedEvent<RoutedEventArgs> ToggleRequestedEvent =
        RoutedEvent.Register<RailGroupRow, RoutedEventArgs>(nameof(ToggleRequested), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RoutedEventArgs> JumpRequestedEvent =
        RoutedEvent.Register<RailGroupRow, RoutedEventArgs>(nameof(JumpRequested), RoutingStrategies.Bubble);

    private Button? _check;
    private Button? _jump;

    #endregion // Fields and constants


    #region Constructors

    public RailGroupRow()
    {
        UpdatePseudoClasses();
    }

    #endregion // Constructors


    #region Properties

    public string? Group
    {
        get => GetValue(GroupProperty);
        set => SetValue(GroupProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? Count
    {
        get => GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    public bool IsShown
    {
        get => GetValue(IsShownProperty);
        set => SetValue(IsShownProperty, value);
    }

    public bool IsAi
    {
        get => GetValue(IsAiProperty);
        set => SetValue(IsAiProperty, value);
    }

    public string? ToggleTip
    {
        get => GetValue(ToggleTipProperty);
        set => SetValue(ToggleTipProperty, value);
    }

    public string? JumpTip
    {
        get => GetValue(JumpTipProperty);
        set => SetValue(JumpTipProperty, value);
    }

    #endregion // Properties


    #region Events

    public event EventHandler<RoutedEventArgs>? ToggleRequested
    {
        add => AddHandler(ToggleRequestedEvent, value);
        remove => RemoveHandler(ToggleRequestedEvent, value);
    }

    public event EventHandler<RoutedEventArgs>? JumpRequested
    {
        add => AddHandler(JumpRequestedEvent, value);
        remove => RemoveHandler(JumpRequestedEvent, value);
    }

    #endregion // Events


    #region Protected methods

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsShownProperty || change.Property == IsAiProperty) UpdatePseudoClasses();
    }


    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_check is not null) _check.Click -= Check_Click;
        if (_jump is not null) _jump.Click -= Jump_Click;

        base.OnApplyTemplate(e);

        _check = e.NameScope.Find<Button>("PART_Check");
        _jump = e.NameScope.Find<Button>("PART_Jump");
        if (_check is not null) _check.Click += Check_Click;
        if (_jump is not null) _jump.Click += Jump_Click;
    }

    #endregion // Protected methods


    #region Private methods

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":shown", IsShown);
        PseudoClasses.Set(":ai", IsAi);
    }

    #endregion // Private methods


    #region Event handlers

    private void Check_Click(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(ToggleRequestedEvent, this));


    private void Jump_Click(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(JumpRequestedEvent, this));

    #endregion // Event handlers
}


/// <summary>
/// The rail's footer (DS 6.10): a dashed drop target that opens the picker; its text wraps.
/// </summary>
public class DropTarget : Button
{
    #region Fields and constants

    public static readonly StyledProperty<IconId?> IconProperty =
        AvaloniaProperty.Register<DropTarget, IconId?>(nameof(Icon));

    #endregion // Fields and constants


    #region Properties

    public IconId? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(DropTarget);

    #endregion // Properties
}
