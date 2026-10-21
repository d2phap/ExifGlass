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
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using ExifGlass.UI.Controls;
using ExifGlass.UI.Tokens;

namespace ExifGlass.UI;

/// <summary>
/// Starts the design system once the app's resources are loaded: the system mono font, menu motion, and grayscale text on every window and popup (S3).
/// </summary>
public static class DesignSystem
{
    #region Fields and constants

    private static bool _initialized;

    #endregion // Fields and constants


    #region Public methods

    public static void Initialize(Application app)
    {
        AppFonts.ApplyMono(app.Resources);
        if (_initialized) return;

        _initialized = true;
        MenuMotion.Register();
        FocusCue.Register();

        // popups are top levels of their own, so the window's text mode doesn't reach them
        TemplatedControl.TemplateAppliedEvent.AddClassHandler<TopLevel>((top, _) => TextOptions.SetTextRenderingMode(top, TextRenderingMode.Antialias));

        // a text box that isn't focused places its caret as it loads: that must not scroll the page around it
        Control.RequestBringIntoViewEvent.AddClassHandler<TextBox>((box, e) =>
        {
            if (!box.IsKeyboardFocusWithin) e.Handled = true;
        });
    }

    #endregion // Public methods
}
