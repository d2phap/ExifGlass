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

    // the visual exists after the first frame that renders the control; a few more cover a layout pass in between
    private const int FRAME_TRIES = 3;

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Runs motion on the target's composition visual; a control under a parent that was hidden has none until it first renders, so the motion waits for that frame.
    /// </summary>
    public static void WhenReady(Visual target, Action<CompositionVisual> start) => WhenReady(target, start, FRAME_TRIES);


    /// <summary>
    /// Fades a visual between two opacities; frozen motion jumps to the end.
    /// </summary>
    public static void Fade(Visual target, double from, double to, TimeSpan duration, Easing easing, TimeSpan delay = default) =>
        WhenReady(target, visual => Fade(visual, from, to, duration, easing, delay));


    /// <summary>
    /// Moves a visual from an offset to another, in DIPs.
    /// </summary>
    public static void Move(Visual target, Vector from, Vector to, TimeSpan duration, Easing easing, TimeSpan delay = default) =>
        WhenReady(target, visual => Move(visual, from, to, duration, easing, delay));


    /// <summary>
    /// Scales a visual about a point of its own box (0.5, 0.5 is the centre; 1, 0 the top-right).
    /// </summary>
    public static void Scale(Visual target, double from, double to, TimeSpan duration, Easing easing, RelativePoint? origin = null) =>
        WhenReady(target, visual => Scale(visual, target.Bounds.Size, from, to, duration, easing, origin));

    #endregion // Public methods


    #region Private methods

    private static void WhenReady(Visual target, Action<CompositionVisual> start, int tries)
    {
        if (ElementComposition.GetElementVisual(target) is { } visual)
        {
            start(visual);
            return;
        }

        if (tries <= 0) return;
        if (TopLevel.GetTopLevel(target) is { } top)
        {
            top.RequestAnimationFrame(_ => WhenReady(target, start, tries - 1));
            return;
        }

        // not in the tree yet: a hidden UserControl adds its content only when it first measures
        void Attached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            target.AttachedToVisualTree -= Attached;
            WhenReady(target, start, tries);
        }

        target.AttachedToVisualTree += Attached;
    }


    private static void Fade(CompositionVisual visual, double from, double to, TimeSpan duration, Easing easing, TimeSpan delay)
    {
        var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        if (!MotionTokens.IsFrozen && duration > TimeSpan.Zero) animation.InsertKeyFrame(0f, (float)from);
        animation.InsertKeyFrame(1f, (float)to, easing);
        animation.Duration = Live(duration);
        animation.DelayTime = MotionTokens.IsFrozen ? TimeSpan.Zero : delay;
        visual.StartAnimation("Opacity", animation);
    }


    private static void Move(CompositionVisual visual, Vector from, Vector to, TimeSpan duration, Easing easing, TimeSpan delay)
    {
        var animation = visual.Compositor.CreateVector3DKeyFrameAnimation();
        if (!MotionTokens.IsFrozen && duration > TimeSpan.Zero) animation.InsertKeyFrame(0f, new Vector3D(from.X, from.Y, 0));
        animation.InsertKeyFrame(1f, new Vector3D(to.X, to.Y, 0), easing);
        animation.Duration = Live(duration);
        animation.DelayTime = MotionTokens.IsFrozen ? TimeSpan.Zero : delay;
        visual.StartAnimation("Translation", animation);
    }


    private static void Scale(CompositionVisual visual, Size size, double from, double to, TimeSpan duration, Easing easing, RelativePoint? origin)
    {
        var centre = (origin ?? RelativePoint.Center).ToPixels(new Rect(size));
        visual.CenterPoint = new Vector3D(centre.X, centre.Y, 0);

        var animation = visual.Compositor.CreateVector3DKeyFrameAnimation();
        if (!MotionTokens.IsFrozen && duration > TimeSpan.Zero) animation.InsertKeyFrame(0f, new Vector3D(from, from, 1));
        animation.InsertKeyFrame(1f, new Vector3D(to, to, 1), easing);
        animation.Duration = Live(duration);
        visual.StartAnimation("Scale", animation);
    }


    private static TimeSpan Live(TimeSpan duration) => MotionTokens.IsFrozen || duration <= TimeSpan.Zero ? Instant : duration;

    #endregion // Private methods
}
