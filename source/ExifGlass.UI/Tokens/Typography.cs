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
using Avalonia.Data;
using Avalonia.Media;

namespace ExifGlass.UI.Tokens;

/// <summary>
/// A role's values from DS 1.1; mono sizes are the Windows ones.
/// </summary>
public readonly record struct TextSpec(double Size, FontWeight Weight, bool Mono, double LineHeight, double LetterSpacing, bool Tabular);


/// <summary>
/// Sets the type ramp on a TextBlock in code: as class styles, every TextBlock carried an instance of all 16 (G6 finding 2).
/// </summary>
public static partial class Typography
{
    #region Fields and constants

    public static readonly AttachedProperty<TextRole> RoleProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, TextRole>("Role", typeof(Typography));

    // what the role set, undone when it changes
    private static readonly AttachedProperty<IDisposable?[]?> AppliedProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, IDisposable?[]?>("Applied", typeof(Typography));

    private static readonly FontFeatureCollection TABULAR = [FontFeature.Parse("+tnum")];

    private static FontFamily? _mono;

    #endregion // Fields and constants


    #region Constructors

    static Typography() => RoleProperty.Changed.AddClassHandler<TextBlock>((text, e) => Apply(text, e.GetNewValue<TextRole>()));

    #endregion // Constructors


    #region Public methods

    public static TextRole GetRole(TextBlock text) => text.GetValue(RoleProperty);


    public static void SetRole(TextBlock text, TextRole role) => text.SetValue(RoleProperty, role);

    #endregion // Public methods


    #region Private methods

    private static void Apply(TextBlock text, TextRole role)
    {
        // the size first, in Set's order: off Windows its removal must give back the inherited size, not a coerced default
        if (text.GetValue(AppliedProperty) is { } applied)
        {
            AppFonts.ReleaseSize(applied[0]);
            foreach (var value in applied.AsSpan(1)) value?.Dispose();
        }

        if (role == TextRole.None)
        {
            text.ClearValue(AppliedProperty);
            return;
        }

        // style priority, as the class styles had: a value set on the element, by its template or by a state style still wins
        var spec = Spec(role);
        IDisposable? Set<T>(StyledProperty<T> property, T value) => text.SetValue(property, value, BindingPriority.Style);

        text.SetValue(AppliedProperty,
        [
            // off Windows the -1 pt coercion takes the point back off mono sizes (S3)
            Set(TextBlock.FontSizeProperty, spec.Mono ? spec.Size - AppFonts.SizeOffset : spec.Size),
            Set(TextBlock.FontWeightProperty, spec.Weight),
            spec.Mono ? Set(TextBlock.FontFamilyProperty, _mono ??= new FontFamily(AppFonts.Mono)) : null,
            double.IsNaN(spec.LineHeight) ? null : Set(TextBlock.LineHeightProperty, spec.LineHeight),
            spec.LetterSpacing == 0 ? null : Set(TextBlock.LetterSpacingProperty, spec.LetterSpacing),
            spec.Tabular ? Set(TextBlock.FontFeaturesProperty, TABULAR) : null,
        ]);
    }

    #endregion // Private methods
}
