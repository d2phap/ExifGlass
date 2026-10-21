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
using Avalonia.Animation.Easings;

namespace ExifGlass.UI.Motion;

/// <summary>
/// A CSS cubic-bezier(x1, y1, x2, y2) solved as WebKit's UnitBezier does, overshoot included.
/// </summary>
public sealed class CubicBezierEasing : Easing
{
    #region Fields and constants

    private const double EPSILON = 1e-7;

    private readonly double _ax, _bx, _cx, _ay, _by, _cy;

    #endregion // Fields and constants


    #region Properties

    public (double X1, double Y1, double X2, double Y2) Points { get; }

    #endregion // Properties


    #region Constructors

    public CubicBezierEasing(double x1, double y1, double x2, double y2)
    {
        if (x1 is < 0 or > 1 || x2 is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(x1), "CSS requires x control points in 0-1");

        Points = (x1, y1, x2, y2);
        _cx = 3 * x1;
        _bx = 3 * (x2 - x1) - _cx;
        _ax = 1 - _cx - _bx;
        _cy = 3 * y1;
        _by = 3 * (y2 - y1) - _cy;
        _ay = 1 - _cy - _by;
    }

    #endregion // Constructors


    #region Public methods

    public override double Ease(double progress)
    {
        if (progress <= 0) return 0;
        if (progress >= 1) return 1;

        return SampleY(SolveT(progress));
    }


    public override string ToString() => FormattableString.Invariant($"cubic-bezier({Points.X1}, {Points.Y1}, {Points.X2}, {Points.Y2})");

    #endregion // Public methods


    #region Private methods

    private double SampleX(double t) => ((_ax * t + _bx) * t + _cx) * t;

    private double SampleY(double t) => ((_ay * t + _by) * t + _cy) * t;

    private double SampleDerivativeX(double t) => (3 * _ax * t + 2 * _bx) * t + _cx;


    // Newton's method first, then bisection where the slope is too flat to trust
    private double SolveT(double x)
    {
        var t = x;
        for (var i = 0; i < 8; i++)
        {
            var error = SampleX(t) - x;
            if (Math.Abs(error) < EPSILON) return t;

            var slope = SampleDerivativeX(t);
            if (Math.Abs(slope) < 1e-6) break;

            t -= error / slope;
        }

        double lo = 0, hi = 1;
        t = x;
        while (lo < hi)
        {
            var value = SampleX(t);
            if (Math.Abs(value - x) < EPSILON) return t;

            if (x > value) lo = t;
            else hi = t;

            t = (hi - lo) / 2 + lo;
            if (hi - lo < EPSILON) break;
        }

        return t;
    }

    #endregion // Private methods
}
