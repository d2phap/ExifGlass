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
using Avalonia.Media;
using ExifGlass.Core.Editing;
using ExifGlass.UI.Tokens;
using ExifGlass.ViewModels;
using System.ComponentModel;

namespace ExifGlass.Views;

/// <summary>
/// The command bar's place under the panels (DS 6.12): the terminal-equivalent read command in the token colours.
/// </summary>
public partial class CommandBar : UserControl
{
    #region Fields and constants

    private const string PLACEHOLDER = "<file>";

    private MainViewModel? _viewModel;

    #endregion // Fields and constants


    #region Constructors

    public CommandBar()
    {
        InitializeComponent();
        ActualThemeVariantChanged += (_, _) => BuildInlines();
    }

    #endregion // Constructors


    #region Protected methods

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null) _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel = DataContext as MainViewModel;
        if (_viewModel is not null) _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        BuildInlines();
    }


    // token colours come from the theme's resources, which resolve only once in a tree
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        BuildInlines();
    }

    #endregion // Protected methods


    #region Private methods

    // executable TextSecondary, flags AccentText, values Accent2, file TextPrimary, the <file> placeholder TextTertiary
    private void BuildInlines()
    {
        var tokens = _viewModel?.CommandTokens ?? [];
        var inlines = new InlineCollection();

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (i > 0) inlines.Add(new Run(" "));
            inlines.Add(new Run(token.Text) { Foreground = Brush(KeyOf(token)) });
        }

        CommandText.Inlines = inlines;
        ToolTip.SetTip(CommandText, tokens.Count > 0 ? WritePlan.ToCommandLine(tokens) : null);
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

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CommandTokens)) BuildInlines();
    }

    #endregion // Event handlers
}
