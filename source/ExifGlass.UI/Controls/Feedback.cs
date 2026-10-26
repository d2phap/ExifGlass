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
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using ExifGlass.UI.Motion;
using ExifGlass.UI.Tokens;
using System.Globalization;

namespace ExifGlass.UI.Controls;

/// <summary>
/// A skeleton bar (DS 6.9): Emphasis 8%, radius 4; while pulsing, opacity 0.45 to 1 over 1.2 s on the render thread, staggered 70 ms by Index.
/// </summary>
public class Skeleton : TemplatedControl
{
    #region Fields and constants

    private const float PULSE_LOW = 0.45f;

    public static readonly StyledProperty<bool> IsPulsingProperty =
        AvaloniaProperty.Register<Skeleton, bool>(nameof(IsPulsing));

    public static readonly StyledProperty<int> IndexProperty =
        AvaloniaProperty.Register<Skeleton, int>(nameof(Index));

    #endregion // Fields and constants


    #region Properties

    public bool IsPulsing
    {
        get => GetValue(IsPulsingProperty);
        set => SetValue(IsPulsingProperty, value);
    }

    public int Index
    {
        get => GetValue(IndexProperty);
        set => SetValue(IndexProperty, value);
    }

    #endregion // Properties


    #region Public methods

    // a render writes the visual's opacity, which ends a running opacity animation, so the pulse starts again in the same batch
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (IsPulsing && ElementComposition.GetElementVisual(this) is { } visual) StartPulse(visual);
    }

    #endregion // Public methods


    #region Protected methods

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdatePulse();
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsPulsingProperty || change.Property == IndexProperty) UpdatePulse();
    }

    #endregion // Protected methods


    #region Private methods

    private void UpdatePulse() => CompositionMotion.WhenReady(this, StartPulse);


    private void StartPulse(CompositionVisual visual)
    {
        var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        if (IsPulsing && !MotionTokens.IsFrozen)
        {
            animation.InsertKeyFrame(0f, 1f);
            animation.InsertKeyFrame(0.5f, PULSE_LOW, MotionTokens.StandardEasing);
            animation.InsertKeyFrame(1f, 1f, MotionTokens.StandardEasing);
            animation.Duration = MotionTokens.SkeletonPulse;
            animation.DelayTime = MotionTokens.SkeletonStagger * Index;
            animation.IterationBehavior = AnimationIterationBehavior.Forever;
        }
        else
        {
            animation.InsertKeyFrame(1f, (float)Opacity);
            animation.Duration = TimeSpan.FromMilliseconds(1);
        }

        visual.StartAnimation("Opacity", animation);
    }

    #endregion // Private methods
}


/// <summary>
/// The 2 px accent progress line (DS 6.9): a determinate fill, or a sweeping segment on the render thread while loading.
/// </summary>
public class ProgressLine : TemplatedControl
{
    #region Fields and constants

    private const double SEGMENT = 0.32;
    private static readonly TimeSpan Sweep = TimeSpan.FromMilliseconds(1300);

    public static readonly StyledProperty<bool> IsIndeterminateProperty =
        AvaloniaProperty.Register<ProgressLine, bool>(nameof(IsIndeterminate), true);

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ProgressLine, double>(nameof(Value));

    private Control? _bar;

    #endregion // Fields and constants


    #region Properties

    public bool IsIndeterminate
    {
        get => GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    /// <summary>
    /// 0 to 1 while determinate.
    /// </summary>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    #endregion // Properties


    #region Protected methods

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _bar = e.NameScope.Find<Control>("PART_Bar");
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsIndeterminateProperty || change.Property == ValueProperty || change.Property == BoundsProperty) InvalidateArrange();
    }


    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        if (_bar is null) return size;

        var width = finalSize.Width;
        if (IsIndeterminate)
        {
            _bar.Width = width * SEGMENT;
            StartSweep(width);
        }
        else
        {
            _bar.Width = width * Math.Clamp(Value, 0, 1);
            StopSweep();
        }

        return size;
    }

    #endregion // Protected methods


    #region Private methods

    private void StartSweep(double width)
    {
        if (_bar is null || ElementComposition.GetElementVisual(_bar) is not { } visual) return;

        var animation = visual.Compositor.CreateVector3DKeyFrameAnimation();
        if (MotionTokens.IsFrozen)
        {
            animation.InsertKeyFrame(1f, new Vector3D(width * (1 - SEGMENT) / 2, 0, 0));
            animation.Duration = TimeSpan.FromMilliseconds(1);
        }
        else
        {
            animation.InsertKeyFrame(0f, new Vector3D(-width * SEGMENT, 0, 0));
            animation.InsertKeyFrame(1f, new Vector3D(width, 0, 0), MotionTokens.EmphasizedEasing);
            animation.Duration = Sweep;
            animation.IterationBehavior = AnimationIterationBehavior.Forever;
        }

        visual.StartAnimation("Translation", animation);
    }


    private void StopSweep()
    {
        if (_bar is null || ElementComposition.GetElementVisual(_bar) is not { } visual) return;

        var animation = visual.Compositor.CreateVector3DKeyFrameAnimation();
        animation.InsertKeyFrame(1f, new Vector3D(0, 0, 0));
        animation.Duration = TimeSpan.FromMilliseconds(1);
        visual.StartAnimation("Translation", animation);
    }

    #endregion // Private methods
}


/// <summary>
/// An icon that turns while IsSpinning (the Save icon while saving, Check for update): 800 ms a turn on the render thread.
/// </summary>
public class SpinningIcon : GlassIcon
{
    #region Fields and constants

    public static readonly StyledProperty<bool> IsSpinningProperty =
        AvaloniaProperty.Register<SpinningIcon, bool>(nameof(IsSpinning));

    #endregion // Fields and constants


    #region Properties

    public bool IsSpinning
    {
        get => GetValue(IsSpinningProperty);
        set => SetValue(IsSpinningProperty, value);
    }

    #endregion // Properties


    #region Protected methods

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateSpin();
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsSpinningProperty || change.Property == BoundsProperty) UpdateSpin();
    }

    #endregion // Protected methods


    #region Private methods

    private void UpdateSpin() => CompositionMotion.WhenReady(this, StartSpin);


    private void StartSpin(CompositionVisual visual)
    {
        visual.CenterPoint = new Vector3D(Bounds.Width / 2, Bounds.Height / 2, 0);
        var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        if (IsSpinning && !MotionTokens.IsFrozen)
        {
            animation.InsertKeyFrame(0f, 0f);
            animation.InsertKeyFrame(1f, (float)(Math.PI * 2), MotionTokens.LinearEasing);
            animation.Duration = MotionTokens.Spin;
            animation.IterationBehavior = AnimationIterationBehavior.Forever;
        }
        else
        {
            animation.InsertKeyFrame(1f, 0f);
            animation.Duration = TimeSpan.FromMilliseconds(1);
        }

        visual.StartAnimation("RotationAngle", animation);
    }

    #endregion // Private methods
}


/// <summary>
/// How a CountUpText writes its number.
/// </summary>
public enum CountUpFormat
{
    /// <summary>
    /// A whole number with group separators: 12,800.
    /// </summary>
    Integer,

    /// <summary>
    /// One decimal: an aperture 2.0, a focal length 23.0.
    /// </summary>
    OneDecimal,

    /// <summary>
    /// A shutter time: Value is the denominator, counting 1/1 to 1/N (DS 5.2).
    /// </summary>
    Shutter,
}


/// <summary>
/// A readout that counts up to its value (DS 5.2): 1100 ms quartic ease-out, tabular figures; shutter times count 1/N.
/// </summary>
public class CountUpText : TextBlock
{
    #region Fields and constants

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<CountUpText, double>(nameof(Value));

    public static readonly StyledProperty<CountUpFormat> FormatProperty =
        AvaloniaProperty.Register<CountUpText, CountUpFormat>(nameof(Format));

    public static readonly StyledProperty<string?> PrefixProperty =
        AvaloniaProperty.Register<CountUpText, string?>(nameof(Prefix));

    public static readonly StyledProperty<string?> SuffixProperty =
        AvaloniaProperty.Register<CountUpText, string?>(nameof(Suffix));

    private Choreo? _motion;
    private double _shown;

    #endregion // Fields and constants


    #region Properties

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public CountUpFormat Format
    {
        get => GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    public string? Prefix
    {
        get => GetValue(PrefixProperty);
        set => SetValue(PrefixProperty, value);
    }

    public string? Suffix
    {
        get => GetValue(SuffixProperty);
        set => SetValue(SuffixProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(TextBlock);

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Counts up from zero again, as a new file's readouts do.
    /// </summary>
    public void Replay()
    {
        _shown = 0;
        Animate();
    }


    /// <summary>
    /// Writes a value as the readout shows it.
    /// </summary>
    public static string FormatValue(double value, CountUpFormat format) => format switch
    {
        CountUpFormat.OneDecimal => value.ToString("0.0", CultureInfo.InvariantCulture),
        CountUpFormat.Shutter => "1/" + Math.Max(1, Math.Round(value)).ToString("0", CultureInfo.InvariantCulture),
        _ => Math.Round(value).ToString("#,0", CultureInfo.InvariantCulture),
    };

    #endregion // Public methods


    #region Protected methods

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Animate();
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ValueProperty) Animate();
        else if (change.Property == FormatProperty || change.Property == PrefixProperty || change.Property == SuffixProperty) Show(_shown);
    }

    #endregion // Protected methods


    #region Private methods

    private void Animate()
    {
        var from = _shown;
        var to = Value;
        _motion?.Stop(jumpToEnd: false);

        if (TopLevel.GetTopLevel(this) is null || MotionTokens.IsFrozen)
        {
            Show(to);
            return;
        }

        _motion = new Choreo().Add(MotionTokens.CountUp, MotionTokens.CountUpEasing, p => Show(from + (to - from) * p));
        _ = _motion.PlayAsync(this);
    }


    private void Show(double value)
    {
        _shown = value;
        Text = Prefix + FormatValue(value, Format) + Suffix;
    }

    #endregion // Private methods
}


/// <summary>
/// The drag-over ring (DS 5.2): an accent squircle ring that grows from 0.94 to 1.5 while it fades, every 1.4 s on the render thread.
/// </summary>
public class PulseRing : SquircleBorder
{
    #region Fields and constants

    private const float START_SCALE = 0.94f;
    private const float END_SCALE = 1.5f;
    private const float START_OPACITY = 0.7f;

    public static readonly StyledProperty<bool> IsPulsingProperty =
        AvaloniaProperty.Register<PulseRing, bool>(nameof(IsPulsing));

    #endregion // Fields and constants


    #region Properties

    public bool IsPulsing
    {
        get => GetValue(IsPulsingProperty);
        set => SetValue(IsPulsingProperty, value);
    }


    // always scaled, so drawn from an image of its exact ring; the holding round rects would show at every frame
    protected override double RasterScale => END_SCALE;

    #endregion // Properties


    #region Public methods

    // a render writes the visual's opacity, which ends a running opacity animation, so the pulse starts again in the same batch
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (IsPulsing && ElementComposition.GetElementVisual(this) is { } visual) StartPulse(visual);
    }

    #endregion // Public methods


    #region Protected methods

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdatePulse();
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsPulsingProperty || change.Property == BoundsProperty) UpdatePulse();
    }

    #endregion // Protected methods


    #region Private methods

    private void UpdatePulse() => CompositionMotion.WhenReady(this, StartPulse);


    private void StartPulse(CompositionVisual visual)
    {
        visual.CenterPoint = new Vector3D(Bounds.Width / 2, Bounds.Height / 2, 0);
        var scale = visual.Compositor.CreateVector3DKeyFrameAnimation();
        var fade = visual.Compositor.CreateScalarKeyFrameAnimation();
        if (IsPulsing && !MotionTokens.IsFrozen)
        {
            scale.InsertKeyFrame(0f, new Vector3D(START_SCALE, START_SCALE, 1));
            scale.InsertKeyFrame(1f, new Vector3D(END_SCALE, END_SCALE, 1), MotionTokens.DragRingEasing);
            fade.InsertKeyFrame(0f, START_OPACITY);
            fade.InsertKeyFrame(1f, 0f, MotionTokens.DragRingEasing);
            scale.Duration = fade.Duration = MotionTokens.DragRing;
            scale.IterationBehavior = fade.IterationBehavior = AnimationIterationBehavior.Forever;
        }
        else
        {
            scale.InsertKeyFrame(1f, new Vector3D(1, 1, 1));
            fade.InsertKeyFrame(1f, 0f);
            scale.Duration = fade.Duration = TimeSpan.FromMilliseconds(1);
        }

        visual.StartAnimation("Scale", scale);
        visual.StartAnimation("Opacity", fade);
    }

    #endregion // Private methods
}


/// <summary>
/// Floats its child 4 px up and back every 4 s on the render thread: the empty card's logo (DS 6.9).
/// </summary>
public class FloatingPanel : Decorator
{
    #region Fields and constants

    private const float RISE = -4f;

    public static readonly StyledProperty<bool> IsFloatingProperty =
        AvaloniaProperty.Register<FloatingPanel, bool>(nameof(IsFloating), true);

    #endregion // Fields and constants


    #region Properties

    public bool IsFloating
    {
        get => GetValue(IsFloatingProperty);
        set => SetValue(IsFloatingProperty, value);
    }

    #endregion // Properties


    #region Protected methods

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateFloat();
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsFloatingProperty) UpdateFloat();
    }

    #endregion // Protected methods


    #region Private methods

    private void UpdateFloat() => CompositionMotion.WhenReady(this, StartFloat);


    private void StartFloat(CompositionVisual visual)
    {
        var animation = visual.Compositor.CreateVector3DKeyFrameAnimation();
        if (IsFloating && !MotionTokens.IsFrozen)
        {
            animation.InsertKeyFrame(0f, default);
            animation.InsertKeyFrame(0.5f, new Vector3D(0, RISE, 0), MotionTokens.LogoFloatEasing);
            animation.InsertKeyFrame(1f, default, MotionTokens.LogoFloatEasing);
            animation.Duration = MotionTokens.LogoFloat;
            animation.IterationBehavior = AnimationIterationBehavior.Forever;
        }
        else
        {
            animation.InsertKeyFrame(1f, default);
            animation.Duration = TimeSpan.FromMilliseconds(1);
        }

        visual.StartAnimation("Translation", animation);
    }

    #endregion // Private methods
}
