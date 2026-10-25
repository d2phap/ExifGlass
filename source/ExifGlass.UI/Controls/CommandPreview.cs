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
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using ExifGlass.Core.Editing;

namespace ExifGlass.UI.Controls;

/// <summary>
/// Settings' command preview (DS 6.15): the read command in a mono well that adapts to the theme, wrapped, its tokens coloured as in the command bar.
/// </summary>
public class CommandPreview : TemplatedControl
{
    #region Fields and constants

    public static readonly StyledProperty<IReadOnlyList<CommandToken>?> TokensProperty =
        AvaloniaProperty.Register<CommandPreview, IReadOnlyList<CommandToken>?>(nameof(Tokens));

    private const double LINE_HEIGHT = 1.7;

    private static readonly string ZERO_WIDTH_SPACE = char.ConvertFromUtf32(0x200B);

    private TextBlock? _text;

    #endregion // Fields and constants


    #region Constructors

    public CommandPreview()
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
    /// Gets the command as one line, as Copy in the command bar gives it.
    /// </summary>
    public string CommandLine => Tokens is { } tokens ? WritePlan.ToCommandLine(tokens) : string.Empty;

    #endregion // Properties


    #region Protected methods

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _text = e.NameScope.Find<TextBlock>("PART_Text");

        // Proto: line-height 1.7 of the mono size, whichever size this OS's mono token has
        if (_text is not null) _text.LineHeight = Math.Round(_text.FontSize * LINE_HEIGHT * 2) / 2;
        BuildInlines();
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TokensProperty) BuildInlines();
    }

    #endregion // Protected methods


    #region Private methods

    private void BuildInlines()
    {
        if (_text is null) return;

        var inlines = new InlineCollection();
        var tokens = Tokens ?? [];
        for (var i = 0; i < tokens.Count; i++)
        {
            if (i > 0) inlines.Add(new Run(" "));

            var token = tokens[i];
            var key = CommandPanel.KeyOf(token);
            var text = token.Kind is CommandTokenKind.Executable or CommandTokenKind.Path or CommandTokenKind.Value ? Breakable(token.Text) : token.Text;
            inlines.Add(new Run(text) { Foreground = this.TryFindResource(key, ActualThemeVariant, out var brush) ? brush as IBrush : null });
        }

        _text.Inlines = inlines;
    }


    // a path wraps at its separators (Proto: overflow-wrap anywhere), not after "D:" with the rest overflowing
    private static string Breakable(string path) => path.Replace("\\", "\\" + ZERO_WIDTH_SPACE, StringComparison.Ordinal).Replace("/", "/" + ZERO_WIDTH_SPACE, StringComparison.Ordinal);

    #endregion // Private methods
}
