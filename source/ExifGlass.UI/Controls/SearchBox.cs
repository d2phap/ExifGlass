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
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ExifGlass.UI.Controls;

/// <summary>
/// The search box (DS 6.2): leading search icon, the Ctrl+K hint while empty, a clear button while not; Esc clears the text, then drops the focus.
/// </summary>
public class SearchBox : TextBox
{
    #region Fields and constants

    public const string CLEAR_LABEL = UiText.CLEAR_SEARCH;

    public static readonly StyledProperty<string?> ShortcutHintProperty =
        AvaloniaProperty.Register<SearchBox, string?>(nameof(ShortcutHint), UiText.Shortcut("K"));

    private Button? _clearButton;

    #endregion // Fields and constants


    #region Properties

    public string? ShortcutHint
    {
        get => GetValue(ShortcutHintProperty);
        set => SetValue(ShortcutHintProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(SearchBox);

    #endregion // Properties


    #region Constructors

    public SearchBox()
    {
        PseudoClasses.Set(":empty", true);
    }

    #endregion // Constructors


    #region Protected methods

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_clearButton is not null) _clearButton.Click -= ClearButton_Click;

        base.OnApplyTemplate(e);

        _clearButton = e.NameScope.Find<Button>("PART_ClearButton");
        if (_clearButton is not null) _clearButton.Click += ClearButton_Click;
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty) PseudoClasses.Set(":empty", string.IsNullOrEmpty(Text));
    }


    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrEmpty(Text)) SetCurrentValue(TextProperty, string.Empty);
            else TopLevel.GetTopLevel(this)?.FocusManager?.Focus(null);

            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    #endregion // Protected methods


    #region Event handlers

    private void ClearButton_Click(object? sender, RoutedEventArgs e)
    {
        SetCurrentValue(TextProperty, string.Empty);
        Focus();
    }

    #endregion // Event handlers
}
