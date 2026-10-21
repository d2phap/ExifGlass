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

namespace ExifGlass.UI.Tokens;

/// <summary>
/// Motion tokens (DS 5); the generated part holds the durations and easings. No reduce-motion mode (O12).
/// </summary>
public static partial class MotionTokens
{
    #region Fields and constants

    /// <summary>
    /// The press scale of compact controls, 150 px wide or less (DS 4).
    /// </summary>
    public const double PRESS_SCALE = 0.96;

    /// <summary>
    /// Disabled controls fade to this opacity (DS 4).
    /// </summary>
    public const double DISABLED_OPACITY = 0.45;

    /// <summary>
    /// Pauses the endless animations under it (spinners, pulses); a window sets it while hidden.
    /// </summary>
    public static readonly AttachedProperty<bool> IsPausedProperty =
        AvaloniaProperty.RegisterAttached<Visual, bool>("IsPaused", typeof(MotionTokens), inherits: true);

    #endregion // Fields and constants


    #region Properties

    /// <summary>
    /// Makes every animation jump to its end state; UI tests freeze motion for stable captures.
    /// </summary>
    public static bool IsFrozen { get; set; }

    #endregion // Properties


    #region Public methods

    public static bool GetIsPaused(AvaloniaObject target) => target.GetValue(IsPausedProperty);

    public static void SetIsPaused(AvaloniaObject target, bool value) => target.SetValue(IsPausedProperty, value);


    /// <summary>
    /// Gets a duration, or zero while motion is frozen.
    /// </summary>
    public static TimeSpan Live(TimeSpan duration) => IsFrozen ? TimeSpan.Zero : duration;

    #endregion // Public methods
}
