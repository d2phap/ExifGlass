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
using ExifGlass.Core.Integration;
using Xunit;

namespace ExifGlass.Core.Tests.Integration;

/// <summary>
/// S6's load policy on a hand-moved clock: leading edge, 100 ms pause, 500 ms max wait, requests that read at once.
/// </summary>
public class NavigationThrottleTests
{
    #region Tests

    [Fact]
    public void ReadsALoneChangeAtOnce()
    {
        var (throttle, time, started) = New();

        throttle.Navigate("a");
        time.Advance(150);
        throttle.Navigate("b");

        Assert.Equal(["a", "b"], started.Select(s => s.Path));
        Assert.Equal([0, 150], started.Select(s => s.Ms));
    }


    [Fact]
    public void ReadsTheLastChangeOfABurstAfterAPause()
    {
        var (throttle, time, started) = New();

        throttle.Navigate("a");
        time.Advance(20);
        throttle.Navigate("b");
        time.Advance(20);
        throttle.Navigate("c");
        time.Advance(99);
        Assert.Equal("c", throttle.Pending);
        Assert.Equal(["a"], started.Select(s => s.Path));

        time.Advance(1);
        Assert.Equal(["a", "c"], started.Select(s => s.Path));
        Assert.Null(throttle.Pending);
    }


    [Fact]
    public void ShowsAPhotoEvery500MsWhileAKeyIsHeldAndSettlesOnTheLast()
    {
        var (throttle, time, started) = New();

        // S6 "rapid": 100 changes 20 ms apart
        for (var i = 0; i < 100; i++)
        {
            throttle.Navigate($"p{i:000}");
            time.Advance(20);
        }

        time.Advance(200);

        Assert.Equal("p000", started[0].Path);
        Assert.Equal("p099", started[^1].Path);
        Assert.InRange(started.Count, 5, 7);
        for (var i = 1; i < started.Count - 1; i++) Assert.InRange(started[i].Ms - started[i - 1].Ms, 1, 520);
    }


    [Fact]
    public void ReadsARequestAtOnceInPlaceOfAWaitingChange()
    {
        var (throttle, time, started) = New();

        throttle.Navigate("a");
        time.Advance(20);
        throttle.Navigate("b");
        throttle.Request("reply");
        time.Advance(300);

        Assert.Equal(["a", "reply"], started.Select(s => s.Path));
    }


    [Fact]
    public void KeysTheLeadingEdgeOnThePreviousChangeNotThePreviousRead()
    {
        var (throttle, time, started) = New();

        // the first step right after EXECUTE's read still reads at once (S6)
        throttle.Request("execute");
        time.Advance(10);
        throttle.Navigate("step");

        Assert.Equal(["execute", "step"], started.Select(s => s.Path));
    }


    [Fact]
    public void TakesNothingOnceStopped()
    {
        var (throttle, time, started) = New();

        throttle.Navigate("a");
        time.Advance(20);
        throttle.Navigate("b");
        throttle.Stop();
        time.Advance(600);
        throttle.Navigate("c");
        throttle.Request("d");

        Assert.Equal(["a"], started.Select(s => s.Path));
        Assert.Null(throttle.Pending);
    }

    #endregion // Tests


    #region Private methods

    private static (NavigationThrottle Throttle, ManualTime Time, List<(string Path, long Ms)> Started) New()
    {
        var time = new ManualTime();
        var started = new List<(string, long)>();
        var throttle = new NavigationThrottle(p => started.Add((p, time.NowMs)), a => a(), time);
        return (throttle, time, started);
    }

    #endregion // Private methods
}


/// <summary>
/// A clock the test moves by hand; timers fire in order as it passes their due time.
/// </summary>
internal sealed class ManualTime : TimeProvider
{
    #region Fields and constants

    private readonly List<ManualTimer> _timers = [];
    private long _now;

    #endregion // Fields and constants


    #region Properties

    public override long TimestampFrequency => 1000;

    public long NowMs => _now;

    #endregion // Properties


    #region Public methods

    public override long GetTimestamp() => _now;


    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        _timers.Add(timer);
        return timer;
    }


    public void Advance(long ms)
    {
        var target = _now + ms;
        while (_timers.Where(t => t.Due is { } d && d <= target).OrderBy(t => t.Due).FirstOrDefault() is { } next)
        {
            _now = next.Due!.Value;
            next.Due = null;
            next.Fire();
        }

        _now = target;
    }

    #endregion // Public methods


    #region Nested types

    private sealed class ManualTimer(ManualTime time, TimerCallback callback, object? state) : ITimer
    {
        public long? Due { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            Due = dueTime == Timeout.InfiniteTimeSpan ? null : time._now + (long)dueTime.TotalMilliseconds;
            return true;
        }

        public void Fire() => callback(state);

        public void Dispose() => Due = null;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }

    #endregion // Nested types
}
