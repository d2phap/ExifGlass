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
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;

namespace ExifGlass.UI.Controls;

/// <summary>
/// One colour of a SwatchPicker: its name (tooltip and screen readers), the colour, and an icon drawn on it.
/// </summary>
public sealed record SwatchItem(string Label, Color Color, IconId? Icon = null);


/// <summary>
/// A row of round colour swatches, one always picked (Settings' accent, Appendix A item 84): a ListBox, so the arrow keys move the choice.
/// </summary>
public class SwatchPicker : ListBox
{
    #region Properties

    protected override Type StyleKeyOverride => typeof(SwatchPicker);

    #endregion // Properties


    #region Protected methods

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) => NeedsContainer<Swatch>(item, out recycleKey);


    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new Swatch();


    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is not Swatch swatch || item is not SwatchItem choice) return;

        swatch.Color = choice.Color;
        swatch.Icon = choice.Icon;
        ToolTip.SetTip(swatch, choice.Label);
        AutomationProperties.SetName(swatch, choice.Label);
    }

    #endregion // Protected methods
}


/// <summary>
/// A 24 px colour dot in a 32 px hit area: it grows on hover, shrinks while pressed; picked, a 2 px ring 2 px around it.
/// </summary>
public class Swatch : ListBoxItem
{
    #region Fields and constants

    public static readonly StyledProperty<Color> ColorProperty =
        AvaloniaProperty.Register<Swatch, Color>(nameof(Color));

    public static readonly StyledProperty<IconId?> IconProperty =
        AvaloniaProperty.Register<Swatch, IconId?>(nameof(Icon));

    #endregion // Fields and constants


    #region Properties

    public Color Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public IconId? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(Swatch);

    #endregion // Properties


    #region Protected methods

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ColorProperty)
        {
            Background = new SolidColorBrush(Color);
            Foreground = new SolidColorBrush(Tokens.AccentPalette.OnColor(Color));
        }
        else if (change.Property == IconProperty) PseudoClasses.Set(":icon", Icon is not null);
    }

    #endregion // Protected methods
}
