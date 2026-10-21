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
using Avalonia.Layout;
using ExifGlass.UI.Tokens;

namespace ExifGlass.UI.Controls;

/// <summary>
/// A docked panel's column (DS 2.1): its width glides between 0 and OpenWidth in 420 ms; the panel keeps its own width and rides the moving edge, so it slides in as the table narrows.
/// </summary>
public class DockSlot : Decorator
{
    #region Fields and constants

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<DockSlot, bool>(nameof(IsOpen));

    public static readonly StyledProperty<double> OpenWidthProperty =
        AvaloniaProperty.Register<DockSlot, double>(nameof(OpenWidth));

    public static readonly StyledProperty<HorizontalAlignment> AnchorProperty =
        AvaloniaProperty.Register<DockSlot, HorizontalAlignment>(nameof(Anchor), HorizontalAlignment.Left);

    // 0 to 1, animated: the measure scales OpenWidth by it
    private static readonly StyledProperty<double> OpennessProperty =
        AvaloniaProperty.Register<DockSlot, double>("Openness");

    private readonly Transitions _motion =
    [
        new DoubleTransition { Property = OpennessProperty, Duration = MotionTokens.Panel, Easing = MotionTokens.PanelEasing },
    ];

    private bool _attached;

    #endregion // Fields and constants


    #region Properties

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>
    /// Gets or sets the column's open width, the panel's own width included its gap (244 rail, 312 inspector).
    /// </summary>
    public double OpenWidth
    {
        get => GetValue(OpenWidthProperty);
        set => SetValue(OpenWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the column edge the panel stays on, the table's side: Right for the rail, Left for the inspector.
    /// </summary>
    public HorizontalAlignment Anchor
    {
        get => GetValue(AnchorProperty);
        set => SetValue(AnchorProperty, value);
    }

    /// <summary>
    /// Gets how open the column is right now, 0 to 1.
    /// </summary>
    public double Openness => GetValue(OpennessProperty);

    #endregion // Properties


    #region Events

    /// <summary>
    /// Occurs when the column has finished closing.
    /// </summary>
    public event EventHandler? Closed;

    #endregion // Events


    #region Constructors

    static DockSlot()
    {
        AffectsMeasure<DockSlot>(OpennessProperty, OpenWidthProperty);
        AffectsArrange<DockSlot>(AnchorProperty);
    }

    #endregion // Constructors


    #region Public methods

    /// <summary>
    /// Opens or closes at once, as a layout restored at startup does.
    /// </summary>
    public void Snap(bool open)
    {
        Transitions = null;
        SetCurrentValue(IsOpenProperty, open);
        SetValue(OpennessProperty, open ? 1.0 : 0.0);
        if (_attached && !MotionTokens.IsFrozen) Transitions = _motion;
    }

    #endregion // Public methods


    #region Protected methods

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;

        // the state it shows up in applies at once; changes after that move
        Transitions = MotionTokens.IsFrozen ? null : _motion;
    }


    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
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
            var openness = GetValue(OpennessProperty);

            // at rest open nothing clips, so the panel's focus rings show whole; a closed panel is out of the tab order
            ClipToBounds = openness < 1;
            if (Child is { } child) child.IsVisible = openness > 0;

            if (change.Property == OpennessProperty && openness <= 0 && !IsOpen) Closed?.Invoke(this, EventArgs.Empty);
        }
    }


    protected override Size MeasureOverride(Size availableSize)
    {
        var openness = GetValue(OpennessProperty);
        var width = OpenWidth;
        if (Child is not { } child || openness <= 0) return default;

        child.Measure(availableSize.WithWidth(width));
        return new Size(width * openness, child.DesiredSize.Height);
    }


    // the panel keeps its full width at its anchored edge; the clip hides the part still outside the column
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is { } child)
        {
            var x = Anchor == HorizontalAlignment.Right ? finalSize.Width - OpenWidth : 0;
            child.Arrange(new Rect(x, 0, OpenWidth, finalSize.Height));
        }

        return finalSize;
    }

    #endregion // Protected methods
}
