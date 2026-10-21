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

namespace ExifGlass.Views;

/// <summary>
/// The drag-over scrim shown while a file is over the window.
/// </summary>
public partial class DropScrim : UserControl
{
    #region Fields and constants

    private const double RISE = 12;

    private int _generation;
    private bool _shown;

    #endregion // Fields and constants


    #region Constructors

    public DropScrim()
    {
        InitializeComponent();
    }

    #endregion // Constructors


    #region Public methods

    /// <summary>
    /// Shows the scrim: fade 200 ms and the message rising 12 px (Proto's egRise).
    /// </summary>
    public void Show()
    {
        _generation++;
        if (_shown) return;

        // shown again while it fades out: back to full at once, without the rise
        _shown = true;
        var fadingOut = IsVisible;
        IsVisible = true;
        Ring.IsPulsing = true;
        Dispatcher.UIThread.Post(() =>
        {
            CompositionMotion.Fade(this, fadingOut ? 1 : 0, 1, MotionTokens.Scrim, MotionTokens.ScrimEasing);
            if (!fadingOut) CompositionMotion.Move(Message, new Vector(0, RISE), default, MotionTokens.Scrim, MotionTokens.ScrimEasing);
        }, DispatcherPriority.Render);
    }


    public void Hide()
    {
        if (!_shown) return;

        _shown = false;
        var generation = ++_generation;
        CompositionMotion.Fade(this, 1, 0, MotionTokens.Scrim, MotionTokens.ScrimEasing);
        DispatcherTimer.RunOnce(() =>
        {
            if (generation != _generation) return;

            IsVisible = false;
            Ring.IsPulsing = false;
        }, MotionTokens.IsFrozen ? TimeSpan.FromMilliseconds(1) : MotionTokens.Scrim);
    }

    #endregion // Public methods
}
