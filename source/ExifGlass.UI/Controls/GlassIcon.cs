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
using Avalonia.Media.Immutable;

namespace ExifGlass.UI.Controls;

/// <summary>
/// An icon's drawing facts, generated from its SVG source: box origin and size, stroke width, which geometries it has.
/// </summary>
public readonly record struct IconSpec(double OriginX, double OriginY, double Box, double StrokeWidth, bool HasStroke, bool HasFill, bool HasCut);


/// <summary>
/// Draws an IconId in its Foreground (plan 5.8): Fluent 2 round-capped strokes and fills scaled from the 20-unit box, so colour changes animate with the text.
/// </summary>
public class GlassIcon : Control
{
    #region Fields and constants

    public static readonly StyledProperty<IconId?> IconProperty =
        AvaloniaProperty.Register<GlassIcon, IconId?>(nameof(Icon));

    /// <summary>
    /// Inherited like text, so an icon takes its button's text colour.
    /// </summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<GlassIcon>();

    private Geometry? _stroke;
    private Geometry? _fill;
    private IconSpec _spec;
    private IconId? _resolved;
    private IPen? _pen;

    #endregion // Fields and constants


    #region Properties

    public IconId? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    #endregion // Properties


    #region Constructors

    static GlassIcon()
    {
        AffectsRender<GlassIcon>(IconProperty, ForegroundProperty);
        AffectsMeasure<GlassIcon>(IconProperty);
        WidthProperty.OverrideDefaultValue<GlassIcon>(16);
        HeightProperty.OverrideDefaultValue<GlassIcon>(16);
        IsHitTestVisibleProperty.OverrideDefaultValue<GlassIcon>(false);
    }

    #endregion // Constructors


    #region Public methods

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0 || Foreground is not { } brush || Icon is not { } icon) return;

        Resolve(icon);

        var scale = size / _spec.Box;
        var matrix = Matrix.CreateTranslation(-_spec.OriginX, -_spec.OriginY)
            * Matrix.CreateScale(scale, scale)
            * Matrix.CreateTranslation((Bounds.Width - size) / 2, (Bounds.Height - size) / 2);

        using (context.PushTransform(matrix))
        {
            if (_fill is not null) context.DrawGeometry(brush, null, _fill);
            if (_stroke is not null) context.DrawGeometry(null, PenFor(brush), _stroke);
        }
    }

    #endregion // Public methods


    #region Private methods

    // a theme switch or a brush transition hands a new brush each time; the pen follows it
    private IPen PenFor(IBrush brush)
    {
        if (_pen is not null && ReferenceEquals(_pen.Brush, brush)) return _pen;

        _pen = brush is IImmutableBrush immutable
            ? new ImmutablePen(immutable, _spec.StrokeWidth, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round)
            : new Pen(brush, _spec.StrokeWidth, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        return _pen;
    }


    private void Resolve(IconId icon)
    {
        if (_resolved == icon) return;

        _resolved = icon;
        _pen = null;
        _spec = IconCatalog.Get(icon);
        _stroke = _spec.HasStroke ? Find($"Icon.{icon}.Stroke") : null;
        _fill = _spec.HasFill ? Find($"Icon.{icon}.Fill") : null;

        // a cut is a stroke knocked out of the fill, so the glyph shows what's behind it
        if (_fill is not null && _spec.HasCut && Find($"Icon.{icon}.Cut") is { } cut)
        {
            var pen = new Pen(Brushes.Black, _spec.StrokeWidth, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _fill = new CombinedGeometry(GeometryCombineMode.Exclude, _fill, cut.GetWidenedGeometry(pen));
        }
    }


    private Geometry? Find(string key) => this.TryFindResource(key, out var value) ? value as Geometry : null;

    #endregion // Private methods
}
