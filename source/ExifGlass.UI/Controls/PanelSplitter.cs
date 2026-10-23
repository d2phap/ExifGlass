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
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ExifGlass.UI.Tokens;

namespace ExifGlass.UI.Controls;

/// <summary>
/// The gap beside a docked panel as its resize handle (owner at G7): the east-west cursor and a 2 px accent line on hover, a drag asks for a new width, a double-click for the default.
/// </summary>
public class PanelSplitter : Panel
{
    #region Fields and constants

    private const double HOVER_OPACITY = 0.6;

    public static readonly StyledProperty<double> PanelWidthProperty =
        AvaloniaProperty.Register<PanelSplitter, double>(nameof(PanelWidth));

    public static readonly StyledProperty<bool> GrowsLeftProperty =
        AvaloniaProperty.Register<PanelSplitter, bool>(nameof(GrowsLeft));

    private readonly Border _line;
    private double _startX;
    private double _startWidth;
    private bool _dragging;

    #endregion // Fields and constants


    #region Properties

    /// <summary>
    /// Gets or sets the panel's width now; a drag starts from it.
    /// </summary>
    public double PanelWidth
    {
        get => GetValue(PanelWidthProperty);
        set => SetValue(PanelWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets whether a drag to the left widens the panel: the inspector's, which sits right of its gap.
    /// </summary>
    public bool GrowsLeft
    {
        get => GetValue(GrowsLeftProperty);
        set => SetValue(GrowsLeftProperty, value);
    }

    public bool IsDragging => _dragging;

    #endregion // Properties


    #region Events

    /// <summary>
    /// Occurs as a drag moves, with the width it asks for; the owner keeps it within the panel's limits.
    /// </summary>
    public event EventHandler<double>? ResizeRequested;

    public event EventHandler? ResizeCompleted;

    public event EventHandler? ResetRequested;

    #endregion // Events


    #region Constructors

    public PanelSplitter()
    {
        Background = Brushes.Transparent;
        Cursor = new Cursor(StandardCursorType.SizeWestEast);

        // the line keeps clear of the panels' rounded corners
        _line = new Border
        {
            Width = 2,
            Margin = new Thickness(0, 12),
            HorizontalAlignment = HorizontalAlignment.Center,
            CornerRadius = new CornerRadius(1),
            IsHitTestVisible = false,
            Opacity = 0,
            Transitions = [new DoubleTransition { Property = OpacityProperty, Duration = MotionTokens.Hover, Easing = MotionTokens.HoverEasing }],
        };
        _line[!Border.BackgroundProperty] = _line.GetResourceObservable(TokenKeys.ACCENT_BRUSH).ToBinding();
        Children.Add(_line);
    }

    #endregion // Constructors


    #region Protected methods

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        UpdateLine();
    }


    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        UpdateLine();
    }


    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        e.Handled = true;
        if (e.ClickCount == 2)
        {
            ResetRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        // the window's coordinates: the splitter itself moves as the panel grows
        _dragging = true;
        _startX = e.GetPosition(null).X;
        _startWidth = PanelWidth;
        e.Pointer.Capture(this);
        UpdateLine();
    }


    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging) return;

        var dx = e.GetPosition(null).X - _startX;
        ResizeRequested?.Invoke(this, _startWidth + (GrowsLeft ? -dx : dx));
    }


    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging) return;

        e.Pointer.Capture(null);
        EndDrag();
    }


    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_dragging) EndDrag();
    }

    #endregion // Protected methods


    #region Private methods

    private void EndDrag()
    {
        _dragging = false;
        UpdateLine();
        ResizeCompleted?.Invoke(this, EventArgs.Empty);
    }


    private void UpdateLine() => _line.Opacity = _dragging ? 1 : IsPointerOver ? HOVER_OPACITY : 0;

    #endregion // Private methods
}
