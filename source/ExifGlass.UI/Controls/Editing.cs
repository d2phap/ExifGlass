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
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ExifGlass.UI.Motion;
using ExifGlass.UI.Tokens;
using System.Windows.Input;

namespace ExifGlass.UI.Controls;

/// <summary>
/// The pending changes bar (DS 6.13, S5): a card under the table styled as the selected tag's (owner at G8); the count, what Save writes, an optional note; Discard and Save with its spinner. Its room glides open in 380 ms while the card fades in and rises 12 px.
/// </summary>
public class PendingBar : TemplatedControl
{
    #region Fields and constants

    private const double RISE = 12;

    // the selected card's 3 px ring lies outside the card, so the clip leaves room for it
    private const double RING = 4;

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<PendingBar, string?>(nameof(Title));

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<PendingBar, string?>(nameof(Subtitle));

    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<PendingBar, string?>(nameof(Note));

    public static readonly StyledProperty<bool> IsSavingProperty =
        AvaloniaProperty.Register<PendingBar, bool>(nameof(IsSaving));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<PendingBar, bool>(nameof(IsOpen));

    public static readonly StyledProperty<ICommand?> SaveCommandProperty =
        AvaloniaProperty.Register<PendingBar, ICommand?>(nameof(SaveCommand));

    public static readonly StyledProperty<ICommand?> DiscardCommandProperty =
        AvaloniaProperty.Register<PendingBar, ICommand?>(nameof(DiscardCommand));

    // 0 to 1, animated: the room the bar takes in the layout, its gap above included
    private static readonly StyledProperty<double> OpennessProperty =
        AvaloniaProperty.Register<PendingBar, double>("Openness");

    private Control? _card;
    private int _generation;

    #endregion // Fields and constants


    #region Constructors

    static PendingBar()
    {
        AffectsMeasure<PendingBar>(OpennessProperty);
        AffectsArrange<PendingBar>(OpennessProperty);
    }


    public PendingBar()
    {
        IsVisible = false;
    }

    #endregion // Constructors


    #region Properties

    /// <summary>
    /// Gets or sets "3 pending changes".
    /// </summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// Gets or sets "Save writes DSCF4127.JPG · backup kept as DSCF4127.JPG_original".
    /// </summary>
    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <summary>
    /// Gets or sets what Save can't do, such as a location in the Content Credentials; null hides the line.
    /// </summary>
    public string? Note
    {
        get => GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    public bool IsSaving
    {
        get => GetValue(IsSavingProperty);
        set => SetValue(IsSavingProperty, value);
    }

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public ICommand? SaveCommand
    {
        get => GetValue(SaveCommandProperty);
        set => SetValue(SaveCommandProperty, value);
    }

    public ICommand? DiscardCommand
    {
        get => GetValue(DiscardCommandProperty);
        set => SetValue(DiscardCommandProperty, value);
    }

    #endregion // Properties


    #region Protected methods

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _card = e.NameScope.Find<Control>("PART_Card");
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsOpenProperty) Slide(IsOpen);
        else if (change.Property == NoteProperty) PseudoClasses.Set(":note", !string.IsNullOrEmpty(Note));
        else if (change.Property == IsSavingProperty) PseudoClasses.Set(":saving", IsSaving);
    }


    // the room opens with the openness; the card keeps its own height
    protected override Size MeasureOverride(Size availableSize)
    {
        var full = base.MeasureOverride(availableSize.WithHeight(double.PositiveInfinity));
        return new Size(full.Width, full.Height * GetValue(OpennessProperty));
    }


    // the card sits at the bottom of the room, so the table's edge uncovers it as the room opens
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (VisualChildren is [Layoutable root])
        {
            var full = root.DesiredSize.Height;
            root.Arrange(new Rect(0, finalSize.Height - full, finalSize.Width, full));
        }

        Clip = new RectangleGeometry(new Rect(-RING, -RING, finalSize.Width + 2 * RING, finalSize.Height + 2 * RING));
        return finalSize;
    }

    #endregion // Protected methods


    #region Private methods

    // a layout glide for the room (the table above gives way), composition motion for the card
    private void Slide(bool open)
    {
        var generation = ++_generation;
        IsHitTestVisible = open;
        Transitions = MotionTokens.IsFrozen ? null : [new DoubleTransition { Property = OpennessProperty, Duration = MotionTokens.PendingBar, Easing = MotionTokens.PendingBarEasing }];

        if (open)
        {
            IsVisible = true;
            SetValue(OpennessProperty, 1.0);
            Dispatcher.UIThread.Post(() =>
            {
                if (generation != _generation || _card is not { } card) return;

                CompositionMotion.Move(card, new Vector(0, RISE), default, MotionTokens.PendingBar, MotionTokens.PendingBarEasing);
                CompositionMotion.Fade(card, 0, 1, MotionTokens.PendingBarFade, MotionTokens.PendingBarFadeEasing);
            }, DispatcherPriority.Render);
            return;
        }

        SetValue(OpennessProperty, 0.0);
        if (_card is { } shown) CompositionMotion.Fade(shown, 1, 0, MotionTokens.PendingBarFade, MotionTokens.PendingBarFadeEasing);
        DispatcherTimer.RunOnce(() =>
        {
            if (generation == _generation) IsVisible = false;
        }, MotionTokens.IsFrozen ? TimeSpan.FromMilliseconds(1) : MotionTokens.PendingBar);
    }

    #endregion // Private methods
}


/// <summary>
/// The Add-tag card (DS 7.3, S5): EXIF, XMP or IPTC; a tag name with suggestions from ExifTool's tag table; the value; Cancel and Add tag.
/// </summary>
public class AddTagCard : TemplatedControl
{
    #region Fields and constants

    public static readonly StyledProperty<IReadOnlyList<string>?> GroupsProperty =
        AvaloniaProperty.Register<AddTagCard, IReadOnlyList<string>?>(nameof(Groups));

    public static readonly StyledProperty<string?> GroupProperty =
        AvaloniaProperty.Register<AddTagCard, string?>(nameof(Group), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string?> TagNameProperty =
        AvaloniaProperty.Register<AddTagCard, string?>(nameof(TagName), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<AddTagCard, string?>(nameof(Value), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<IReadOnlyList<string>?> SuggestionsProperty =
        AvaloniaProperty.Register<AddTagCard, IReadOnlyList<string>?>(nameof(Suggestions));

    public static readonly StyledProperty<string?> HintProperty =
        AvaloniaProperty.Register<AddTagCard, string?>(nameof(Hint));

    public static readonly StyledProperty<ICommand?> AddCommandProperty =
        AvaloniaProperty.Register<AddTagCard, ICommand?>(nameof(AddCommand));

    public static readonly StyledProperty<ICommand?> CancelCommandProperty =
        AvaloniaProperty.Register<AddTagCard, ICommand?>(nameof(CancelCommand));

    // a focus leaving for the suggestion list comes back before this; a real leave doesn't
    private static readonly TimeSpan LeaveGrace = TimeSpan.FromMilliseconds(150);

    private TextBox? _name;
    private TextBox? _value;
    private Popup? _popup;
    private ListBox? _list;
    private bool _picked;

    #endregion // Fields and constants


    #region Properties

    /// <summary>
    /// Gets or sets the groups offered: EXIF, XMP and IPTC where the file takes IPTC.
    /// </summary>
    public IReadOnlyList<string>? Groups
    {
        get => GetValue(GroupsProperty);
        set => SetValue(GroupsProperty, value);
    }

    public string? Group
    {
        get => GetValue(GroupProperty);
        set => SetValue(GroupProperty, value);
    }

    public string? TagName
    {
        get => GetValue(TagNameProperty);
        set => SetValue(TagNameProperty, value);
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    /// Gets or sets the names offered under the name field for what is typed.
    /// </summary>
    public IReadOnlyList<string>? Suggestions
    {
        get => GetValue(SuggestionsProperty);
        set => SetValue(SuggestionsProperty, value);
    }

    /// <summary>
    /// Gets or sets why Add tag is off, such as a name ExifTool can't write; null hides the line.
    /// </summary>
    public string? Hint
    {
        get => GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    public ICommand? AddCommand
    {
        get => GetValue(AddCommandProperty);
        set => SetValue(AddCommandProperty, value);
    }

    public ICommand? CancelCommand
    {
        get => GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Plays the inspector's entry: fade and a 12 px rise in 400 ms (DS 5.2).
    /// </summary>
    public void Appear() => Card.PlayAppear(this);


    /// <summary>
    /// Puts the caret in the name field, as the card opens.
    /// </summary>
    public void FocusName() => Dispatcher.UIThread.Post(() => _name?.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);

    #endregion // Public methods


    #region Protected methods

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_name is not null)
        {
            _name.RemoveHandler(KeyDownEvent, Name_KeyDown);
            _name.TextChanged -= Name_TextChanged;
            _name.GotFocus -= Name_GotFocus;
            _name.LostFocus -= Name_LostFocus;
        }

        _value?.RemoveHandler(KeyDownEvent, Value_KeyDown);
        if (_list is not null) _list.Tapped -= List_Tapped;

        base.OnApplyTemplate(e);

        _name = e.NameScope.Find<TextBox>("PART_Name");
        _value = e.NameScope.Find<TextBox>("PART_Value");
        _popup = e.NameScope.Find<Popup>("PART_Suggestions");
        _list = e.NameScope.Find<ListBox>("PART_SuggestionList");

        if (_name is not null)
        {
            _name.AddHandler(KeyDownEvent, Name_KeyDown, RoutingStrategies.Tunnel);
            _name.TextChanged += Name_TextChanged;
            _name.GotFocus += Name_GotFocus;
            _name.LostFocus += Name_LostFocus;
        }

        _value?.AddHandler(KeyDownEvent, Value_KeyDown, RoutingStrategies.Tunnel);
        if (_list is not null) _list.Tapped += List_Tapped;
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SuggestionsProperty) UpdatePopup();
        else if (change.Property == HintProperty) PseudoClasses.Set(":hint", !string.IsNullOrEmpty(Hint));
    }

    #endregion // Protected methods


    #region Private methods

    // the list shows while the name has focus and something else than what is typed is offered
    private void UpdatePopup()
    {
        if (_popup is null || _list is null || _name is null) return;

        var items = Suggestions;
        var show = !_picked && _name.IsFocused && items is { Count: > 0 }
            && !(items.Count == 1 && string.Equals(items[0], _name.Text, StringComparison.OrdinalIgnoreCase));

        if (show) _list.ItemsSource = items;
        _list.SelectedIndex = -1;
        _popup.IsOpen = show;
    }


    private void Pick(string name)
    {
        if (_name is null) return;

        _picked = true;
        _name.Text = name;
        _name.CaretIndex = name.Length;
        if (_popup is not null) _popup.IsOpen = false;
    }


    private void Run(ICommand? command)
    {
        if (command?.CanExecute(null) == true) command.Execute(null);
    }


    private static bool IsApplyKey(KeyEventArgs e) =>
        e.Key == Key.Enter && e.KeyModifiers == (OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control);

    #endregion // Private methods


    #region Event handlers

    // Down and Up walk the list, Enter or Tab take the highlighted name, Esc closes the list before it cancels the card
    private void Name_KeyDown(object? sender, KeyEventArgs e)
    {
        var open = _popup?.IsOpen == true && _list is not null;
        var count = _list?.ItemCount ?? 0;

        if (IsApplyKey(e))
        {
            Run(AddCommand);
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.Down when open:
                _list!.SelectedIndex = Math.Min(_list.SelectedIndex + 1, count - 1);
                _list.ScrollIntoView(_list.SelectedIndex);
                e.Handled = true;
                break;

            case Key.Down:
                _picked = false;
                UpdatePopup();
                e.Handled = _popup?.IsOpen == true;
                break;

            case Key.Up when open:
                _list!.SelectedIndex = Math.Max(_list.SelectedIndex - 1, 0);
                _list.ScrollIntoView(_list.SelectedIndex);
                e.Handled = true;
                break;

            case Key.Enter or Key.Tab when open && _list!.SelectedItem is string name:
                Pick(name);
                e.Handled = e.Key == Key.Enter;
                break;

            case Key.Enter when e.KeyModifiers == KeyModifiers.None:
                _value?.Focus(NavigationMethod.Tab);
                e.Handled = true;
                break;

            case Key.Escape when open:
                _picked = true;
                _popup!.IsOpen = false;
                e.Handled = true;
                break;

            case Key.Escape:
                Run(CancelCommand);
                e.Handled = true;
                break;
        }
    }


    private void Value_KeyDown(object? sender, KeyEventArgs e)
    {
        if (IsApplyKey(e)) Run(AddCommand);
        else if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None) Run(CancelCommand);
        else return;

        e.Handled = true;
    }


    private void Name_TextChanged(object? sender, TextChangedEventArgs e)
    {
        // typing again after a pick offers names again
        if (_name?.Text != (_list?.SelectedItem as string)) _picked = false;
        UpdatePopup();
    }


    private void Name_GotFocus(object? sender, FocusChangedEventArgs e) => UpdatePopup();


    private void Name_LostFocus(object? sender, RoutedEventArgs e) => DispatcherTimer.RunOnce(() =>
    {
        if (_name is { IsFocused: false } && _popup is not null) _popup.IsOpen = false;
    }, LeaveGrace);


    private void List_Tapped(object? sender, TappedEventArgs e)
    {
        if (_list?.SelectedItem is not string name) return;

        Pick(name);
        _name?.Focus(NavigationMethod.Pointer);
    }

    #endregion // Event handlers
}
