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

namespace ExifGlass.Views;

/// <summary>
/// The app toolbar's row: the first child at the left, the second at the right, and the third centred on the row, narrowed so it keeps Spacing from both, or filling the gap with FillsGap (owner at G4 review).
/// </summary>
public class ToolbarRow : Panel
{
    #region Fields and constants

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<ToolbarRow, double>(nameof(Spacing));

    public static readonly StyledProperty<bool> FillsGapProperty =
        AvaloniaProperty.Register<ToolbarRow, bool>(nameof(FillsGap));

    #endregion // Fields and constants


    #region Properties

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the third child fills the space between the others instead of staying centred.
    /// </summary>
    public bool FillsGap
    {
        get => GetValue(FillsGapProperty);
        set => SetValue(FillsGapProperty, value);
    }

    #endregion // Properties


    #region Constructors

    static ToolbarRow()
    {
        AffectsMeasure<ToolbarRow>(SpacingProperty, FillsGapProperty);
    }

    #endregion // Constructors


    #region Protected methods

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 3) return base.MeasureOverride(availableSize);

        Children[0].Measure(availableSize);
        Children[1].Measure(availableSize);
        var width = double.IsFinite(availableSize.Width)
            ? availableSize.Width
            : Children[0].DesiredSize.Width + Children[1].DesiredSize.Width + 2 * Spacing + Children[2].MinWidth;

        Children[2].Measure(new Size(Place(width, availableSize.Height).Centre.Width, availableSize.Height));

        var height = Math.Max(Children[2].DesiredSize.Height, Math.Max(Children[0].DesiredSize.Height, Children[1].DesiredSize.Height));
        return new Size(width, double.IsFinite(availableSize.Height) ? availableSize.Height : height);
    }


    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 3) return base.ArrangeOverride(finalSize);

        var (left, right, centre) = Place(finalSize.Width, finalSize.Height);
        Children[0].Arrange(left);
        Children[1].Arrange(right);
        Children[2].Arrange(centre);

        return finalSize;
    }

    #endregion // Protected methods


    #region Private methods

    // the centre child is as wide as it can be while centred, within its Min/MaxWidth; below its MinWidth it moves off centre toward the wider gap
    private (Rect Left, Rect Right, Rect Centre) Place(double width, double height)
    {
        var leftWidth = Children[0].DesiredSize.Width;
        var rightWidth = Children[1].DesiredSize.Width;
        var centre = Children[2];

        var from = leftWidth + Spacing;
        var to = width - rightWidth - Spacing;
        var middle = width / 2;
        var fit = FillsGap ? to - from : 2 * Math.Min(middle - from, to - middle);
        var centreWidth = Math.Clamp(fit, centre.MinWidth, Math.Max(centre.MinWidth, centre.MaxWidth));
        var x = Math.Max(from, Math.Min(middle - centreWidth / 2, to - centreWidth));

        return (new Rect(0, 0, leftWidth, height), new Rect(width - rightWidth, 0, rightWidth, height), new Rect(x, 0, centreWidth, height));
    }

    #endregion // Private methods
}
