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
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;
using ExifGlass.UI.Tokens;

namespace ExifGlass.UI.Controls;

/// <summary>
/// The argument token field (DS 6.15, D1): a chip per argument, Enter or Space adds, Backspace removes the last, hover shows the description, locked chips for app-owned flags, dashed suggestions.
/// </summary>
public class TokenField : TemplatedControl
{
    #region Fields and constants

    public static readonly StyledProperty<IList<string>?> LockedTokensProperty =
        AvaloniaProperty.Register<TokenField, IList<string>?>(nameof(LockedTokens));

    public static readonly StyledProperty<IList<string>?> SuggestionsProperty =
        AvaloniaProperty.Register<TokenField, IList<string>?>(nameof(Suggestions));

    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<TokenField, string?>(nameof(Placeholder), UiText.ADD_ARGUMENT);

    private static readonly TransformOperations POP_FROM = TransformOperations.Parse("scale(0.6)");
    private static readonly TransformOperations POP_TO = TransformOperations.Parse("scale(1)");

    private WrapPanel? _panel;
    private WrapPanel? _suggestionPanel;
    private TextBox? _input;
    private bool _rebuilding;

    #endregion // Fields and constants


    #region Properties

    /// <summary>
    /// Gets the arguments in order; edit it to change the chips.
    /// </summary>
    public AvaloniaList<string> Tokens { get; } = [];

    /// <summary>
    /// Arguments the app owns: shown with a lock and never removed (D1).
    /// </summary>
    public IList<string>? LockedTokens
    {
        get => GetValue(LockedTokensProperty);
        set => SetValue(LockedTokensProperty, value);
    }

    public IList<string>? Suggestions
    {
        get => GetValue(SuggestionsProperty);
        set => SetValue(SuggestionsProperty, value);
    }

    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>
    /// Gets or sets the hover description of an argument.
    /// </summary>
    public Func<string, string?>? DescriptionProvider { get; set; }

    /// <summary>
    /// Gets or sets why an argument is refused; null accepts it.
    /// </summary>
    public Func<string, string?>? Validator { get; set; }

    #endregion // Properties


    #region Constructors

    public TokenField()
    {
        Tokens.CollectionChanged += (_, _) =>
        {
            if (!_rebuilding) Rebuild(popLast: false);
        };
    }

    #endregion // Constructors


    #region Public methods

    /// <summary>
    /// Adds arguments typed or picked; repeats are kept, as ExifTool reads them.
    /// </summary>
    public void Add(string text)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return;

        _rebuilding = true;
        foreach (var part in parts) Tokens.Add(part);
        _rebuilding = false;
        Rebuild(popLast: true);
    }


    /// <summary>
    /// Removes the last argument the user owns; locked ones stay.
    /// </summary>
    public bool RemoveLast()
    {
        for (var i = Tokens.Count - 1; i >= 0; i--)
        {
            if (IsLocked(Tokens[i])) continue;

            Tokens.RemoveAt(i);
            return true;
        }

        return false;
    }

    #endregion // Public methods


    #region Protected methods

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        _input?.RemoveHandler(KeyDownEvent, Input_KeyDown);

        base.OnApplyTemplate(e);

        _panel = e.NameScope.Find<WrapPanel>("PART_Panel");
        _suggestionPanel = e.NameScope.Find<WrapPanel>("PART_Suggestions");
        _input = e.NameScope.Find<TextBox>("PART_Input");
        if (_input is not null) _input.AddHandler(KeyDownEvent, Input_KeyDown, RoutingStrategies.Tunnel);

        Rebuild(popLast: false);
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == LockedTokensProperty || change.Property == SuggestionsProperty) Rebuild(popLast: false);
    }


    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _input?.Focus();
    }

    #endregion // Protected methods


    #region Private methods

    private bool IsLocked(string token) => LockedTokens?.Contains(token) == true;


    private void Rebuild(bool popLast)
    {
        if (_panel is null || _input is null) return;

        // the input stays put, so it keeps the keyboard focus while chips come and go
        for (var i = _panel.Children.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(_panel.Children[i], _input)) _panel.Children.RemoveAt(i);
        }

        if (!_panel.Children.Contains(_input)) _panel.Children.Add(_input);

        for (var i = 0; i < Tokens.Count; i++)
        {
            var token = Tokens[i];
            var chip = new TokenChip
            {
                Text = token,
                IsLocked = IsLocked(token),
                Error = Validator?.Invoke(token),
                Index = i,
            };
            chip.Classes.Set("value", !token.StartsWith('-'));

            var tip = chip.IsLocked ? UiText.LOCKED_ARGUMENT : chip.Error ?? DescriptionProvider?.Invoke(token);
            if (tip is not null) ToolTip.SetTip(chip, tip);

            chip.RemoveRequested += Chip_RemoveRequested;
            if (popLast && i == Tokens.Count - 1) Pop(chip);
            _panel.Children.Insert(i, chip);
        }

        if (_suggestionPanel is null) return;

        _suggestionPanel.Children.Clear();
        if (Suggestions is null) return;

        foreach (var suggestion in Suggestions)
        {
            if (Tokens.Contains(suggestion)) continue;

            var chip = new TokenChip { Text = suggestion, IsSuggestion = true };
            chip.Classes.Add("suggestion");
            if (DescriptionProvider?.Invoke(suggestion) is { } tip) ToolTip.SetTip(chip, tip);
            chip.Click += Suggestion_Click;
            _suggestionPanel.Children.Add(chip);
        }
    }


    // DS 6.15: chips spring in as they are added
    private static void Pop(Control chip)
    {
        if (MotionTokens.IsFrozen) return;

        chip.RenderTransform = POP_FROM;
        chip.Opacity = 0;
        chip.AttachedToVisualTree += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            chip.Transitions =
            [
                new TransformOperationsTransition { Property = RenderTransformProperty, Duration = MotionTokens.TokenPop, Easing = MotionTokens.TokenPopEasing },
                new DoubleTransition { Property = OpacityProperty, Duration = MotionTokens.Fade, Easing = MotionTokens.FadeEasing },
            ];
            chip.RenderTransform = POP_TO;
            chip.Opacity = 1;
        }, Avalonia.Threading.DispatcherPriority.Render);
    }

    #endregion // Private methods


    #region Event handlers

    private void Input_KeyDown(object? sender, KeyEventArgs e)
    {
        if (_input is null) return;

        if (e.Key is Key.Enter or Key.Space)
        {
            if (!string.IsNullOrWhiteSpace(_input.Text))
            {
                Add(_input.Text);
                _input.Text = string.Empty;
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Back && string.IsNullOrEmpty(_input.Text))
        {
            RemoveLast();
            e.Handled = true;
        }
    }


    private void Chip_RemoveRequested(object? sender, EventArgs e)
    {
        if (sender is TokenChip { IsLocked: false, Index: >= 0 } chip && chip.Index < Tokens.Count) Tokens.RemoveAt(chip.Index);
        _input?.Focus();
    }


    private void Suggestion_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is TokenChip chip && chip.Text is { } text) Add(text);
        _input?.Focus();
    }

    #endregion // Event handlers
}


/// <summary>
/// One argument chip of a TokenField: 26 high, radius 12, mono 11.5; flags on Accent 10% in AccentText, values on Accent2 10%.
/// </summary>
public class TokenChip : Button
{
    #region Fields and constants

    public const string REMOVE_LABEL = UiText.REMOVE;

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<TokenChip, string?>(nameof(Text));

    public static readonly StyledProperty<bool> IsLockedProperty =
        AvaloniaProperty.Register<TokenChip, bool>(nameof(IsLocked));

    public static readonly StyledProperty<string?> ErrorProperty =
        AvaloniaProperty.Register<TokenChip, string?>(nameof(Error));

    public static readonly StyledProperty<bool> IsSuggestionProperty =
        AvaloniaProperty.Register<TokenChip, bool>(nameof(IsSuggestion));

    private Button? _remove;

    #endregion // Fields and constants


    #region Properties

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsLocked
    {
        get => GetValue(IsLockedProperty);
        set => SetValue(IsLockedProperty, value);
    }

    /// <summary>
    /// Why the argument is refused (D1); the chip turns red and the tooltip says it.
    /// </summary>
    public string? Error
    {
        get => GetValue(ErrorProperty);
        set => SetValue(ErrorProperty, value);
    }

    public bool IsSuggestion
    {
        get => GetValue(IsSuggestionProperty);
        set => SetValue(IsSuggestionProperty, value);
    }

    public int Index { get; init; } = -1;

    protected override Type StyleKeyOverride => typeof(TokenChip);

    #endregion // Properties


    #region Events

    public event EventHandler? RemoveRequested;

    #endregion // Events


    #region Constructors

    public TokenChip()
    {
        Focusable = false;
    }

    #endregion // Constructors


    #region Protected methods

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_remove is not null) _remove.Click -= Remove_Click;

        base.OnApplyTemplate(e);

        _remove = e.NameScope.Find<Button>("PART_Remove");
        if (_remove is not null) _remove.Click += Remove_Click;
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsLockedProperty) PseudoClasses.Set(":locked", IsLocked);
        else if (change.Property == ErrorProperty) PseudoClasses.Set(":invalid", Error is not null);
    }

    #endregion // Protected methods


    #region Event handlers

    private void Remove_Click(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RemoveRequested?.Invoke(this, EventArgs.Empty);
    }

    #endregion // Event handlers
}
