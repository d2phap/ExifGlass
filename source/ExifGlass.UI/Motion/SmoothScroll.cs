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
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ExifGlass.UI.Tokens;
using System.Runtime.CompilerServices;

namespace ExifGlass.UI.Motion;

/// <summary>
/// Eases mouse-wheel scrolling of a ScrollViewer like a browser; quick notches add up, touchpad deltas pass through.
/// </summary>
public static class SmoothScroll
{
    #region Fields and constants

    // Avalonia's ScrollContentPresenter step per wheel unit: only the motion changes
    private const double STEP = 50;

    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(150);
    private static readonly CubicBezierEasing EaseOut = new(0, 0, 0.58, 1);
    private static readonly ConditionalWeakTable<ScrollViewer, State> States = [];

    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, bool>("IsEnabled", typeof(SmoothScroll));

    #endregion // Fields and constants


    #region Constructors

    static SmoothScroll()
    {
        IsEnabledProperty.Changed.AddClassHandler<ScrollViewer>((viewer, e) =>
        {
            if (e.NewValue is true)
            {
                viewer.AddHandler(InputElement.PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
                viewer.AddHandler(InputElement.PointerWheelChangedEvent, OnWheelDone, RoutingStrategies.Bubble);
            }
            else
            {
                viewer.RemoveHandler(InputElement.PointerWheelChangedEvent, OnWheel);
                viewer.RemoveHandler(InputElement.PointerWheelChangedEvent, OnWheelDone);
            }
        });
    }

    #endregion // Constructors


    #region Public methods

    public static bool GetIsEnabled(ScrollViewer viewer) => viewer.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(ScrollViewer viewer, bool value) => viewer.SetValue(IsEnabledProperty, value);

    #endregion // Public methods


    #region Event handlers

    private static void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (sender is not ScrollViewer viewer || MotionTokens.IsFrozen) return;

        var delta = Delta(e);

        // the tunnel reaches the outer scroller first: a scroller under the pointer that can move takes the wheel
        if (e.Source is Visual source && HasInnerScroller(source, viewer, delta)) return;

        // a wheel notch is a whole unit; touchpads send fractions and already move smoothly
        if (!IsWholeNotch(delta.X) || !IsWholeNotch(delta.Y)) return;

        var state = States.GetValue(viewer, _ => new State());
        var from = state.Choreo.IsPlaying ? state.Target : viewer.Offset;
        var max = new Vector(Math.Max(0, viewer.Extent.Width - viewer.Viewport.Width), Math.Max(0, viewer.Extent.Height - viewer.Viewport.Height));
        var target = new Vector(Math.Clamp(from.X - delta.X * STEP, 0, max.X), Math.Clamp(from.Y - delta.Y * STEP, 0, max.Y));

        // at an edge the wheel belongs to the outer scroller, as in Avalonia
        if (target == from) return;

        e.Handled = true;
        state.Choreo.Stop(jumpToEnd: false);
        state.Target = target;
        state.Start = viewer.Offset;

        state.Choreo = new Choreo().Add(Duration, EaseOut, p =>
        {
            viewer.SetCurrentValue(ScrollViewer.OffsetProperty, state.Start + (state.Target - state.Start) * p);
        });
        _ = state.Choreo.PlayAsync(viewer);
    }


    // after its presenter: a scroller inside another one never passes the wheel on
    private static void OnWheelDone(object? sender, PointerWheelEventArgs e)
    {
        if (sender is ScrollViewer viewer && viewer.FindAncestorOfType<ScrollViewer>() is not null && CanScroll(viewer, Delta(e))) e.Handled = true;
    }

    #endregion // Event handlers


    #region Private methods

    private static bool HasInnerScroller(Visual source, ScrollViewer viewer, Vector delta)
    {
        for (Visual? v = source; v is not null && v != viewer; v = v.GetVisualParent())
        {
            if (v is ScrollViewer inner && CanScroll(inner, delta)) return true;
        }

        return false;
    }


    private static Vector Delta(PointerWheelEventArgs e) => e.KeyModifiers == KeyModifiers.Shift && e.Delta.X == 0 ? new Vector(e.Delta.Y, 0) : e.Delta;


    private static bool CanScroll(ScrollViewer viewer, Vector delta) =>
        (delta.Y != 0 && viewer.Extent.Height > viewer.Viewport.Height + 0.5) || (delta.X != 0 && viewer.Extent.Width > viewer.Viewport.Width + 0.5);


    private static bool IsWholeNotch(double value) => value == 0 || (Math.Abs(value) >= 1 && value == Math.Round(value));

    #endregion // Private methods


    #region Nested types

    private sealed class State
    {
        public Choreo Choreo { get; set; } = new();

        public Vector Start { get; set; }

        public Vector Target { get; set; }
    }

    #endregion // Nested types
}
