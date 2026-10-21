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
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using ExifGlass.UI.Tokens;

namespace ExifGlass.UI.Motion;

/// <summary>
/// A timeline of eased tracks played on the frame clock; the start state applies at once.
/// </summary>
public sealed class Choreo
{
    #region Fields and constants

    private readonly List<Track> _tracks = [];
    private TaskCompletionSource? _done;
    private TopLevel? _topLevel;
    private TimeSpan _firstFrame;
    private bool _hasFirstFrame;

    #endregion // Fields and constants


    #region Properties

    /// <summary>
    /// Gets the end of the last track.
    /// </summary>
    public TimeSpan Duration { get; private set; }

    public bool IsPlaying => _done is not null;

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Adds a track; apply gets the eased progress (overshoot included), and 0 before begin.
    /// </summary>
    public Choreo Add(TimeSpan begin, TimeSpan duration, Easing easing, Action<double> apply)
    {
        _tracks.Add(new Track(begin, duration, easing, apply));
        if (begin + duration > Duration) Duration = begin + duration;

        return this;
    }


    /// <summary>
    /// Adds a track that starts at once.
    /// </summary>
    public Choreo Add(TimeSpan duration, Easing easing, Action<double> apply) => Add(TimeSpan.Zero, duration, easing, apply);


    /// <summary>
    /// Applies every track's state at a time; tests sample motion with it.
    /// </summary>
    public void Seek(TimeSpan time)
    {
        foreach (var track in _tracks)
        {
            var progress = track.Duration <= TimeSpan.Zero
                ? (time >= track.Begin ? 1 : 0)
                : Math.Clamp((time - track.Begin) / track.Duration, 0, 1);

            track.Apply(progress <= 0 ? track.Easing.Ease(0) : progress >= 1 ? 1 : track.Easing.Ease(progress));
        }
    }


    /// <summary>
    /// Plays the timeline on the host's frame clock; without a window, or with motion frozen, it jumps to the end.
    /// </summary>
    public Task PlayAsync(Visual host)
    {
        Stop(jumpToEnd: false);
        Seek(TimeSpan.Zero);

        _topLevel = TopLevel.GetTopLevel(host);
        if (_topLevel is null || MotionTokens.IsFrozen || Duration <= TimeSpan.Zero)
        {
            Seek(Duration);
            return Task.CompletedTask;
        }

        _done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _hasFirstFrame = false;
        _topLevel.RequestAnimationFrame(OnFrame);

        return _done.Task;
    }


    /// <summary>
    /// Stops playing; jumpToEnd applies the final state first.
    /// </summary>
    public void Stop(bool jumpToEnd)
    {
        if (_done is not { } done) return;

        _done = null;
        if (jumpToEnd) Seek(Duration);
        done.TrySetResult();
    }

    #endregion // Public methods


    #region Private methods

    private void OnFrame(TimeSpan time)
    {
        if (_done is null || _topLevel is null) return;

        if (!_hasFirstFrame)
        {
            _hasFirstFrame = true;
            _firstFrame = time;
        }

        var elapsed = time - _firstFrame;
        if (elapsed >= Duration)
        {
            Stop(jumpToEnd: true);
            return;
        }

        Seek(elapsed);
        _topLevel.RequestAnimationFrame(OnFrame);
    }

    #endregion // Private methods


    #region Nested types

    private sealed record Track(TimeSpan Begin, TimeSpan Duration, Easing Easing, Action<double> Apply);

    #endregion // Nested types
}
