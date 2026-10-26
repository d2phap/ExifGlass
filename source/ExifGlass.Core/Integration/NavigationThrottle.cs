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
namespace ExifGlass.Core.Integration;

/// <summary>
/// S6's load policy for a viewer's photo changes: a lone change reads at once, a burst reads after a 100 ms pause and at least every 500 ms. One thread only: the owner's, which the timer posts back to.
/// </summary>
public sealed class NavigationThrottle : IDisposable
{
    #region Fields and constants

    public static readonly TimeSpan DEBOUNCE = TimeSpan.FromMilliseconds(100);
    public static readonly TimeSpan MAX_WAIT = TimeSpan.FromMilliseconds(500);

    private readonly Action<string> _start;
    private readonly Action<Action> _post;
    private readonly TimeProvider _time;
    private readonly ITimer _timer;

    private string? _pending;
    private long _burstStart;
    private long _lastChange;
    private bool _changed;
    private int _armings;
    private bool _stopped;

    #endregion // Fields and constants


    #region Constructors

    /// <summary>
    /// <paramref name="start"/> reads a file; <paramref name="post"/> runs an action on the owner's thread.
    /// </summary>
    public NavigationThrottle(Action<string> start, Action<Action> post, TimeProvider? time = null)
    {
        _start = start;
        _post = post;
        _time = time ?? TimeProvider.System;
        _timer = _time.CreateTimer(static s => ((NavigationThrottle)s!).Timer_Elapsed(), this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    #endregion // Constructors


    #region Properties

    /// <summary>
    /// The newest change still waiting for its read; null when none waits.
    /// </summary>
    public string? Pending => _pending;

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// A photo change: read at once when nothing changed in the last 100 ms, else wait for the burst to pause.
    /// </summary>
    public void Navigate(string path)
    {
        if (_stopped) return;

        var now = _time.GetTimestamp();

        // keyed on the previous change, not the previous read: EXECUTE's read must not swallow the first step (S6)
        var quiet = !_changed || _time.GetElapsedTime(_lastChange, now) >= DEBOUNCE;
        _changed = true;
        _lastChange = now;

        if (quiet && _pending is null)
        {
            _start(path);
            return;
        }

        if (_pending is null) _burstStart = now;
        _pending = path;

        var due = MAX_WAIT - _time.GetElapsedTime(_burstStart, now);
        if (due > DEBOUNCE) due = DEBOUNCE;
        if (due < TimeSpan.Zero) due = TimeSpan.Zero;
        Arm(due);
    }


    /// <summary>
    /// A file to read now (the viewer's answer, the command line), in place of any change still waiting.
    /// </summary>
    public void Request(string path)
    {
        if (_stopped) return;

        _pending = null;
        Arm(Timeout.InfiniteTimeSpan);
        _start(path);
    }


    /// <summary>
    /// Drops what waits and takes nothing more.
    /// </summary>
    public void Stop()
    {
        _stopped = true;
        _pending = null;
        Arm(Timeout.InfiniteTimeSpan);
    }


    public void Dispose()
    {
        Stop();
        _timer.Dispose();
    }

    #endregion // Public methods


    #region Private methods

    private void Arm(TimeSpan due)
    {
        Interlocked.Increment(ref _armings);
        _timer.Change(due, Timeout.InfiniteTimeSpan);
    }


    private void Fire(int arming)
    {
        if (_stopped || arming != _armings || _pending is not { } path) return;

        _pending = null;
        _start(path);
    }

    #endregion // Private methods


    #region Event handlers

    // the timer runs on TickCount and may fire a little before a Stopwatch deadline: compare armings, not times (S6)
    private void Timer_Elapsed()
    {
        var arming = Volatile.Read(ref _armings);
        _post(() => Fire(arming));
    }

    #endregion // Event handlers
}
