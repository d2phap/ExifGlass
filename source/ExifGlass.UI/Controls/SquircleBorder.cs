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
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using ExifGlass.UI.Tokens;
using SkiaSharp;

namespace ExifGlass.UI.Controls;

/// <summary>
/// A Border whose corners take the inherited CornerShape: the squircle (O1), or round where a shape must stay round; shadows included.
/// </summary>
public class SquircleBorder : Decorator
{
    #region Fields and constants

    private static readonly ShadowPart[] NO_SHADOWS = [];

    // a box holds its round rects until its size has stood still this long
    private static readonly TimeSpan RESIZE_SETTLE = TimeSpan.FromMilliseconds(150);

    /// <summary>
    /// Squircle everywhere; inherited, so a panel set to Round rounds everything inside it.
    /// </summary>
    public static readonly AttachedProperty<CornerShape> CornerShapeProperty =
        AvaloniaProperty.RegisterAttached<SquircleBorder, Visual, CornerShape>("CornerShape", CornerShape.Squircle, inherits: true);

    public static readonly StyledProperty<IBrush?> BackgroundProperty = Border.BackgroundProperty.AddOwner<SquircleBorder>();

    public static readonly StyledProperty<IBrush?> BorderBrushProperty = Border.BorderBrushProperty.AddOwner<SquircleBorder>();

    public static readonly StyledProperty<Thickness> BorderThicknessProperty = Border.BorderThicknessProperty.AddOwner<SquircleBorder>();

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty = Border.CornerRadiusProperty.AddOwner<SquircleBorder>();

    public static readonly StyledProperty<BoxShadows> BoxShadowProperty = Border.BoxShadowProperty.AddOwner<SquircleBorder>();

    /// <summary>
    /// Dashes the border (suggestion chips, drop targets); attached, so Border#Name styles can set it.
    /// </summary>
    public static readonly AttachedProperty<AvaloniaList<double>?> BorderDashArrayProperty =
        AvaloniaProperty.RegisterAttached<SquircleBorder, Visual, AvaloniaList<double>?>("BorderDashArray");

    private Shapes? _shapes;
    private ShadowPart[] _shadows = NO_SHADOWS;
    private ShadowKey? _shadowKey;
    private IPen? _pen;
    private bool _ownsClip;
    private Size _drawnSize;
    private bool _resizing;
    private DispatcherTimer? _settle;
    private SKImage? _ringImage;
    private RingKey? _ringKey;

    #endregion // Fields and constants


    #region Properties

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public IBrush? BorderBrush
    {
        get => GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public Thickness BorderThickness
    {
        get => GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public BoxShadows BoxShadow
    {
        get => GetValue(BoxShadowProperty);
        set => SetValue(BoxShadowProperty, value);
    }


    // S1: Border's styles and setters apply, so template parts are styled as Border#Name (never c|SquircleBorder)
    protected override Type StyleKeyOverride => GetType() == typeof(SquircleBorder) ? typeof(Border) : base.StyleKeyOverride;


    /// <summary>
    /// Gets the largest scale the render thread draws this box at, for a box that is always scaled; above 0 a plain ring is drawn from an image of its exact squircle made at that scale.
    /// </summary>
    protected virtual double RasterScale => 0;


    private bool IsRounded => CornerRadius != default;

    #endregion // Properties


    #region Constructors

    static SquircleBorder()
    {
        AffectsRender<SquircleBorder>(BackgroundProperty, BorderBrushProperty, BorderThicknessProperty, CornerRadiusProperty, BoxShadowProperty, CornerShapeProperty, BorderDashArrayProperty);
        AffectsMeasure<SquircleBorder>(BorderThicknessProperty);
    }

    #endregion // Constructors


    #region Public methods

    public static CornerShape GetCornerShape(Visual element) => element.GetValue(CornerShapeProperty);

    public static AvaloniaList<double>? GetBorderDashArray(Visual element) => element.GetValue(BorderDashArrayProperty);

    public static void SetBorderDashArray(Visual element, AvaloniaList<double>? value) => element.SetValue(BorderDashArrayProperty, value);

    public static void SetCornerShape(Visual element, CornerShape value) => element.SetValue(CornerShapeProperty, value);


    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0) return;

        var thickness = LayoutThickness();
        if (GetBorderDashArray(this) is { Count: > 0 } dashes && thickness.IsUniform && thickness.Top > 0)
        {
            RenderDashed(context, size, thickness.Top, dashes);
            return;
        }

        if ((GetCornerShape(this) == CornerShape.Round || !IsRounded) && thickness.IsUniform)
        {
            RenderAsBorder(context, size, thickness.Top);
            return;
        }

        var shapes = ShapesFor(size);
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        TrackResize(size);

        if (RasterScale > 0 && Background is null && BoxShadow.Count == 0 && BorderBrush is { } ringBrush && Paint(ringBrush, shapes.Box) is (var ringColor, null))
        {
            RenderRingImage(context, shapes, scale, ringColor);
            return;
        }

        if (BoxShadow.Count == 0)
        {
            RenderMerged(context, shapes, scale);
            return;
        }

        RenderWithShadows(context, shapes, scale);
    }

    #endregion // Public methods


    #region Protected methods

    protected override Size MeasureOverride(Size availableSize) => LayoutHelper.MeasureChild(Child, availableSize, Padding, LayoutThickness());


    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = LayoutHelper.ArrangeChild(Child, finalSize, Padding, LayoutThickness());
        UpdateClip(finalSize);
        return size;
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ClipToBoundsProperty || change.Property == CornerRadiusProperty || change.Property == CornerShapeProperty) UpdateClip(Bounds.Size);
    }


    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // never disposed here: the render thread may still draw the last frame's, and the GC frees them
        _shadows = NO_SHADOWS;
        _shadowKey = null;

        _settle?.Stop();
        _resizing = false;
        _drawnSize = default;
        _ringImage = null;
        _ringKey = null;
    }

    #endregion // Protected methods


    #region Private methods

    // plan 5.4: a box being resized (a docked column's glide, a window resize) is moving; an exact path at each new size is a new GPU mask
    private void TrackResize(Size size)
    {
        if (_drawnSize == size) return;

        var first = _drawnSize == default;
        _drawnSize = size;
        if (first || MotionTokens.IsFrozen) return;

        _resizing = true;
        if (_settle is null)
        {
            _settle = new DispatcherTimer { Interval = RESIZE_SETTLE };
            _settle.Tick += Settle_Tick;
        }

        _settle.Stop();
        _settle.Start();
    }


    // S1: one draw op for fill and border, none for a transparent overlay at rest; hit testing keeps Border's rules
    private void RenderMerged(DrawingContext context, Shapes shapes, double scale)
    {
        SkiaPaint fill = default, edge = default;
        bool hasFill = false, hasEdge = false, filled = false;

        if (Background is { } background)
        {
            filled = true;
            if (Paint(background, shapes.Box) is var (color, shader))
            {
                if (color.Alpha > 0 || shader is not null)
                {
                    fill = new SkiaPaint(shapes.Body, null, default, color, 1, shapes.Box, shader);
                    hasFill = true;
                }
            }
            else context.DrawGeometry(background, null, shapes.Outer);
        }

        if (BorderBrush is { } brush && shapes.Ring is { } ring)
        {
            if (Paint(brush, shapes.Box) is var (border, borderShader))
            {
                if (border.Alpha > 0 || borderShader is not null)
                {
                    edge = new SkiaPaint(shapes.BorderRing, null, default, border, 1, shapes.Box, borderShader);
                    hasEdge = true;
                }
            }
            else context.DrawGeometry(brush, null, ring);
        }

        if (!hasFill && !hasEdge && !(filled && IsHitTestVisible)) return;

        SkiaPaint[] paints = (hasFill, hasEdge) switch
        {
            (true, true) => [fill, edge],
            (true, false) => [fill],
            (false, true) => [edge],
            _ => [],
        };
        context.Custom(new SkiaDraw(paints, scale, filled ? shapes.OuterHit : shapes.RingHit, shapes.Box, _resizing));
    }


    // made once per size and colour, then scaled like any image: no frame masks the path again or falls back to its holding round rects
    private void RenderRingImage(DrawingContext context, Shapes shapes, double scale, SKColor color)
    {
        var key = new RingKey(shapes, scale * RasterScale, color);
        if (_ringImage is null || _ringKey != key)
        {
            _ringKey = key;
            _ringImage = RingImage(shapes, key.Scale, color);
        }

        var destination = new SKRect(0, 0, (float)(_ringImage.Width / key.Scale), (float)(_ringImage.Height / key.Scale));
        context.Custom(new SkiaDraw([new SkiaPaint(null, _ringImage, destination, SKColors.White, 1, shapes.Box)], scale, shapes.RingHit, shapes.Box));
    }


    private static SKImage RingImage(Shapes shapes, double scale, SKColor color)
    {
        var pixels = new SKImageInfo(Math.Max(1, (int)Math.Ceiling(shapes.Box.Width * scale)), Math.Max(1, (int)Math.Ceiling(shapes.Box.Height * scale)), SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(pixels);
        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.Scale((float)scale);

        using var ring = Band.Path(shapes.Box, shapes.Radii, shapes.Shape, shapes.Inner, shapes.InnerRadii);
        using var paint = new SKPaint { IsAntialias = true, Color = color };
        surface.Canvas.DrawPath(ring, paint);
        return surface.Snapshot();
    }


    // CSS paints the first shadow on top: outset shadows, fill, inset shadows, border
    private void RenderWithShadows(DrawingContext context, Shapes shapes, double scale)
    {
        var shadows = ShadowsFor(shapes, out var alphas);

        var outset = CollectShadows(shadows, alphas, inset: false);
        if (outset.Length > 0) context.Custom(new SkiaDraw(outset, scale, null, resizing: _resizing));

        if (Background is { } background)
        {
            if (Paint(background, shapes.Box) is var (color, shader)) context.Custom(new SkiaDraw([new SkiaPaint(shapes.Body, null, default, color, 1, shapes.Box, shader)], scale, shapes.OuterHit, resizing: _resizing));
            else context.DrawGeometry(background, null, shapes.Outer);
        }

        var inset = CollectShadows(shadows, alphas, inset: true);
        if (inset.Length > 0) context.Custom(new SkiaDraw(inset, scale, null, resizing: _resizing));

        if (BorderBrush is not { } brush || shapes.Ring is not { } ring) return;

        if (Paint(brush, shapes.Box) is var (border, borderShader)) context.Custom(new SkiaDraw([new SkiaPaint(shapes.BorderRing, null, default, border, 1, shapes.Box, borderShader)], scale, shapes.RingHit, resizing: _resizing));
        else context.DrawGeometry(brush, null, ring);
    }


    private static SkiaPaint[] CollectShadows(ShadowPart[] shadows, double[] alphas, bool inset)
    {
        var count = 0;
        foreach (var s in shadows) if (s.IsInset == inset) count++;
        if (count == 0) return [];

        var paints = new SkiaPaint[count];
        var at = 0;
        for (var i = shadows.Length - 1; i >= 0; i--)
        {
            if (shadows[i].IsInset == inset) paints[at++] = shadows[i].Paint(alphas[i]);
        }

        return paints;
    }


    private static SKColor Sk(Color color, double opacity = 1) => new(color.R, color.G, color.B, (byte)Math.Round(color.A * Math.Clamp(opacity, 0, 1)));


    // the brushes the app paints boxes with, as Skia paints them; anything else Avalonia draws
    private static (SKColor Color, SKShader? Shader)? Paint(IBrush brush, Rect box)
    {
        if (brush.Transform is not null) return null;

        switch (brush)
        {
            case ISolidColorBrush solid:
                return (Sk(solid.Color, solid.Opacity), null);

            case IGradientBrush { GradientStops.Count: > 0 } gradient and (ILinearGradientBrush or IRadialGradientBrush):
            {
                var stops = new GradientStop[gradient.GradientStops.Count];
                for (var i = 0; i < stops.Length; i++)
                {
                    var stop = gradient.GradientStops[i];
                    stops[i] = new GradientStop(stop.Color, stop.Offset);
                }

                Array.Sort(stops, static (a, b) => a.Offset.CompareTo(b.Offset));
                var colors = new SKColor[stops.Length];
                var offsets = new float[stops.Length];
                for (var i = 0; i < stops.Length; i++)
                {
                    colors[i] = Sk(stops[i].Color);
                    offsets[i] = (float)stops[i].Offset;
                }

                var tile = gradient.SpreadMethod switch { GradientSpreadMethod.Reflect => SKShaderTileMode.Mirror, GradientSpreadMethod.Repeat => SKShaderTileMode.Repeat, _ => SKShaderTileMode.Clamp };
                var white = SKColors.White.WithAlpha((byte)Math.Round(255 * Math.Clamp(gradient.Opacity, 0, 1)));

                if (gradient is ILinearGradientBrush linear)
                {
                    var (start, end) = (linear.StartPoint.ToPixels(box), linear.EndPoint.ToPixels(box));
                    return (white, SKShader.CreateLinearGradient(new SKPoint((float)start.X, (float)start.Y), new SKPoint((float)end.X, (float)end.Y), colors, offsets, tile));
                }

                var radial = (IRadialGradientBrush)gradient;
                var centre = radial.Center.ToPixels(box);
                var (rx, ry) = (radial.RadiusX.ToValue(box.Width), radial.RadiusY.ToValue(box.Height));
                if (rx <= 0 || ry <= 0 || radial.GradientOrigin.ToPixels(box) != centre) return null;

                // an elliptical gradient is a circle stretched about its centre
                var matrix = SKMatrix.CreateScale(1, (float)(ry / rx), (float)centre.X, (float)centre.Y);
                return (white, SKShader.CreateRadialGradient(new SKPoint((float)centre.X, (float)centre.Y), (float)rx, colors, offsets, tile, matrix));
            }

            default:
                return null;
        }
    }


    // a dashed outline along the squircle, centred on the border's middle; few on screen, so Avalonia draws it
    private void RenderDashed(DrawingContext context, Size size, double thickness, AvaloniaList<double> dashes)
    {
        var box = new Rect(size).Deflate(thickness / 2);
        var radius = CornerRadius;
        var inner = new CornerRadius(Math.Max(0, radius.TopLeft - thickness / 2), Math.Max(0, radius.TopRight - thickness / 2), Math.Max(0, radius.BottomRight - thickness / 2), Math.Max(0, radius.BottomLeft - thickness / 2));
        var outline = Squircle.Geometry(box, Squircle.Radii(box, inner), GetCornerShape(this));

        if (Background is { } background) context.DrawGeometry(background, null, Squircle.Geometry(new Rect(size), Squircle.Radii(new Rect(size), radius), GetCornerShape(this)));
        if (BorderBrush is { } brush) context.DrawGeometry(null, new Pen(brush, thickness, new DashStyle(dashes, 0), PenLineCap.Round), outline);
    }


    // Avalonia's own Border drawing (BorderRenderHelper's uniform case), shadows included
    private void RenderAsBorder(DrawingContext context, Size size, double thickness)
    {
        var brush = BorderBrush;
        if (brush is null || thickness <= 0) _pen = null;
        else if (_pen is null || !ReferenceEquals(_pen.Brush, brush) || _pen.Thickness != thickness)
        {
            _pen = brush is IImmutableBrush immutable ? new ImmutablePen(immutable, thickness) : new Pen(brush, thickness);
        }

        var rect = new Rect(size);
        if (thickness > 0) rect = rect.Deflate(thickness / 2);

        var r = CornerRadius;
        context.DrawRectangle(Background, _pen, new RoundedRect(rect, r.TopLeft, r.TopRight, r.BottomRight, r.BottomLeft), BoxShadow);
    }


    private Thickness LayoutThickness()
    {
        var thickness = BorderThickness;
        return UseLayoutRounding ? LayoutHelper.RoundLayoutThickness(thickness, LayoutHelper.GetLayoutScale(this)) : thickness;
    }


    private Shapes ShapesFor(Size size)
    {
        var thickness = LayoutThickness();
        var shape = GetCornerShape(this);
        if (_shapes is { } shapes && shapes.Size == size && shapes.Thickness == thickness && shapes.Radius == CornerRadius && shapes.Shape == shape) return shapes;

        var box = new Rect(size);
        var radii = Squircle.Radii(box, CornerRadius);
        var inner = box.Deflate(thickness);
        var innerRadii = Squircle.Inset(radii, thickness);

        _shapes = new Shapes(size, thickness, CornerRadius, shape, box, radii, inner, innerRadii,
            Squircle.Geometry(box, radii, shape),
            thickness != default ? Squircle.Ring(box, radii, inner, innerRadii, shape) : null);
        return _shapes;
    }


    // paths and blurred images follow everything but each colour's alpha (which a transition animates), and the border's width unless a shadow is inset
    private ShadowPart[] ShadowsFor(Shapes shapes, out double[] alphas)
    {
        var shadows = BoxShadow;
        var keys = new BoxShadow[shadows.Count];
        alphas = new double[keys.Length];
        var anyInset = false;
        for (var i = 0; i < keys.Length; i++)
        {
            var s = shadows[i];
            keys[i] = s with { Color = Color.FromArgb(255, s.Color.R, s.Color.G, s.Color.B) };
            alphas[i] = s.Color.A / 255.0;
            anyInset |= s.IsInset;
        }

        var key = new ShadowKey(shapes.Size, shapes.Radius, shapes.Shape, anyInset ? shapes.Thickness : default, TopLevel.GetTopLevel(this)?.RenderScaling ?? 1, keys);
        if (_shadowKey is not { } last || !last.Matches(key))
        {
            _shadowKey = key;
            _shadows = new ShadowPart[keys.Length];
            for (var i = 0; i < keys.Length; i++) _shadows[i] = ShadowPart.Create(shapes, keys[i], key.Scale);
        }

        return _shadows;
    }


    // a square box clips to its bounds; a rounded one to the round rect that holds its squircle, which Skia clips with no mask
    private void UpdateClip(Size size)
    {
        if (ClipToBounds && IsRounded && size.Width > 0 && size.Height > 0)
        {
            var shapes = ShapesFor(size);
            var r = shapes.Radii;
            Clip = r.TopLeft == r.TopRight && r.TopLeft == r.BottomRight && r.TopLeft == r.BottomLeft
                ? new RectangleGeometry(shapes.Box) { RadiusX = Squircle.RoundHolding(r.TopLeft.Width, shapes.Shape), RadiusY = Squircle.RoundHolding(r.TopLeft.Height, shapes.Shape) }
                : shapes.Outer;
            _ownsClip = true;
        }
        else if (_ownsClip)
        {
            Clip = null;
            _ownsClip = false;
        }
    }

    #endregion // Private methods


    #region Event handlers

    private void Settle_Tick(object? sender, EventArgs e)
    {
        _settle?.Stop();
        _resizing = false;
        InvalidateVisual();
    }

    #endregion // Event handlers


    #region Nested types

    private sealed record ShadowKey(Size Size, CornerRadius Radius, CornerShape Shape, Thickness Thickness, double Scale, BoxShadow[] Shadows)
    {
        public bool Matches(ShadowKey other)
        {
            if (Size != other.Size || Radius != other.Radius || Shape != other.Shape || Thickness != other.Thickness || Scale != other.Scale) return false;
            if (Shadows.Length != other.Shadows.Length) return false;

            for (var i = 0; i < Shadows.Length; i++)
            {
                if (!Shadows[i].Equals(other.Shadows[i])) return false;
            }

            return true;
        }
    }


    private sealed record RingKey(Shapes Shapes, double Scale, SKColor Color);


    private sealed record Shapes(Size Size, Thickness Thickness, CornerRadius Radius, CornerShape Shape, Rect Box, CornerRadii Radii, Rect Inner, CornerRadii InnerRadii, StreamGeometry Outer, StreamGeometry? Ring)
    {
        public Band BorderRing => field ??= new Band(Band.Path(Box, Radii, Shape, Inner, InnerRadii), Band.Holding(Box, Radii, Shape), Band.Holding(Inner, InnerRadii, Shape));

        public Band Body => field ??= new Band(Band.Path(Box, Radii, Shape, default, default), Band.Holding(Box, Radii, Shape), null);

        public Func<Point, bool> OuterHit => field ??= Outer.FillContains;

        public Func<Point, bool>? RingHit => field ??= Ring is { } ring ? ring.FillContains : null;
    }


    /// <summary>
    /// An outline, or the band between two, drawn exactly at rest; moving or scaled as the round rects that hold its squircles, which Skia draws analytically.
    /// </summary>
    private sealed class Band(SKPath exact, SKRoundRect outer, SKRoundRect? hole)
    {
        // the analytic plan for a moving box, made once (null run = not analytic)
        private bool _planned;
        private SKRoundRect? _run;
        private SKRect _cut;


        public void Draw(SKCanvas canvas, SKPaint paint, bool moving)
        {
            if (!moving)
            {
                canvas.DrawPath(exact, paint);
                return;
            }

            if (hole is null)
            {
                if (outer.Type != SKRoundRectType.Complex)
                {
                    canvas.DrawRoundRect(outer, paint);
                    return;
                }

                if (!_planned)
                {
                    _planned = true;
                    _run = Plan(outer, out _cut);
                }

                if (_run is null)
                {
                    canvas.DrawPath(exact, paint);
                    return;
                }

                canvas.Save();
                canvas.ClipRect(_cut, antialias: true);
                canvas.DrawRoundRect(_run, paint);
                canvas.Restore();
                return;
            }

            // Skia keeps only simple and nine-patch round rects analytic
            if (outer.Type == SKRoundRectType.Complex || hole.Type == SKRoundRectType.Complex)
            {
                canvas.DrawPath(exact, paint);
                return;
            }

            canvas.Save();
            canvas.ClipRoundRect(hole, SKClipOperation.Difference, antialias: true);
            canvas.DrawRoundRect(outer, paint);
            canvas.Restore();
        }


        public static SKPath Path(Rect box, in CornerRadii radii, CornerShape shape, Rect hole, in CornerRadii holeRadii)
        {
            using var builder = new SKPathBuilder { FillType = SKPathFillType.EvenOdd };
            Squircle.AddTo(builder, box, radii, shape);
            if (hole.Width > 0 && hole.Height > 0) Squircle.AddTo(builder, hole, holeRadii, shape);
            return builder.Detach();
        }


        public static SKRoundRect Holding(Rect box, in CornerRadii radii, CornerShape shape)
        {
            SKPoint Corner(Size r) => new((float)Squircle.RoundHolding(r.Width, shape), (float)Squircle.RoundHolding(r.Height, shape));

            var rect = new SKRoundRect();
            rect.SetRectRadii(new SKRect((float)box.Left, (float)box.Top, (float)box.Right, (float)box.Bottom), [Corner(radii.TopLeft), Corner(radii.TopRight), Corner(radii.BottomRight), Corner(radii.BottomLeft)]);
            return rect;
        }


        // a box rounded on some sides only (a card's caption, 0,0,20,20) is a simple round rect run past its square sides, cut to the box
        private static SKRoundRect? Plan(SKRoundRect rect, out SKRect cut)
        {
            cut = rect.Rect;
            var r = rect.Radii;

            SKPoint? round = null;
            foreach (var c in r)
            {
                if (c.X <= 0 && c.Y <= 0) continue;
                if (round is { } first && first != c) return null;
                round = c;
            }

            if (round is not { } corner) return null;

            // corners in Skia's order: upper left, upper right, lower right, lower left
            bool Square(int i) => r[i].X <= 0 && r[i].Y <= 0;
            var (left, top, right, bottom) = (Square(0) && Square(3), Square(0) && Square(1), Square(1) && Square(2), Square(2) && Square(3));
            for (var i = 0; i < 4; i++)
            {
                var covered = i switch { 0 => left || top, 1 => top || right, 2 => right || bottom, _ => bottom || left };
                if (Square(i) && !covered) return null;
            }

            var box = rect.Rect;
            var run = new SKRect(left ? box.Left - corner.X : box.Left, top ? box.Top - corner.Y : box.Top, right ? box.Right + corner.X : box.Right, bottom ? box.Bottom + corner.Y : box.Bottom);
            return new SKRoundRect(run, corner.X, corner.Y);
        }
    }


    // one thing to paint: a band in a colour or a gradient, or an image at an opacity
    private readonly record struct SkiaPaint(Band? Band, SKImage? Image, SKRect Destination, SKColor Color, double Alpha, Rect Reach, SKShader? Shader = null);


    // drawn by Skia on the render thread, where the frame's transform is known; shadows are never hit, as Border's
    private sealed class SkiaDraw(SkiaPaint[] paints, double scale, Func<Point, bool>? hit, Rect? bounds = null, bool resizing = false) : ICustomDrawOperation
    {
        [ThreadStatic]
        private static SKPaint? t_paint;


        public Rect Bounds { get; } = bounds ?? Union(paints);


        public bool HitTest(Point p) => hit?.Invoke(p) ?? false;


        public bool Equals(ICustomDrawOperation? other) => ReferenceEquals(this, other);


        public void Dispose()
        {
        }


        public void Render(ImmediateDrawingContext context)
        {
            if (paints.Length == 0) return;
            if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } feature) return;

            using var lease = feature.Lease();
            var canvas = lease.SkCanvas;

            // Skia masks a path afresh at each new scale, turn and part-pixel place, so only a box at rest on whole pixels takes its exact outline (FewerPhotos F13)
            var m = canvas.TotalMatrix;
            static bool Whole(float v) => Math.Abs(v - MathF.Round(v)) < 1e-3;
            var moving = resizing || Math.Abs(m.ScaleX - scale) > 1e-3 || Math.Abs(m.ScaleY - scale) > 1e-3 || m.SkewX != 0 || m.SkewY != 0 || !Whole(m.TransX) || !Whole(m.TransY);
            var paint = t_paint ??= new SKPaint { IsAntialias = true };

            foreach (var p in paints)
            {
                var alpha = (byte)Math.Round(p.Color.Alpha * Math.Clamp(p.Alpha * lease.CurrentOpacity, 0, 1));
                if (alpha == 0) continue;

                if (p.Band is { } band)
                {
                    paint.Color = p.Color.WithAlpha(alpha);
                    paint.Shader = p.Shader;
                    band.Draw(canvas, paint, moving);
                    paint.Shader = null;
                }
                else if (p.Image is { } image)
                {
                    paint.Color = SKColors.White.WithAlpha(alpha);
                    canvas.DrawImage(image, p.Destination, new SKSamplingOptions(SKFilterMode.Linear), paint);
                }
            }
        }


        private static Rect Union(SkiaPaint[] paints)
        {
            var all = default(Rect);
            foreach (var p in paints) all = all.Union(p.Reach);
            return all;
        }
    }


    // a crisp shadow is a ring; a blurred one an image drawn once per size, since a blurred path is a new GPU mask at each new position or scale
    private sealed class ShadowPart
    {
        // a blur is rastered at a size where its sigma still spans this many pixels, at most this many times smaller
        private const double MIN_BLUR_PIXELS = 6;
        private const int MAX_DOWNSCALE = 8;

        private readonly SKColor _color;
        private readonly Band? _band;
        private readonly SKImage? _image;
        private readonly SKRect _destination;
        private readonly Rect _reach;


        private ShadowPart(bool isInset, SKColor color, Band? ring, SKImage? image, SKRect destination, Rect reach)
        {
            IsInset = isInset;
            _color = color;
            _band = ring;
            _image = image;
            _destination = destination;
            _reach = reach;
        }


        public bool IsInset { get; }


        public SkiaPaint Paint(double alpha) => new(_band, _image, _destination, _color, alpha, _reach);


        public static ShadowPart Create(Shapes shapes, BoxShadow shadow, double scale)
        {
            var color = new SKColor(shadow.Color.R, shadow.Color.G, shadow.Color.B);

            // outset: the box spread and moved, outside the box; inset: inside the padding edge, outside the box shrunk and moved
            var spread = shadow.IsInset ? -shadow.Spread : shadow.Spread;
            var from = shadow.IsInset ? shapes.Inner : shapes.Box;
            var box = from.Inflate(spread).Translate(new Vector(shadow.OffsetX, shadow.OffsetY));
            var radii = Squircle.Spread(shadow.IsInset ? shapes.InnerRadii : shapes.Radii, spread);
            var cast = box.Width > 0 && box.Height > 0;

            if (shadow.Blur <= 0)
            {
                using var outer = SkiaPath(shapes.Box, shapes.Radii, shapes.Shape);
                using var inner = SkiaPath(shapes.Inner, shapes.InnerRadii, shapes.Shape);
                using var shape = cast ? SkiaPath(box, radii, shapes.Shape) : new SKPath();
                var ring = shadow.IsInset
                    ? new Band(inner.Op(shape, SKPathOp.Difference) ?? new SKPath(), Band.Holding(shapes.Inner, shapes.InnerRadii, shapes.Shape), cast ? Band.Holding(box, radii, shapes.Shape) : null)
                    : new Band(shape.Op(outer, SKPathOp.Difference) ?? new SKPath(), cast ? Band.Holding(box, radii, shapes.Shape) : new SKRoundRect(), Band.Holding(shapes.Box, shapes.Radii, shapes.Shape));
                return new ShadowPart(shadow.IsInset, color, ring, null, default, shadow.IsInset ? shapes.Inner : box.Union(shapes.Box));
            }

            // Avalonia's blur to sigma, the one the tokens convert Chrome's for, so a squircle's shadow matches a round one's
            var sigma = 0.288675 * shadow.Blur + 0.5;
            var reach = 3 * sigma + Math.Abs(shadow.Spread) + Math.Max(Math.Abs(shadow.OffsetX), Math.Abs(shadow.OffsetY));
            var extent = shadow.IsInset ? 0 : Math.Ceiling(reach);
            var area = shapes.Box.Inflate(extent);
            var pixels = new SKImageInfo(Math.Max(1, (int)Math.Ceiling(area.Width * scale)), Math.Max(1, (int)Math.Ceiling(area.Height * scale)), SKColorType.Bgra8888, SKAlphaType.Premul);

            using var surface = SKSurface.Create(pixels);
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            canvas.Scale((float)scale);
            canvas.Translate((float)extent, (float)extent);

            using var blur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, (float)sigma);
            using var paint = new SKPaint { IsAntialias = true, Color = color, MaskFilter = blur };

            if (shadow.IsInset)
            {
                using var inner = SkiaPath(shapes.Inner, shapes.InnerRadii, shapes.Shape);
                canvas.ClipPath(inner, antialias: true);

                // a frame around the hole, far enough out that its blur reaches in whole
                using var builder = new SKPathBuilder { FillType = SKPathFillType.EvenOdd };
                var far = shapes.Inner.Inflate(Math.Ceiling(reach) + 1);
                builder.AddRect(new SKRect((float)far.Left, (float)far.Top, (float)far.Right, (float)far.Bottom));
                if (cast) Squircle.AddTo(builder, box, radii, shapes.Shape);
                using var frame = builder.Detach();
                canvas.DrawPath(frame, paint);
            }
            else
            {
                if (cast)
                {
                    using var shape = SkiaPath(box, radii, shapes.Shape);
                    DrawBlurred(canvas, shape, paint, pixels, scale, sigma, extent);
                }

                using var outer = SkiaPath(shapes.Box, shapes.Radii, shapes.Shape);
                using var cut = new SKPaint { IsAntialias = true, BlendMode = SKBlendMode.Clear };
                canvas.DrawPath(outer, cut);
            }

            var destination = new SKRect((float)-extent, (float)-extent, (float)(-extent + pixels.Width / scale), (float)(-extent + pixels.Height / scale));
            return new ShadowPart(shadow.IsInset, color, null, surface.Snapshot(), destination, new Rect(destination.Left, destination.Top, destination.Width, destination.Height));
        }


        // a wide blur keeps no detail a smaller raster loses: blurred there, then scaled up (an elevation 2 card's took about 80 ms at full size)
        private static void DrawBlurred(SKCanvas canvas, SKPath shape, SKPaint paint, SKImageInfo pixels, double scale, double sigma, double extent)
        {
            var step = Math.Clamp((int)(sigma * scale / MIN_BLUR_PIXELS), 1, MAX_DOWNSCALE);
            if (step == 1)
            {
                canvas.DrawPath(shape, paint);
                return;
            }

            var small = new SKImageInfo(Math.Max(1, pixels.Width / step), Math.Max(1, pixels.Height / step), pixels.ColorType, pixels.AlphaType);
            using var surface = SKSurface.Create(small);
            surface.Canvas.Clear(SKColors.Transparent);
            surface.Canvas.Scale((float)(scale / step));
            surface.Canvas.Translate((float)extent, (float)extent);
            surface.Canvas.DrawPath(shape, paint);

            using var image = surface.Snapshot();
            var area = new SKRect((float)-extent, (float)-extent, (float)(-extent + small.Width * step / scale), (float)(-extent + small.Height * step / scale));
            canvas.DrawImage(image, area, new SKSamplingOptions(SKFilterMode.Linear));
        }


        private static SKPath SkiaPath(Rect box, in CornerRadii radii, CornerShape shape)
        {
            using var builder = new SKPathBuilder();
            Squircle.AddTo(builder, box, radii, shape);
            return builder.Detach();
        }
    }

    #endregion // Nested types
}
