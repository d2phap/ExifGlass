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
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using ExifGlass.UI.Motion;
using ExifGlass.UI.Tokens;

namespace ExifGlass.UI.Controls;

/// <summary>
/// What a toast reports: its Filled status icon and colour.
/// </summary>
public enum ToastKind
{
    Success,
    Danger,
    Info,
}


/// <summary>
/// A window's toast (DS 6.14): bottom centre, 62 above the bottom, 44 high, radius 16; one at a time, a new one replaces the old; soft-spring in, auto-hide after 2.3 s, fade 220 ms.
/// </summary>
public class ToastHost : TemplatedControl
{
    #region Fields and constants

    private const double RISE = 14;
    private const double START_SCALE = 0.96;

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ToastHost, string?>(nameof(Title));

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<ToastHost, string?>(nameof(Subtitle));

    public static readonly StyledProperty<ToastKind> KindProperty =
        AvaloniaProperty.Register<ToastHost, ToastKind>(nameof(Kind));

    public static readonly DirectProperty<ToastHost, bool> IsShownProperty =
        AvaloniaProperty.RegisterDirect<ToastHost, bool>(nameof(IsShown), t => t.IsShown);

    public static readonly DirectProperty<ToastHost, IconId> StatusIconProperty =
        AvaloniaProperty.RegisterDirect<ToastHost, IconId>(nameof(StatusIcon), t => t.StatusIcon);

    private Control? _toast;
    private DispatcherTimer? _timer;
    private bool _isShown;
    private IconId _statusIcon = IconId.CheckmarkCircleFilled;

    #endregion // Fields and constants


    #region Properties

    public string? Title
    {
        get => GetValue(TitleProperty);
        private set => SetValue(TitleProperty, value);
    }

    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        private set => SetValue(SubtitleProperty, value);
    }

    public ToastKind Kind
    {
        get => GetValue(KindProperty);
        private set => SetValue(KindProperty, value);
    }

    /// <summary>
    /// Gets whether a toast is showing or on its way in.
    /// </summary>
    public bool IsShown
    {
        get => _isShown;
        private set => SetAndRaise(IsShownProperty, ref _isShown, value);
    }

    public IconId StatusIcon
    {
        get => _statusIcon;
        private set => SetAndRaise(StatusIconProperty, ref _statusIcon, value);
    }

    /// <summary>
    /// Gets or sets how long a toast stays; the gallery keeps one on screen.
    /// </summary>
    public TimeSpan Lifetime { get; set; } = MotionTokens.ToastLife;

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Shows a toast; one already on screen is replaced and its time restarts.
    /// </summary>
    public void Show(string title, string? subtitle = null, ToastKind kind = ToastKind.Success)
    {
        Title = title;
        Subtitle = subtitle;
        Kind = kind;
        StatusIcon = kind switch
        {
            ToastKind.Danger => IconId.ErrorCircleFilled,
            ToastKind.Info => IconId.InfoFilled,
            _ => IconId.CheckmarkCircleFilled,
        };
        PseudoClasses.Set(":danger", kind == ToastKind.Danger);
        PseudoClasses.Set(":info", kind == ToastKind.Info);

        _timer?.Stop();
        if (!IsShown)
        {
            IsShown = true;
            AnimateIn();
        }

        if (Lifetime <= TimeSpan.Zero || Lifetime == Timeout.InfiniteTimeSpan) return;

        _timer = new DispatcherTimer { Interval = Lifetime };
        _timer.Tick += Timer_Tick;
        _timer.Start();
    }


    public void Hide()
    {
        _timer?.Stop();
        if (!IsShown || _toast is not { } toast) return;

        IsShown = false;
        CompositionMotion.Fade(toast, 1, 0, MotionTokens.Fade, MotionTokens.FadeEasing);

        // hidden once faded, unless another toast came meanwhile
        DispatcherTimer.RunOnce(() =>
        {
            if (!IsShown) toast.IsVisible = false;
        }, MotionTokens.IsFrozen ? TimeSpan.FromMilliseconds(1) : MotionTokens.Fade);
    }

    #endregion // Public methods


    #region Protected methods

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _toast = e.NameScope.Find<Control>("PART_Toast");
        if (_toast is not null) _toast.IsVisible = IsShown;
    }

    #endregion // Protected methods


    #region Private methods

    // in: rise 14 px and scale 0.96 to 1 on the soft spring, fading in over the first 220 ms
    private void AnimateIn()
    {
        if (_toast is not { } toast) return;

        toast.IsVisible = true;

        // a toast shown before its first layout scales about its real centre once arranged
        Dispatcher.UIThread.Post(() =>
        {
            CompositionMotion.Fade(toast, 0, 1, MotionTokens.Fade, MotionTokens.FadeEasing);
            CompositionMotion.Move(toast, new Vector(0, RISE), default, MotionTokens.ToastIn, MotionTokens.ToastInEasing);
            CompositionMotion.Scale(toast, START_SCALE, 1, MotionTokens.ToastIn, MotionTokens.ToastInEasing);
        }, DispatcherPriority.Render);
    }

    #endregion // Private methods


    #region Event handlers

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (sender is DispatcherTimer timer) timer.Stop();
        Hide();
    }

    #endregion // Event handlers
}


/// <summary>
/// The answer to an in-window dialog: its primary action, its second action, or Cancel (also Esc).
/// </summary>
public enum DialogAnswer
{
    Cancel,
    Primary,
    Secondary,
}


/// <summary>
/// What an in-window dialog asks (D8: Save / Discard / Cancel).
/// </summary>
public sealed record DialogRequest(string Title, string? Body, string PrimaryText, string? SecondaryText = null, string CancelText = UiText.DIALOG_CANCEL, bool IsSecondaryDanger = true, IconId? Icon = null);


/// <summary>
/// Hosts a window's content and shows an in-window dialog over it (D8): menu-style surface at elevation 2, a scrim, focus kept inside, Esc cancels.
/// </summary>
public class DialogHost : ContentControl
{
    #region Fields and constants

    private const double START_SCALE = 0.96;

    public static readonly DirectProperty<DialogHost, bool> IsOpenProperty =
        AvaloniaProperty.RegisterDirect<DialogHost, bool>(nameof(IsOpen), d => d.IsOpen);

    private ContentPresenter? _contentPresenter;
    private Panel? _layer;
    private Control? _scrim;
    private DialogCard? _card;
    private TaskCompletionSource<DialogAnswer>? _result;
    private IInputElement? _focusBefore;
    private bool _isOpen;
    private int _generation;

    #endregion // Fields and constants


    #region Properties

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetAndRaise(IsOpenProperty, ref _isOpen, value);
    }

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Shows a dialog and waits for its answer; Cancel when dismissed with Esc or replaced by another.
    /// </summary>
    public Task<DialogAnswer> ShowAsync(DialogRequest request)
    {
        if (_layer is null || _card is null || _contentPresenter is null) return Task.FromResult(DialogAnswer.Cancel);

        _result?.TrySetResult(DialogAnswer.Cancel);
        _result = new TaskCompletionSource<DialogAnswer>(TaskCreationOptions.RunContinuationsAsynchronously);
        _focusBefore ??= TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        _generation++;

        _card.Apply(request);
        _contentPresenter.IsHitTestVisible = false;
        _layer.IsVisible = true;
        IsOpen = true;

        if (_scrim is not null) CompositionMotion.Fade(_scrim, 0, 1, MotionTokens.Scrim, MotionTokens.ScrimEasing);
        Dispatcher.UIThread.Post(() =>
        {
            CompositionMotion.Fade(_card, 0, 1, MotionTokens.Dialog, MotionTokens.StandardEasing);
            CompositionMotion.Scale(_card, START_SCALE, 1, MotionTokens.Dialog, MotionTokens.DialogEasing);
            _card.FocusDefault();
        }, DispatcherPriority.Render);

        return _result.Task;
    }

    #endregion // Public methods


    #region Protected methods

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_card is not null) _card.Answered -= Card_Answered;

        base.OnApplyTemplate(e);

        _contentPresenter = e.NameScope.Find<ContentPresenter>("PART_ContentPresenter");
        _layer = e.NameScope.Find<Panel>("PART_DialogLayer");
        _scrim = e.NameScope.Find<Control>("PART_Scrim");
        _card = e.NameScope.Find<DialogCard>("PART_Card");
        if (_card is not null) _card.Answered += Card_Answered;
    }


    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (IsOpen && e.Key == Key.Escape)
        {
            Close(DialogAnswer.Cancel);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    #endregion // Protected methods


    #region Private methods

    private void Close(DialogAnswer answer)
    {
        if (!IsOpen || _layer is null || _contentPresenter is null || _card is null) return;

        IsOpen = false;
        _contentPresenter.IsHitTestVisible = true;

        var generation = _generation;
        var layer = _layer;
        if (_scrim is not null) CompositionMotion.Fade(_scrim, 1, 0, MotionTokens.Fade, MotionTokens.FadeEasing);
        CompositionMotion.Fade(_card, 1, 0, MotionTokens.Fade, MotionTokens.FadeEasing);
        DispatcherTimer.RunOnce(() =>
        {
            if (generation == _generation && !IsOpen) layer.IsVisible = false;
        }, MotionTokens.IsFrozen ? TimeSpan.FromMilliseconds(1) : MotionTokens.Fade);

        _focusBefore?.Focus();
        _focusBefore = null;
        _result?.TrySetResult(answer);
        _result = null;
    }

    #endregion // Private methods


    #region Event handlers

    private void Card_Answered(object? sender, DialogAnswer answer) => Close(answer);

    #endregion // Event handlers
}


/// <summary>
/// The dialog surface a DialogHost shows: icon, title, body and up to three buttons; the primary one takes the first focus.
/// </summary>
public class DialogCard : TemplatedControl
{
    #region Fields and constants

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<DialogCard, string?>(nameof(Title));

    public static readonly StyledProperty<string?> BodyProperty =
        AvaloniaProperty.Register<DialogCard, string?>(nameof(Body));

    public static readonly StyledProperty<string?> PrimaryTextProperty =
        AvaloniaProperty.Register<DialogCard, string?>(nameof(PrimaryText));

    public static readonly StyledProperty<string?> SecondaryTextProperty =
        AvaloniaProperty.Register<DialogCard, string?>(nameof(SecondaryText));

    public static readonly StyledProperty<string?> CancelTextProperty =
        AvaloniaProperty.Register<DialogCard, string?>(nameof(CancelText), UiText.DIALOG_CANCEL);

    public static readonly StyledProperty<bool> IsSecondaryDangerProperty =
        AvaloniaProperty.Register<DialogCard, bool>(nameof(IsSecondaryDanger), true);

    public static readonly StyledProperty<IconId?> IconProperty =
        AvaloniaProperty.Register<DialogCard, IconId?>(nameof(Icon));

    private Button? _primary;
    private Button? _secondary;
    private Button? _cancel;

    #endregion // Fields and constants


    #region Properties

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public string? PrimaryText
    {
        get => GetValue(PrimaryTextProperty);
        set => SetValue(PrimaryTextProperty, value);
    }

    public string? SecondaryText
    {
        get => GetValue(SecondaryTextProperty);
        set => SetValue(SecondaryTextProperty, value);
    }

    public string? CancelText
    {
        get => GetValue(CancelTextProperty);
        set => SetValue(CancelTextProperty, value);
    }

    public bool IsSecondaryDanger
    {
        get => GetValue(IsSecondaryDangerProperty);
        set => SetValue(IsSecondaryDangerProperty, value);
    }

    public IconId? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    #endregion // Properties


    #region Events

    public event EventHandler<DialogAnswer>? Answered;

    #endregion // Events


    #region Public methods

    public void Apply(DialogRequest request)
    {
        Title = request.Title;
        Body = request.Body;
        PrimaryText = request.PrimaryText;
        SecondaryText = request.SecondaryText;
        CancelText = request.CancelText;
        IsSecondaryDanger = request.IsSecondaryDanger;
        Icon = request.Icon;
    }


    /// <summary>
    /// Focuses the primary button as Tab would, so its ring shows.
    /// </summary>
    public void FocusDefault() => _primary?.Focus(NavigationMethod.Tab);

    #endregion // Public methods


    #region Protected methods

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_primary is not null) _primary.Click -= Primary_Click;
        if (_secondary is not null) _secondary.Click -= Secondary_Click;
        if (_cancel is not null) _cancel.Click -= Cancel_Click;

        base.OnApplyTemplate(e);

        _primary = e.NameScope.Find<Button>("PART_Primary");
        _secondary = e.NameScope.Find<Button>("PART_Secondary");
        _cancel = e.NameScope.Find<Button>("PART_Cancel");
        if (_primary is not null) _primary.Click += Primary_Click;
        if (_secondary is not null) _secondary.Click += Secondary_Click;
        if (_cancel is not null) _cancel.Click += Cancel_Click;
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SecondaryTextProperty) PseudoClasses.Set(":secondary", !string.IsNullOrEmpty(SecondaryText));
        else if (change.Property == IconProperty) PseudoClasses.Set(":icon", Icon is not null);
    }

    #endregion // Protected methods


    #region Event handlers

    private void Primary_Click(object? sender, RoutedEventArgs e) => Answered?.Invoke(this, DialogAnswer.Primary);

    private void Secondary_Click(object? sender, RoutedEventArgs e) => Answered?.Invoke(this, DialogAnswer.Secondary);

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Answered?.Invoke(this, DialogAnswer.Cancel);

    #endregion // Event handlers
}


/// <summary>
/// Side panels that push the content aside (owner at G4 review, replacing DS 2.1's overlays): a pane slides in from its edge and the content moves with it, keeping its width, clipped at the far edge; no scrim, no shadow. Opening one closes the other; Esc closes.
/// </summary>
public class PushHost : ContentControl
{
    #region Fields and constants

    public static readonly StyledProperty<object?> LeftPaneProperty =
        AvaloniaProperty.Register<PushHost, object?>(nameof(LeftPane));

    public static readonly StyledProperty<object?> RightPaneProperty =
        AvaloniaProperty.Register<PushHost, object?>(nameof(RightPane));

    public static readonly StyledProperty<bool> IsLeftOpenProperty =
        AvaloniaProperty.Register<PushHost, bool>(nameof(IsLeftOpen), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsRightOpenProperty =
        AvaloniaProperty.Register<PushHost, bool>(nameof(IsRightOpen), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<double> LeftTravelProperty =
        AvaloniaProperty.Register<PushHost, double>(nameof(LeftTravel), 244);

    public static readonly StyledProperty<double> RightTravelProperty =
        AvaloniaProperty.Register<PushHost, double>(nameof(RightTravel), 312);

    /// <summary>
    /// How far past its edges the host still draws: the window's padding, for the inspector's scroll bar and the selected card's ring; a pushed table is cut at the window's edge.
    /// </summary>
    public static readonly StyledProperty<double> BleedProperty =
        AvaloniaProperty.Register<PushHost, double>(nameof(Bleed), 8);

    private Control? _strip;
    private Control? _left;
    private Control? _right;
    private double _offset;
    private int _generation;

    #endregion // Fields and constants


    #region Properties

    public object? LeftPane
    {
        get => GetValue(LeftPaneProperty);
        set => SetValue(LeftPaneProperty, value);
    }

    public object? RightPane
    {
        get => GetValue(RightPaneProperty);
        set => SetValue(RightPaneProperty, value);
    }

    public bool IsLeftOpen
    {
        get => GetValue(IsLeftOpenProperty);
        set => SetValue(IsLeftOpenProperty, value);
    }

    public bool IsRightOpen
    {
        get => GetValue(IsRightOpenProperty);
        set => SetValue(IsRightOpenProperty, value);
    }

    /// <summary>
    /// Gets or sets how far the left pane pushes the content: its width and the gap after it (rail 244).
    /// </summary>
    public double LeftTravel
    {
        get => GetValue(LeftTravelProperty);
        set => SetValue(LeftTravelProperty, value);
    }

    /// <summary>
    /// Gets or sets how far the right pane pushes the content: the gap before it and its width (inspector 312).
    /// </summary>
    public double RightTravel
    {
        get => GetValue(RightTravelProperty);
        set => SetValue(RightTravelProperty, value);
    }

    public double Bleed
    {
        get => GetValue(BleedProperty);
        set => SetValue(BleedProperty, value);
    }

    /// <summary>
    /// Gets where the content rests now: LeftTravel with the left pane open, minus RightTravel with the right one, else 0.
    /// </summary>
    public double Offset => _offset;

    #endregion // Properties


    #region Public methods

    public void CloseAll()
    {
        SetCurrentValue(IsLeftOpenProperty, false);
        SetCurrentValue(IsRightOpenProperty, false);
    }

    #endregion // Public methods


    #region Protected methods

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        var bounds = new Rect(size).Inflate(Bleed);
        if (Clip is not RectangleGeometry clip || clip.Rect != bounds) Clip = new RectangleGeometry(bounds);

        return size;
    }


    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _strip = e.NameScope.Find<Control>("PART_Strip");
        _left = e.NameScope.Find<Control>("PART_LeftPane");
        _right = e.NameScope.Find<Control>("PART_RightPane");
        Update(animate: false);
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsLeftOpenProperty)
        {
            if (IsLeftOpen) SetCurrentValue(IsRightOpenProperty, false);
            Update(animate: true);
        }
        else if (change.Property == IsRightOpenProperty)
        {
            if (IsRightOpen) SetCurrentValue(IsLeftOpenProperty, false);
            Update(animate: true);
        }
        else if (change.Property == LeftTravelProperty || change.Property == RightTravelProperty)
        {
            Update(animate: false);
        }
    }


    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && (IsLeftOpen || IsRightOpen))
        {
            CloseAll();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    #endregion // Protected methods


    #region Private methods

    // the rest place is a render transform, so hit testing follows it; the render thread glides there from where the content was (FLIP)
    private void Update(bool animate)
    {
        if (_strip is null) return;

        var generation = ++_generation;
        var previous = _offset;
        _offset = IsLeftOpen ? LeftTravel : IsRightOpen ? -RightTravel : 0;

        if (_left is not null && IsLeftOpen) _left.IsVisible = true;
        if (_right is not null && IsRightOpen) _right.IsVisible = true;

        _strip.RenderTransform = _offset == 0 ? null : new TranslateTransform(_offset, 0);
        var glide = animate && previous != _offset && !MotionTokens.IsFrozen;
        if (glide) CompositionMotion.Move(_strip, new Vector(previous - _offset, 0), default, MotionTokens.Overlay, MotionTokens.OverlayEasing);

        // a closed pane leaves the tab order once it is out of sight
        DispatcherTimer.RunOnce(() =>
        {
            if (generation != _generation) return;

            if (_left is not null) _left.IsVisible = IsLeftOpen;
            if (_right is not null) _right.IsVisible = IsRightOpen;
        }, glide ? MotionTokens.Overlay : TimeSpan.FromMilliseconds(1));
    }

    #endregion // Private methods
}


/// <summary>
/// The strip a PushHost slides: its first child, the content, fills it; the second waits one LeftTravel before its left edge and the third one RightTravel past its right edge.
/// </summary>
public class PushStrip : Panel
{
    #region Fields and constants

    public static readonly StyledProperty<double> LeftTravelProperty =
        PushHost.LeftTravelProperty.AddOwner<PushStrip>();

    public static readonly StyledProperty<double> RightTravelProperty =
        PushHost.RightTravelProperty.AddOwner<PushStrip>();

    #endregion // Fields and constants


    #region Properties

    public double LeftTravel
    {
        get => GetValue(LeftTravelProperty);
        set => SetValue(LeftTravelProperty, value);
    }

    public double RightTravel
    {
        get => GetValue(RightTravelProperty);
        set => SetValue(RightTravelProperty, value);
    }

    #endregion // Properties


    #region Constructors

    static PushStrip()
    {
        AffectsArrange<PushStrip>(LeftTravelProperty, RightTravelProperty);
    }

    #endregion // Constructors


    #region Protected methods

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = default(Size);
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            child.Measure(i == 0 ? availableSize : availableSize.WithWidth(double.PositiveInfinity));
            if (i == 0) size = child.DesiredSize;
        }

        return size;
    }


    protected override Size ArrangeOverride(Size finalSize)
    {
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            var width = child.DesiredSize.Width;
            var x = i switch
            {
                0 => 0,
                1 => -LeftTravel,
                _ => finalSize.Width + RightTravel - width,
            };

            child.Arrange(new Rect(x, 0, i == 0 ? finalSize.Width : width, finalSize.Height));
        }

        return finalSize;
    }

    #endregion // Protected methods
}
