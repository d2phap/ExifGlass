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
using Avalonia.Media;
using SkiaSharp;

namespace ExifGlass.UI.Controls;

/// <summary>
/// How a box's rounded corners curve (CSS corner-shape).
/// </summary>
public enum CornerShape
{
    /// <summary>
    /// Circular arcs: switch tracks, knobs and dots stay truly round (plan 5.4).
    /// </summary>
    Round,

    /// <summary>
    /// CSS superellipse(1.5): Lame exponent 2^1.5, one shape everywhere (O1).
    /// </summary>
    Squircle,
}


/// <summary>
/// Each corner's horizontal and vertical radius.
/// </summary>
public readonly record struct CornerRadii(Size TopLeft, Size TopRight, Size BottomRight, Size BottomLeft);


/// <summary>
/// A rounded box's outline as CSS draws it: radii clamped to the box, each corner two cubics.
/// </summary>
public static class Squircle
{
    #region Fields and constants

    /// <summary>
    /// CSS superellipse(K) is |x|^n + |y|^n = 1 with n = 2^K.
    /// </summary>
    public static readonly double EXPONENT = Math.Pow(2, 1.5);

    // a corner runs (0,1) > (a,1) > (A-c,A+c) > apex (A,A), mirrored to (1,0); a and c fitted to within 0.00007 r
    private static readonly Curve ROUND = new(Math.Sqrt(0.5), 0.2652165, 0.1875364);
    private static readonly Curve SQUIRCLE = new(Math.Pow(2, -1 / EXPONENT), 0.2335650, 0.2188740);

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// The box's corner radii, scaled down together until adjacent ones fit each side, as CSS does.
    /// </summary>
    public static CornerRadii Radii(Rect box, CornerRadius radius)
    {
        double tl = Math.Max(0, radius.TopLeft), tr = Math.Max(0, radius.TopRight), br = Math.Max(0, radius.BottomRight), bl = Math.Max(0, radius.BottomLeft);
        var f = Fit(Fit(Fit(Fit(1, box.Width, tl + tr), box.Width, bl + br), box.Height, tl + bl), box.Height, tr + br);

        return new CornerRadii(new Size(tl * f, tl * f), new Size(tr * f, tr * f), new Size(br * f, br * f), new Size(bl * f, bl * f));
    }


    /// <summary>
    /// The radii of the box inside a border (the padding edge).
    /// </summary>
    public static CornerRadii Inset(in CornerRadii radii, Thickness thickness) => new(
        Shrink(radii.TopLeft, thickness.Left, thickness.Top),
        Shrink(radii.TopRight, thickness.Right, thickness.Top),
        Shrink(radii.BottomRight, thickness.Right, thickness.Bottom),
        Shrink(radii.BottomLeft, thickness.Left, thickness.Bottom));


    /// <summary>
    /// The radii of a shadow spread; a radius smaller than the spread grows less, as CSS adjusts it.
    /// </summary>
    public static CornerRadii Spread(in CornerRadii radii, double spread) => new(
        new Size(Grow(radii.TopLeft.Width, spread), Grow(radii.TopLeft.Height, spread)),
        new Size(Grow(radii.TopRight.Width, spread), Grow(radii.TopRight.Height, spread)),
        new Size(Grow(radii.BottomRight.Width, spread), Grow(radii.BottomRight.Height, spread)),
        new Size(Grow(radii.BottomLeft.Width, spread), Grow(radii.BottomLeft.Height, spread)));


    public static StreamGeometry Geometry(Rect box, in CornerRadii radii, CornerShape shape)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            Trace(new GeometrySink(context), box, radii, shape);
        }

        return geometry;
    }


    /// <summary>
    /// The band between the outer and inner outlines (a border), filled even-odd.
    /// </summary>
    public static StreamGeometry Ring(Rect outer, in CornerRadii outerRadii, Rect inner, in CornerRadii innerRadii, CornerShape shape)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.SetFillRule(FillRule.EvenOdd);
            var sink = new GeometrySink(context);
            Trace(sink, outer, outerRadii, shape);
            if (inner.Width > 0 && inner.Height > 0) Trace(sink, inner, innerRadii, shape);
        }

        return geometry;
    }


    public static void AddTo(SKPathBuilder path, Rect box, in CornerRadii radii, CornerShape shape) => Trace(new SkiaSink(path), box, radii, shape);


    /// <summary>
    /// The radius of the round corner that holds a squircle corner: they touch at its apex, so a round-rect clip covers it.
    /// </summary>
    public static double RoundHolding(double radius, CornerShape shape) => shape == CornerShape.Squircle ? radius * (1 - SQUIRCLE.Apex) / (1 - Math.Sqrt(0.5)) : radius;

    #endregion // Public methods


    #region Private methods

    private static double Fit(double factor, double side, double sum) => sum > side ? Math.Min(factor, side / sum) : factor;


    private static Size Shrink(Size radius, double x, double y) => new(Math.Max(0, radius.Width - x), Math.Max(0, radius.Height - y));


    private static double Grow(double radius, double spread)
    {
        if (spread < 0) return Math.Max(0, radius + spread);
        if (spread == 0 || radius >= spread) return radius + spread;

        return radius + spread * (1 + Math.Pow(radius / spread - 1, 3));
    }


    // clockwise from the top edge; each corner runs from the edge before it to the edge after it
    private static void Trace<TSink>(TSink sink, Rect box, in CornerRadii r, CornerShape shape) where TSink : IPathSink
    {
        var curve = shape == CornerShape.Squircle ? SQUIRCLE : ROUND;

        sink.Begin(new Point(box.Left + r.TopLeft.Width, box.Top));
        Corner(sink, curve, new Point(box.Right - r.TopRight.Width, box.Top + r.TopRight.Height), r.TopRight, 1, -1, fromHorizontal: true);
        Corner(sink, curve, new Point(box.Right - r.BottomRight.Width, box.Bottom - r.BottomRight.Height), r.BottomRight, 1, 1, fromHorizontal: false);
        Corner(sink, curve, new Point(box.Left + r.BottomLeft.Width, box.Bottom - r.BottomLeft.Height), r.BottomLeft, -1, 1, fromHorizontal: true);
        Corner(sink, curve, new Point(box.Left + r.TopLeft.Width, box.Top + r.TopLeft.Height), r.TopLeft, -1, -1, fromHorizontal: false);
        sink.Close();
    }


    // a corner from its centre: (sx, sy) point to the box's corner; one that starts on a vertical edge swaps the axes
    private static void Corner<TSink>(TSink sink, Curve curve, Point centre, Size radius, int sx, int sy, bool fromHorizontal) where TSink : IPathSink
    {
        Point At(double x, double y)
        {
            var (u, v) = fromHorizontal ? (x, y) : (y, x);
            return new Point(centre.X + sx * radius.Width * u, centre.Y + sy * radius.Height * v);
        }

        if (radius.Width <= 0 || radius.Height <= 0)
        {
            sink.Line(centre);
            return;
        }

        var (apex, a, c) = curve;
        sink.Line(At(0, 1));
        sink.Cubic(At(a, 1), At(apex - c, apex + c), At(apex, apex));
        sink.Cubic(At(apex + c, apex - c), At(1, a), At(1, 0));
    }

    #endregion // Private methods


    #region Nested types

    private readonly record struct Curve(double Apex, double A, double C);


    private interface IPathSink
    {
        void Begin(Point point);

        void Line(Point point);

        void Cubic(Point control1, Point control2, Point end);

        void Close();
    }


    private readonly struct GeometrySink(StreamGeometryContext context) : IPathSink
    {
        public void Begin(Point point) => context.BeginFigure(point, isFilled: true);

        public void Line(Point point) => context.LineTo(point);

        public void Cubic(Point control1, Point control2, Point end) => context.CubicBezierTo(control1, control2, end);

        public void Close() => context.EndFigure(isClosed: true);
    }


    private readonly struct SkiaSink(SKPathBuilder path) : IPathSink
    {
        public void Begin(Point point) => path.MoveTo((float)point.X, (float)point.Y);

        public void Line(Point point) => path.LineTo((float)point.X, (float)point.Y);

        public void Cubic(Point control1, Point control2, Point end)
            => path.CubicTo((float)control1.X, (float)control1.Y, (float)control2.X, (float)control2.Y, (float)end.X, (float)end.Y);

        public void Close() => path.Close();
    }

    #endregion // Nested types
}


/// <summary>
/// A box's outline for a control that draws it, made again only when its size, radius or shape changes.
/// </summary>
public sealed class SquircleShape
{
    #region Fields and constants

    private (Size Size, CornerRadius Radius, CornerShape Shape) _key;
    private StreamGeometry? _geometry;

    #endregion // Fields and constants


    #region Public methods

    public StreamGeometry For(Size size, CornerRadius radius, CornerShape shape)
    {
        if (_geometry is null || _key != (size, radius, shape))
        {
            var box = new Rect(size);
            _key = (size, radius, shape);
            _geometry = Squircle.Geometry(box, Squircle.Radii(box, radius), shape);
        }

        return _geometry;
    }


    /// <summary>
    /// The round rect that holds a box's squircle (within 0.012 r), for a fill that changes every frame: Skia clips it with no mask.
    /// </summary>
    public static RoundedRect Holding(Rect box, CornerRadius radius, CornerShape shape)
    {
        var r = Squircle.Radii(box, radius);
        double H(Size corner) => Squircle.RoundHolding(corner.Width, shape);
        return new RoundedRect(box, H(r.TopLeft), H(r.TopRight), H(r.BottomRight), H(r.BottomLeft));
    }

    #endregion // Public methods
}
