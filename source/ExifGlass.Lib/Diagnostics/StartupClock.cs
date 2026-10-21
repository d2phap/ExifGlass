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
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace ExifGlass.Diagnostics;

/// <summary>
/// Marks from process creation through Main to the first frame the render thread draws (plan 8: startup, S8).
/// </summary>
public static class StartupClock
{
    #region Fields and constants

    public const string FIRST_FRAME = "first frame drawn (render thread)";

    private static readonly Lock _lock = new();
    private static readonly List<(string Label, double Ms)> _marks = [];
    private static long _main = Stopwatch.GetTimestamp();
    private static double _beforeMain = double.NaN;

    #endregion // Fields and constants


    #region Properties

    /// <summary>
    /// Gets the milliseconds since Main started.
    /// </summary>
    public static double Now => Stopwatch.GetElapsedTime(_main).TotalMilliseconds;

    /// <summary>
    /// Gets the milliseconds from process creation to Main, by the wall clock (about 1 ms resolution); NaN before Start.
    /// </summary>
    public static double BeforeMain => _beforeMain;

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Called first thing in Main.
    /// </summary>
    public static void Start()
    {
        _main = Stopwatch.GetTimestamp();

        using var self = Process.GetCurrentProcess();
        _beforeMain = (DateTime.Now - self.StartTime).TotalMilliseconds;
    }


    public static void Mark(string label)
    {
        var ms = Now;
        lock (_lock)
        {
            if (!_marks.Exists(m => m.Label == label)) _marks.Add((label, ms));
        }
    }


    public static double? Of(string label)
    {
        lock (_lock)
        {
            foreach (var (l, ms) in _marks)
            {
                if (l == label) return ms;
            }
        }

        return null;
    }


    public static string Format()
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"  process created -> Main {_beforeMain:0} ms (wall clock)\n");
        lock (_lock)
        {
            foreach (var (label, ms) in _marks) sb.Append(CultureInfo.InvariantCulture, $"  {ms,8:0.0} ms after Main  {label}\n");
        }

        return sb.ToString();
    }

    #endregion // Public methods
}
