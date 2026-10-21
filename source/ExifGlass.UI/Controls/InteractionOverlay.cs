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
using Avalonia.Media;
using Avalonia.Media.Transformation;
using ExifGlass.UI.Tokens;

namespace ExifGlass.UI.Controls;

/// <summary>
/// The state layer between a control's fill and content: its brush follows hover and press, and it drives the template root's press scale (DS 4).
/// </summary>
public class InteractionOverlay : SquircleBorder
{
    #region Fields and constants

    private const double SCALE_EPSILON = 0.0001;

    /// <summary>
    /// A state shown regardless of input; the gallery and snapshot tests pin hover and press with it.
    /// </summary>
    public static readonly AttachedProperty<InteractionState> ForcedStateProperty =
        AvaloniaProperty.RegisterAttached<InteractionOverlay, InputElement, InteractionState>("ForcedState");

    public static readonly StyledProperty<IBrush?> RestBrushProperty =
        AvaloniaProperty.Register<InteractionOverlay, IBrush?>(nameof(RestBrush));

    public static readonly StyledProperty<IBrush?> HoverBrushProperty =
        AvaloniaProperty.Register<InteractionOverlay, IBrush?>(nameof(HoverBrush));

    public static readonly StyledProperty<IBrush?> PressBrushProperty =
        AvaloniaProperty.Register<InteractionOverlay, IBrush?>(nameof(PressBrush));

    public static readonly StyledProperty<double> PressScaleProperty =
        AvaloniaProperty.Register<InteractionOverlay, double>(nameof(PressScale), 1);

    /// <summary>
    /// Holds the hover brush while set: an open dropdown keeps its hover fill (DS 6.1).
    /// </summary>
    public static readonly StyledProperty<bool> IsHeldProperty =
        AvaloniaProperty.Register<InteractionOverlay, bool>(nameof(IsHeld));

    private static readonly TransformOperations SCALE_ONE = TransformOperations.Parse("scale(1)");

    private readonly BrushTransition _brushTransition = new() { Property = BackgroundProperty, Easing = MotionTokens.HoverEasing };
    private readonly TransformOperationsTransition _scaleTransition = new() { Property = RenderTransformProperty, Easing = MotionTokens.PressInEasing };
    private InputElement? _owner;
    private Visual? _scaleTarget;
    private TransformOperations? _pressedScale;
    private State _state = State.Rest;
    private bool _pointerDown;

    #endregion // Fields and constants


    #region Properties

    /// <summary>
    /// The hover colour at 0% alpha, so the brush transition never shifts hue.
    /// </summary>
    public IBrush? RestBrush
    {
        get => GetValue(RestBrushProperty);
        set => SetValue(RestBrushProperty, value);
    }

    public IBrush? HoverBrush
    {
        get => GetValue(HoverBrushProperty);
        set => SetValue(HoverBrushProperty, value);
    }

    public IBrush? PressBrush
    {
        get => GetValue(PressBrushProperty);
        set => SetValue(PressBrushProperty, value);
    }

    /// <summary>
    /// The template root's scale while pressed; 1 (wide items) turns it off.
    /// </summary>
    public double PressScale
    {
        get => GetValue(PressScaleProperty);
        set => SetValue(PressScaleProperty, value);
    }

    public bool IsHeld
    {
        get => GetValue(IsHeldProperty);
        set => SetValue(IsHeldProperty, value);
    }

    #endregion // Properties


    #region Public methods

    public static InteractionState GetForcedState(InputElement element) => element.GetValue(ForcedStateProperty);

    public static void SetForcedState(InputElement element, InteractionState value) => element.SetValue(ForcedStateProperty, value);

    #endregion // Public methods


    #region Protected methods

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        IsHitTestVisible = false;

        _owner = TemplatedParent as InputElement;
        _scaleTarget = Parent as Visual;
        if (_owner is null) return;

        // rows and toggles mark their pointer events handled, so listen to handled ones too
        _owner.PropertyChanged += Owner_PropertyChanged;
        _owner.AddHandler(PointerPressedEvent, Owner_PointerPressed, handledEventsToo: true);
        _owner.AddHandler(PointerReleasedEvent, Owner_PointerReleased, handledEventsToo: true);
        _owner.AddHandler(PointerCaptureLostEvent, Owner_PointerCaptureLost, handledEventsToo: true);

        Transitions ??= [];
        if (!Transitions.Contains(_brushTransition)) Transitions.Add(_brushTransition);

        if (_scaleTarget is not null)
        {
            _scaleTarget.Transitions ??= [];
            if (!_scaleTarget.Transitions.Contains(_scaleTransition)) _scaleTarget.Transitions.Add(_scaleTransition);
        }

        Update(animate: false);
    }


    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_owner is not null)
        {
            _owner.PropertyChanged -= Owner_PropertyChanged;
            _owner.RemoveHandler(PointerPressedEvent, Owner_PointerPressed);
            _owner.RemoveHandler(PointerReleasedEvent, Owner_PointerReleased);
            _owner.RemoveHandler(PointerCaptureLostEvent, Owner_PointerCaptureLost);
        }

        _owner = null;
        _scaleTarget = null;
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // a theme switch replaces the brushes: follow without a transition (DS 1.3)
        if (change.Property == RestBrushProperty || change.Property == HoverBrushProperty || change.Property == PressBrushProperty) Update(animate: false);
        else if (change.Property == IsHeldProperty) Update(animate: true);
        else if (change.Property == PressScaleProperty) _pressedScale = null;
    }

    #endregion // Protected methods


    #region Private methods

    // a Button's IsPressed includes Space; other surfaces (rows) track the left button
    private bool IsOwnerPressed() => _owner switch
    {
        Button button => button.IsPressed,
        _ => _pointerDown && _owner?.IsPointerOver == true,
    };


    private void Update(bool animate)
    {
        if (_owner is null) return;

        var forced = GetForcedState(_owner);
        var state = !_owner.IsEffectivelyEnabled ? State.Disabled
            : forced == InteractionState.Pressed || IsOwnerPressed() ? State.Pressed
            : forced == InteractionState.Hover || _owner.IsPointerOver || IsHeld ? State.Hover
            : State.Rest;

        var wasPressed = _state == State.Pressed;
        _state = state;

        // CSS takes the destination state's transition: one into :active, another out of it
        var instant = !animate || MotionTokens.IsFrozen;
        _brushTransition.Duration = state == State.Pressed ? MotionTokens.PressIn : MotionTokens.Hover;
        var brush = state switch
        {
            State.Hover => HoverBrush,
            State.Pressed => PressBrush,
            _ => RestBrush,
        };
        Apply(Transitions, _brushTransition, instant, () => Background = brush);

        if (_scaleTarget is not { } target || PressScale >= 1 - SCALE_EPSILON) return;

        if (state == State.Pressed || wasPressed)
        {
            _scaleTransition.Duration = state == State.Pressed ? MotionTokens.PressIn : MotionTokens.PressOut;
            _pressedScale ??= TransformOperations.Parse(FormattableString.Invariant($"scale({PressScale})"));
            var scale = state == State.Pressed ? _pressedScale : SCALE_ONE;
            Apply(target.Transitions, _scaleTransition, instant, () => target.RenderTransform = scale);
        }
    }


    // a transition even of zero length lands a frame late, so an instant change lifts it while the value is set
    private static void Apply(Transitions? transitions, ITransition transition, bool instant, Action set)
    {
        if (!instant || transitions is null || !transitions.Remove(transition))
        {
            set();
            return;
        }

        set();
        transitions.Add(transition);
    }

    #endregion // Private methods


    #region Event handlers

    private void Owner_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsPointerOverProperty || e.Property == IsEffectivelyEnabledProperty || e.Property == Button.IsPressedProperty || e.Property == ForcedStateProperty)
        {
            Update(animate: true);
        }
    }


    private void Owner_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(_owner).Properties.IsLeftButtonPressed) _pointerDown = true;
        Update(animate: true);
    }


    private void Owner_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _pointerDown = false;
        Update(animate: true);
    }


    private void Owner_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _pointerDown = false;
        Update(animate: true);
    }

    #endregion // Event handlers


    #region Nested types

    private enum State
    {
        Rest,
        Hover,
        Pressed,
        Disabled,
    }

    #endregion // Nested types
}


/// <summary>
/// An interaction state pinned with InteractionOverlay.ForcedState.
/// </summary>
public enum InteractionState
{
    None,
    Hover,
    Pressed,
}
