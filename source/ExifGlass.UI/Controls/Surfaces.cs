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
using Avalonia.Controls;
using Avalonia.Threading;
using ExifGlass.UI.Motion;
using ExifGlass.UI.Tokens;

namespace ExifGlass.UI.Controls;

/// <summary>
/// A card (DS 3.2, 6.11): Bg.Panel, radius 18, hairline, padding 12; classes accent (selected tag: Accent 35% border and a 3 px ring) and ai (Accent2 30% border).
/// </summary>
public class Card : ContentControl
{
    #region Fields and constants

    private const double RISE = 12;

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Plays the inspector's entry: fade and a 12 px rise in 400 ms Emphasized (DS 5.2).
    /// </summary>
    public void Appear(TimeSpan delay = default) => PlayAppear(this, delay);


    /// <summary>
    /// The card entry for any surface that draws itself as a card.
    /// </summary>
    public static void PlayAppear(Visual target, TimeSpan delay = default)
    {
        Dispatcher.UIThread.Post(() =>
        {
            CompositionMotion.Fade(target, 0, 1, MotionTokens.CardAppear, MotionTokens.CardAppearEasing, delay);
            CompositionMotion.Move(target, new Vector(0, RISE), default, MotionTokens.CardAppear, MotionTokens.CardAppearEasing, delay);
        }, DispatcherPriority.Render);
    }

    #endregion // Public methods
}
