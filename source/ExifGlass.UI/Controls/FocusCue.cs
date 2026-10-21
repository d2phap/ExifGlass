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
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ExifGlass.UI.Controls;

/// <summary>
/// The focus ring cue for menu items, which Avalonia focuses under the arrow keys without :focus-visible: :keyboard-focus marks the focused item while the last input was a key, as a browser's :focus-visible.
/// </summary>
public static class FocusCue
{
    #region Fields and constants

    public const string KEYBOARD_FOCUS = ":keyboard-focus";

    private static bool _registered;
    private static bool _keyboard;
    private static WeakReference<MenuItem>? _focused;

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Watches every window's input and every menu item's focus; called once at startup.
    /// </summary>
    public static void Register()
    {
        if (_registered) return;

        _registered = true;
        InputElement.KeyDownEvent.AddClassHandler<TopLevel>((_, _) => SetKeyboard(true), RoutingStrategies.Tunnel, handledEventsToo: true);
        InputElement.PointerPressedEvent.AddClassHandler<TopLevel>((_, _) => SetKeyboard(false), RoutingStrategies.Tunnel, handledEventsToo: true);
        InputElement.PointerMovedEvent.AddClassHandler<TopLevel>((_, _) => SetKeyboard(false), RoutingStrategies.Tunnel, handledEventsToo: true);

        // menu items don't raise GotFocus in Avalonia 12, but IsFocused changes
        InputElement.IsFocusedProperty.Changed.AddClassHandler<MenuItem>((item, e) =>
        {
            var focused = e.GetNewValue<bool>();
            if (focused) _focused = new WeakReference<MenuItem>(item);
            Mark(item, focused && _keyboard);
        });
    }

    #endregion // Public methods


    #region Private methods

    private static void SetKeyboard(bool keyboard)
    {
        if (_keyboard == keyboard) return;

        _keyboard = keyboard;
        if (_focused?.TryGetTarget(out var item) == true && item.IsFocused) Mark(item, keyboard);
    }


    private static void Mark(MenuItem item, bool on) => ((IPseudoClasses)item.Classes).Set(KEYBOARD_FOCUS, on);

    #endregion // Private methods
}
