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
using Avalonia.Threading;
using ExifGlass.UI.Tokens;

namespace ExifGlass.UI.Controls;

/// <summary>
/// Glides to its child's new height when Trigger changes it (the command bar's expand); other changes, a resize's, apply at once.
/// </summary>
public class HeightGlide : Decorator
{
    #region Fields and constants

    public static readonly StyledProperty<object?> TriggerProperty =
        AvaloniaProperty.Register<HeightGlide, object?>(nameof(Trigger));

    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<HeightGlide, TimeSpan>(nameof(Duration), MotionTokens.CommandBar);

    private static readonly StyledProperty<double> ShownProperty =
        AvaloniaProperty.Register<HeightGlide, double>("Shown", double.NaN);

    private readonly DoubleTransition _transition = new() { Property = ShownProperty, Duration = MotionTokens.CommandBar, Easing = MotionTokens.EmphasizedEasing };
    private readonly Transitions _motion;
    private double _target = double.NaN;
    private bool _armed;

    #endregion // Fields and constants


    #region Properties

    public object? Trigger
    {
        get => GetValue(TriggerProperty);
        set => SetValue(TriggerProperty, value);
    }

    public TimeSpan Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    #endregion // Properties


    #region Constructors

    static HeightGlide()
    {
        AffectsMeasure<HeightGlide>(ShownProperty);
    }


    public HeightGlide()
    {
        _motion = [_transition];
    }

    #endregion // Constructors


    #region Public methods

    /// <summary>
    /// Shows the child's top part at once, clipped, until its height next changes.
    /// </summary>
    public void Reveal(double height)
    {
        Transitions = null;
        SetValue(ShownProperty, height);
        Transitions = _motion;
    }

    #endregion // Public methods


    #region Protected methods

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DurationProperty) _transition.Duration = Duration;
        if (change.Property != TriggerProperty || double.IsNaN(_target) || MotionTokens.IsFrozen) return;

        // only the layout this change makes glides: layout runs before the background queue
        _armed = true;
        Dispatcher.UIThread.Post(() => _armed = false, DispatcherPriority.Background);
    }


    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child) return default;

        child.Measure(availableSize.WithHeight(double.PositiveInfinity));
        var height = child.DesiredSize.Height;

        if (height != _target)
        {
            _target = height;
            if (_armed)
            {
                // a value set during a measure would measure again before this one returns
                _armed = false;
                Dispatcher.UIThread.Post(() => SetValue(ShownProperty, _target));
            }
            else
            {
                Transitions = null;
                SetValue(ShownProperty, height);
                Transitions = _motion;
            }
        }

        var shown = GetValue(ShownProperty);
        if (double.IsNaN(shown)) shown = height;

        // at rest nothing clips, so focus rings and shadows show whole
        ClipToBounds = Math.Abs(shown - height) > 0.5;
        return new Size(child.DesiredSize.Width, shown);
    }


    // the child keeps its full height; the clip shows the part the glide has reached
    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(finalSize.Height, Child.DesiredSize.Height)));
        return finalSize;
    }

    #endregion // Protected methods
}
