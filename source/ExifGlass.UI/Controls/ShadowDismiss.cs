/*
ExifGlass - AI & EXIF metadata viewer
Copyright (C) 2023 - 2027 DUONG DIEU PHAP
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
using Avalonia.Input;
using Avalonia.VisualTree;

namespace ExifGlass.UI.Controls;

/// <summary>
/// Closes a light-dismissed popup on a press in its shadow room: the room is part of the popup's own window,
/// so the owner's dismiss layer never sees that press.
/// </summary>
public static class ShadowDismiss
{
    #region Fields and constants

    private static bool _registered;

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Watches every popup's presses; called once at startup.
    /// </summary>
    public static void Register()
    {
        if (_registered) return;

        _registered = true;
        InputElement.PointerPressedEvent.AddClassHandler<PopupRoot>(PopupRoot_PointerPressed);
    }

    #endregion // Public methods


    #region Event handlers

    // the root's transparent background takes a press that misses the content
    private static void PopupRoot_PointerPressed(PopupRoot root, PointerPressedEventArgs e)
    {
        if (e.Source is not Visual hit || root.Content is not Visual content || content == hit || content.IsVisualAncestorOf(hit)) return;

        // a submenu isn't light-dismissed itself: the menu it opened from closes and takes it along
        Popup? dismiss = null;
        for (var popup = root.Parent as Popup; popup is not null; popup = (TopLevel.GetTopLevel(popup.PlacementTarget ?? popup) as PopupRoot)?.Parent as Popup)
        {
            if (popup.IsLightDismissEnabled) dismiss = popup;
        }

        if (dismiss is null) return;

        e.Handled = true;
        dismiss.Close();
    }

    #endregion // Event handlers
}
