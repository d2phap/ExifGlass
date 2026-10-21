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
using Avalonia.Rendering.Composition;
using ExifGlass.UI.Tokens;

namespace ExifGlass.UI.Motion;

/// <summary>
/// Opacity, translation and scale animated on the render thread (plan 4.2), so motion keeps its frame rate while the UI thread works.
/// </summary>
public static class CompositionMotion
{
    #region Fields and constants

    private static readonly TimeSpan Instant = TimeSpan.FromMilliseconds(1);

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Fades a visual between two opacities; a missing composition visual or frozen motion jumps to the end.
    /// </summary>
    public static void Fade(Visual target, double from, double to, TimeSpan duration, Easing easing, TimeSpan delay = default)
    {
        if (ElementComposition.GetElementVisual(target) is not { } visual) return;

        var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        if (!MotionTokens.IsFrozen && duration > TimeSpan.Zero) animation.InsertKeyFrame(0f, (float)from);
        animation.InsertKeyFrame(1f, (float)to, easing);
        animation.Duration = Live(duration);
        animation.DelayTime = MotionTokens.IsFrozen ? TimeSpan.Zero : delay;
        visual.StartAnimation("Opacity", animation);
    }


    /// <summary>
    /// Moves a visual from an offset to another, in DIPs.
    /// </summary>
    public static void Move(Visual target, Vector from, Vector to, TimeSpan duration, Easing easing, TimeSpan delay = default)
    {
        if (ElementComposition.GetElementVisual(target) is not { } visual) return;

        var animation = visual.Compositor.CreateVector3DKeyFrameAnimation();
        if (!MotionTokens.IsFrozen && duration > TimeSpan.Zero) animation.InsertKeyFrame(0f, new Vector3D(from.X, from.Y, 0));
        animation.InsertKeyFrame(1f, new Vector3D(to.X, to.Y, 0), easing);
        animation.Duration = Live(duration);
        animation.DelayTime = MotionTokens.IsFrozen ? TimeSpan.Zero : delay;
        visual.StartAnimation("Translation", animation);
    }


    /// <summary>
    /// Scales a visual about a point of its own box (0.5, 0.5 is the centre; 1, 0 the top-right).
    /// </summary>
    public static void Scale(Visual target, double from, double to, TimeSpan duration, Easing easing, RelativePoint? origin = null)
    {
        if (ElementComposition.GetElementVisual(target) is not { } visual) return;

        var centre = (origin ?? RelativePoint.Center).ToPixels(new Rect(target.Bounds.Size));
        visual.CenterPoint = new Vector3D(centre.X, centre.Y, 0);

        var animation = visual.Compositor.CreateVector3DKeyFrameAnimation();
        if (!MotionTokens.IsFrozen && duration > TimeSpan.Zero) animation.InsertKeyFrame(0f, new Vector3D(from, from, 1));
        animation.InsertKeyFrame(1f, new Vector3D(to, to, 1), easing);
        animation.Duration = Live(duration);
        visual.StartAnimation("Scale", animation);
    }

    #endregion // Public methods


    #region Private methods

    private static TimeSpan Live(TimeSpan duration) => MotionTokens.IsFrozen || duration <= TimeSpan.Zero ? Instant : duration;

    #endregion // Private methods
}
