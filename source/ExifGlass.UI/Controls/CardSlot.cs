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
using Avalonia.Media;
using ExifGlass.UI.Tokens;

namespace ExifGlass.UI.Controls;

/// <summary>
/// An inspector card's room, as DockSlot is a panel's column turned on its side (owner at G8): its height glides between 0 and the card's in 420 ms, pushing the cards below; the card keeps its height at the room's bottom edge, so it slides down into view. Padding is the room around the card, its gap to the next card included.
/// </summary>
public class CardSlot : Decorator
{
    #region Fields and constants

    // the selected card's 3 px ring lies outside the card, so the clip leaves room for it at the sides
    private const double RING = 4;

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<CardSlot, bool>(nameof(IsOpen));

    // 0 to 1, animated: the measure scales the room's height by it
    private static readonly StyledProperty<double> OpennessProperty =
        AvaloniaProperty.Register<CardSlot, double>("Openness");

    private readonly Transitions _motion =
    [
        new DoubleTransition { Property = OpennessProperty, Duration = MotionTokens.Panel, Easing = MotionTokens.PanelEasing },
    ];

    #endregion // Fields and constants


    #region Properties

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>
    /// Gets how open the room is right now, 0 to 1.
    /// </summary>
    public double Openness => GetValue(OpennessProperty);

    #endregion // Properties


    #region Events

    /// <summary>
    /// Occurs when the room has finished closing, so the card can let go of what it showed.
    /// </summary>
    public event EventHandler? Closed;

    #endregion // Events


    #region Constructors

    static CardSlot()
    {
        AffectsMeasure<CardSlot>(OpennessProperty);
        AffectsArrange<CardSlot>(OpennessProperty);
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

        if (change.Property == IsOpenProperty)
        {
            if (MotionTokens.IsFrozen) Transitions = null;
            SetValue(OpennessProperty, IsOpen ? 1.0 : 0.0);
        }
        else if (change.Property == OpennessProperty || change.Property == ChildProperty)
        {
            // a closed card is out of the layout and the tab order
            var openness = GetValue(OpennessProperty);
            if (Child is { } child) child.IsVisible = openness > 0;
            if (openness >= 1) Clip = null;

            if (change.Property == OpennessProperty && openness <= 0 && !IsOpen) Closed?.Invoke(this, EventArgs.Empty);
        }
    }


    protected override Size MeasureOverride(Size availableSize)
    {
        var openness = GetValue(OpennessProperty);
        if (Child is not { } child || openness <= 0) return default;

        var padding = Padding;
        child.Measure(new Size(Math.Max(0, availableSize.Width - padding.Left - padding.Right), double.PositiveInfinity));
        return new Size(child.DesiredSize.Width + padding.Left + padding.Right, (child.DesiredSize.Height + padding.Top + padding.Bottom) * openness);
    }


    // the card keeps its full height at the room's bottom; while it moves, the clip hides the part still above the room
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is not { } child) return finalSize;

        var padding = Padding;
        var height = child.DesiredSize.Height;
        child.Arrange(new Rect(padding.Left, finalSize.Height - padding.Bottom - height, Math.Max(0, finalSize.Width - padding.Left - padding.Right), height));

        if (GetValue(OpennessProperty) < 1) Clip = new RectangleGeometry(new Rect(-RING, 0, finalSize.Width + 2 * RING, finalSize.Height));
        return finalSize;
    }

    #endregion // Protected methods
}
