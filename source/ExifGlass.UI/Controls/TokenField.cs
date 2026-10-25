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
/// What a chip of a TokenField says about itself: its hover description, why it's refused, whether the app owns it.
/// </summary>
public readonly record struct TokenNote(string? Description, string? Error = null, bool IsLocked = false);


/// <summary>
/// The argument token field (DS 6.15, D1): a chip per argument, Enter or Space adds, Backspace removes the last, hover shows the description, locked chips for app-owned flags, dashed suggestions.
/// </summary>
public class TokenField : TemplatedControl
{
    #region Fields and constants

    public static readonly StyledProperty<IList<string>?> SuggestionsProperty =
        AvaloniaProperty.Register<TokenField, IList<string>?>(nameof(Suggestions));

    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<TokenField, string?>(nameof(Placeholder), UiText.ADD_ARGUMENT);

    public static readonly StyledProperty<object?> NoteProperty =
        AvaloniaProperty.Register<TokenField, object?>(nameof(Note));

    private static readonly TransformOperations POP_FROM = TransformOperations.Parse("scale(0.6)");
    private static readonly TransformOperations POP_TO = TransformOperations.Parse("scale(1)");

    private WrapPanel? _panel;
    private WrapPanel? _suggestionPanel;
    private TextBox? _input;
    private bool _rebuilding;
    private TokenNote[] _notes = [];

    #endregion // Fields and constants


    #region Properties

    /// <summary>
    /// Gets the arguments in order; edit it to change the chips.
    /// </summary>
    public AvaloniaList<string> Tokens { get; } = [];

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
    /// What shows between the field and its suggestions: Settings' hint or a refused argument (Proto).
    /// </summary>
    public object? Note
    {
        get => GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    /// <summary>
    /// Gets or sets what each chip says, from all the tokens and its index, so a value can belong to the flag before it (D1).
    /// </summary>
    public Func<IReadOnlyList<string>, int, TokenNote>? NoteProvider { get; set; }

    /// <summary>
    /// Gets or sets a suggestion's short label after its text, such as "raw values" (Proto).
    /// </summary>
    public Func<string, string?>? SuggestionHint { get; set; }

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
        var notes = Notes();
        for (var i = Tokens.Count - 1; i >= 0; i--)
        {
            if (notes[i].IsLocked) continue;

            Tokens.RemoveAt(i);
            return true;
        }

        return false;
    }


    /// <summary>
    /// Replaces every argument at once, without the chips' spring.
    /// </summary>
    public void SetTokens(IEnumerable<string> tokens)
    {
        _rebuilding = true;
        Tokens.Clear();
        Tokens.AddRange(tokens);
        _rebuilding = false;
        Rebuild(popLast: false);
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

        if (change.Property == SuggestionsProperty) Rebuild(popLast: false);
    }


    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _input?.Focus();
    }

    #endregion // Protected methods


    #region Private methods

    private TokenNote[] Notes()
    {
        var notes = new TokenNote[Tokens.Count];
        if (NoteProvider is not { } provider) return notes;

        for (var i = 0; i < notes.Length; i++) notes[i] = provider(Tokens, i);
        return notes;
    }


    private void Rebuild(bool popLast)
    {
        _notes = Notes();
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
            var note = _notes[i];
            var chip = new TokenChip
            {
                Text = token,
                IsLocked = note.IsLocked,
                Error = note.Error,
                Index = i,
            };
            chip.Classes.Set("value", !token.StartsWith('-'));

            var tip = note.Error ?? note.Description;
            if (!string.IsNullOrEmpty(tip)) ToolTip.SetTip(chip, tip);

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

            var chip = new TokenChip { Text = suggestion, Hint = SuggestionHint?.Invoke(suggestion), IsSuggestion = true };
            chip.Classes.Add("suggestion");
            if (NoteProvider?.Invoke([suggestion], 0).Description is { Length: > 0 } tip) ToolTip.SetTip(chip, tip);
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

    public static readonly StyledProperty<string?> HintProperty =
        AvaloniaProperty.Register<TokenChip, string?>(nameof(Hint));

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

    /// <summary>
    /// A suggestion's short label in TextSecondary after its text (Proto: "-n raw values").
    /// </summary>
    public string? Hint
    {
        get => GetValue(HintProperty);
        set => SetValue(HintProperty, value);
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
