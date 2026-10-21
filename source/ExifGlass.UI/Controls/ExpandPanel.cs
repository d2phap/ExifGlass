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
using ExifGlass.UI.Tokens;

namespace ExifGlass.UI.Controls;

/// <summary>
/// Opens and closes its child (expanders such as "All parameters"), clipped while it moves: height 420 ms Emphasized, fade 220 ms.
/// </summary>
public class ExpandPanel : Decorator
{
    #region Fields and constants

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ExpandPanel, bool>(nameof(IsExpanded));

    // 0 to 1, animated: the measure scales the child's height by it
    private static readonly StyledProperty<double> OpennessProperty =
        AvaloniaProperty.Register<ExpandPanel, double>("Openness");

    private readonly Transitions _motion =
    [
        new DoubleTransition { Property = OpennessProperty, Duration = MotionTokens.GroupCollapse, Easing = MotionTokens.GroupCollapseEasing },
        new DoubleTransition { Property = OpacityProperty, Duration = MotionTokens.Fade, Easing = MotionTokens.FadeEasing },
    ];

    #endregion // Fields and constants


    #region Properties

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    #endregion // Properties


    #region Constructors

    static ExpandPanel()
    {
        AffectsMeasure<ExpandPanel>(OpennessProperty);
        ClipToBoundsProperty.OverrideDefaultValue<ExpandPanel>(true);
        OpacityProperty.OverrideDefaultValue<ExpandPanel>(0);
    }

    #endregion // Constructors


    #region Protected methods

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // the state it shows up in applies at once; changes after that move
        Transitions = MotionTokens.IsFrozen ? null : _motion;
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsExpandedProperty)
        {
            if (MotionTokens.IsFrozen) Transitions = null;
            SetValue(OpennessProperty, IsExpanded ? 1.0 : 0.0);
            Opacity = IsExpanded ? 1 : 0;
        }
        else if ((change.Property == OpennessProperty || change.Property == ChildProperty) && Child is { } child)
        {
            // a closed panel's controls are out of the tab order
            child.IsVisible = GetValue(OpennessProperty) > 0;
        }
    }


    protected override Size MeasureOverride(Size availableSize)
    {
        var openness = GetValue(OpennessProperty);
        if (Child is not { } child || openness <= 0) return default;

        child.Measure(availableSize.WithHeight(double.PositiveInfinity));
        return new Size(child.DesiredSize.Width, child.DesiredSize.Height * openness);
    }


    // the child keeps its full height; the clip shows the part that is open
    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(0, 0, finalSize.Width, Child.DesiredSize.Height));
        return finalSize;
    }

    #endregion // Protected methods
}
