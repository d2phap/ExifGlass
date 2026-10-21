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
using Avalonia.Threading;
using System.Diagnostics;
using System.Globalization;

namespace ExifGlass.Diagnostics;

/// <summary>
/// The plan's UI watchdog (4.2, S8): a background timer posts to the dispatcher every 16 ms and records any wait over 50 ms with the current operation.
/// </summary>
public sealed class UiWatchdog : IDisposable
{
    #region Fields and constants

    public const double HITCH_MS = 50;

    private readonly Timer _timer;
    private readonly Lock _lock = new();
    private readonly List<string> _hitches = [];
    private double _max;
    private int _pending;

    #endregion // Fields and constants


    #region Constructors

    public UiWatchdog() => _timer = new Timer(_ => Probe(), null, 16, 16);

    #endregion // Constructors


    #region Properties

    /// <summary>
    /// Gets or sets what the UI thread is doing, set by the code that starts a long job.
    /// </summary>
    public static string Operation { get; set; } = "idle";

    /// <summary>
    /// Gets the longest wait seen since the last Take.
    /// </summary>
    public double Max
    {
        get
        {
            lock (_lock) return _max;
        }
    }

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Returns the hitches seen so far and starts counting again.
    /// </summary>
    public string[] Take()
    {
        lock (_lock)
        {
            var list = _hitches.ToArray();
            _hitches.Clear();
            _max = 0;
            return list;
        }
    }


    public void Dispose() => _timer.Dispose();

    #endregion // Public methods


    #region Private methods

    // one probe in flight: a blocked UI thread shows as one long wait, not a queue of them
    private void Probe()
    {
        if (Interlocked.Exchange(ref _pending, 1) == 1) return;

        var posted = Stopwatch.GetTimestamp();
        Dispatcher.UIThread.Post(() =>
        {
            var waited = Stopwatch.GetElapsedTime(posted).TotalMilliseconds;
            Volatile.Write(ref _pending, 0);

            lock (_lock)
            {
                if (waited > _max) _max = waited;
                if (waited > HITCH_MS) _hitches.Add(string.Create(CultureInfo.InvariantCulture, $"UI thread blocked {waited:0} ms during '{Operation}' at {StartupClock.Now:0} ms"));
            }

            if (waited > HITCH_MS) Debug.WriteLine($"[watchdog] UI thread blocked {waited:0} ms during '{Operation}'");
        }, DispatcherPriority.Send);
    }

    #endregion // Private methods
}
