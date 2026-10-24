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
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using ExifGlass.Core.Editing;
using ExifGlass.UI.Tokens;
using System.Windows.Input;

namespace ExifGlass.UI.Controls;

/// <summary>
/// The command bar (DS 6.12): terminal tile, the Write chip while edits are staged, mono tokens in their colours; one line collapsed, wrapped up to 140 then scrolling expanded (360 ms), Copy beside Expand or stacked above it.
/// </summary>
public class CommandPanel : TemplatedControl
{
    #region Fields and constants

    private const string PLACEHOLDER = "<file>";

    // DS 6.12: one 20 px line collapsed, at most 140 expanded
    private const double LINE = 20;
    private const double MAX_TEXT = 140;

    // Proto: the typed command adds 3 characters a frame
    private const int TYPE_STEP = 3;
    private static readonly TimeSpan TypeFrame = TimeSpan.FromMilliseconds(16);

    public static readonly StyledProperty<IReadOnlyList<CommandToken>?> TokensProperty =
        AvaloniaProperty.Register<CommandPanel, IReadOnlyList<CommandToken>?>(nameof(Tokens));

    public static readonly StyledProperty<bool> IsWriteProperty =
        AvaloniaProperty.Register<CommandPanel, bool>(nameof(IsWrite));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<CommandPanel, bool>(nameof(IsExpanded), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsTypingProperty =
        AvaloniaProperty.Register<CommandPanel, bool>(nameof(IsTyping));

    public static readonly StyledProperty<ICommand?> CopyCommandProperty =
        AvaloniaProperty.Register<CommandPanel, ICommand?>(nameof(CopyCommand));

    public static readonly DirectProperty<CommandPanel, string?> CommandLineProperty =
        AvaloniaProperty.RegisterDirect<CommandPanel, string?>(nameof(CommandLine), p => p.CommandLine);

    public static readonly DirectProperty<CommandPanel, string> ToggleTipProperty =
        AvaloniaProperty.RegisterDirect<CommandPanel, string>(nameof(ToggleTip), p => p.ToggleTip);

    private SelectableTextBlock? _text;
    private Control? _textHost;
    private ScrollViewer? _scroller;
    private DispatcherTimer? _typer;
    private string? _commandLine;
    private int _typed = int.MaxValue;
    private int _unwrapGeneration;

    #endregion // Fields and constants


    #region Constructors

    public CommandPanel()
    {
        ActualThemeVariantChanged += (_, _) => BuildInlines();
    }

    #endregion // Constructors


    #region Properties

    public IReadOnlyList<CommandToken>? Tokens
    {
        get => GetValue(TokensProperty);
        set => SetValue(TokensProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the tokens are the write Save runs (the Write chip shows).
    /// </summary>
    public bool IsWrite
    {
        get => GetValue(IsWriteProperty);
        set => SetValue(IsWriteProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the command types itself in, as it does while a file is read (DS 7.1); off shows it whole.
    /// </summary>
    public bool IsTyping
    {
        get => GetValue(IsTypingProperty);
        set => SetValue(IsTypingProperty, value);
    }

    public ICommand? CopyCommand
    {
        get => GetValue(CopyCommandProperty);
        set => SetValue(CopyCommandProperty, value);
    }

    /// <summary>
    /// Gets the whole command as one line, the text's tooltip.
    /// </summary>
    public string? CommandLine
    {
        get => _commandLine;
        private set => SetAndRaise(CommandLineProperty, ref _commandLine, value);
    }

    public string ToggleTip => IsExpanded ? UiText.COLLAPSE_COMMAND : UiText.EXPAND_COMMAND;

    #endregion // Properties


    #region Protected methods

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_textHost is not null) _textHost.SizeChanged -= TextHost_SizeChanged;

        base.OnApplyTemplate(e);

        _text = e.NameScope.Find<SelectableTextBlock>("PART_Text");
        _textHost = e.NameScope.Find<Control>("PART_TextHost");
        _scroller = e.NameScope.Find<ScrollViewer>("PART_Scroller");
        if (_text is not null) _text.LineHeight = LINE;
        if (_textHost is not null)
        {
            _textHost.SizeChanged += TextHost_SizeChanged;
            _textHost.Height = LINE;
        }

        ApplyWrap(IsExpanded);
        BuildInlines();
        UpdatePseudoClasses();
    }


    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // the token colours resolve once the panel is in a themed tree
        BuildInlines();
        if (IsExpanded) Dispatcher.UIThread.Post(() => Resize(animate: false), DispatcherPriority.Loaded);
    }


    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopTyping();
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TokensProperty)
        {
            CommandLine = Tokens is { Count: > 0 } tokens ? WritePlan.ToCommandLine(tokens) : null;
            BuildInlines();
            if (IsExpanded) Dispatcher.UIThread.Post(() => Resize(animate: true), DispatcherPriority.Loaded);
        }
        else if (change.Property == IsExpandedProperty)
        {
            RaisePropertyChanged(ToggleTipProperty, IsExpanded ? UiText.EXPAND_COMMAND : UiText.COLLAPSE_COMMAND, ToggleTip);
            UpdatePseudoClasses();
            Expand(IsExpanded);
        }
        else if (change.Property == IsWriteProperty) UpdatePseudoClasses();
        else if (change.Property == IsTypingProperty)
        {
            if (IsTyping) StartTyping();
            else StopTyping();
        }
    }

    #endregion // Protected methods


    #region Private methods

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":expanded", IsExpanded);
        PseudoClasses.Set(":write", IsWrite);
    }


    // text wraps at once on expand and unwraps only after the collapse ends (DS 5.2)
    private void Expand(bool expanded)
    {
        var generation = ++_unwrapGeneration;
        if (expanded)
        {
            ApplyWrap(true);
            Dispatcher.UIThread.Post(() => Resize(animate: true), DispatcherPriority.Loaded);
            return;
        }

        PseudoClasses.Set(":scrolls", false);
        SetTextHeight(LINE, animate: true);
        _scroller?.ScrollToHome();
        DispatcherTimer.RunOnce(() =>
        {
            if (generation == _unwrapGeneration && !IsExpanded) ApplyWrap(false);
        }, MotionTokens.IsFrozen ? TimeSpan.FromMilliseconds(1) : MotionTokens.CommandBar);
    }


    private void ApplyWrap(bool wrap)
    {
        if (_text is null) return;

        _text.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        _text.TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis;
    }


    // the wrapped text's height at the host's width, 20 to 140
    private void Resize(bool animate)
    {
        if (!IsExpanded || _text is null || _textHost is null || _textHost.Bounds.Width <= 0) return;

        _text.Measure(new Size(_textHost.Bounds.Width, double.PositiveInfinity));
        var height = Math.Ceiling(_text.DesiredSize.Height);

        // a scroll bar only past 140: one that came from rounding would be too short for its arrows
        PseudoClasses.Set(":scrolls", height > MAX_TEXT);
        SetTextHeight(Math.Clamp(height, LINE, MAX_TEXT), animate);
    }


    private void SetTextHeight(double height, bool animate)
    {
        if (_textHost is null || Math.Abs(_textHost.Height - height) < 0.5) return;

        // a layout glide on the UI thread (plan 8: layout motion), not composition: the panels above give way to it
        _textHost.Transitions = animate && !MotionTokens.IsFrozen
            ? [new DoubleTransition { Property = HeightProperty, Duration = MotionTokens.CommandBar, Easing = MotionTokens.CommandBarEasing }]
            : null;
        _textHost.Height = height;
    }


    // executable TextSecondary, flags and writes AccentText, values Accent2, the file TextPrimary, the <file> placeholder TextTertiary
    private void BuildInlines()
    {
        if (_text is null) return;

        var tokens = Tokens ?? [];
        var inlines = new InlineCollection();
        var left = _typed;

        for (var i = 0; i < tokens.Count && left > 0; i++)
        {
            if (i > 0)
            {
                inlines.Add(new Run(" "));
                left--;
            }

            var token = tokens[i];

            // a value with line breaks stays on one line here; Copy keeps them
            var text = token.Text.Replace("\r\n", " ↵ ", StringComparison.Ordinal).Replace('\n', '↵');
            if (text.Length > left) text = text[..left];
            left -= text.Length;
            inlines.Add(new Run(text) { Foreground = Brush(KeyOf(token)) });
        }

        _text.Inlines = inlines;
    }


    private void StartTyping()
    {
        _typed = 0;
        BuildInlines();
        if (MotionTokens.IsFrozen)
        {
            StopTyping();
            return;
        }

        _typer ??= new DispatcherTimer(TypeFrame, DispatcherPriority.Render, Typer_Tick);
        _typer.Start();
    }


    private void StopTyping()
    {
        _typer?.Stop();
        if (_typed == int.MaxValue) return;

        _typed = int.MaxValue;
        BuildInlines();
    }


    private static string KeyOf(CommandToken token) => token.Kind switch
    {
        CommandTokenKind.Executable => TokenKeys.TEXT_SECONDARY_BRUSH,
        CommandTokenKind.Flag or CommandTokenKind.Write => TokenKeys.ACCENT_TEXT_BRUSH,
        CommandTokenKind.Value => TokenKeys.ACCENT2_TEXT_BRUSH,
        _ => token.Text == PLACEHOLDER ? TokenKeys.TEXT_TERTIARY_BRUSH : TokenKeys.TEXT_PRIMARY_BRUSH,
    };


    private IBrush? Brush(string key) => this.TryFindResource(key, ActualThemeVariant, out var value) ? value as IBrush : null;

    #endregion // Private methods


    #region Event handlers

    private void Typer_Tick(object? sender, EventArgs e)
    {
        var length = CommandLine?.Length ?? 0;
        _typed = Math.Min(_typed + TYPE_STEP, length);
        BuildInlines();

        if (_typed >= length) _typer?.Stop();
    }


    private void TextHost_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // a new width wraps the text again; the glide that changed the height is left alone
        if (e.WidthChanged && IsExpanded) Resize(animate: false);
    }

    #endregion // Event handlers
}
