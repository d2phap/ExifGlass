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
using Avalonia.Layout;
using Avalonia.Threading;
using ExifGlass.UI.Controls;

namespace ExifGlass.UI.Gallery;

/// <summary>
/// Pins a control in a state for the gallery and the snapshot tests (plan P3: every state, forced).
/// </summary>
public static class GalleryPins
{
    #region Public methods

    /// <summary>
    /// Shows hover: the :pointerover styles and the state layer's hover brush.
    /// </summary>
    public static T Hover<T>(this T control) where T : Control
    {
        Pin(control, ":pointerover");
        InteractionOverlay.SetForcedState(control, InteractionState.Hover);
        return control;
    }


    /// <summary>
    /// Shows pressed: the :pressed styles, the press brush and the press scale.
    /// </summary>
    public static T Pressed<T>(this T control) where T : Control
    {
        Pin(control, ":pointerover");
        Pin(control, ":pressed");
        InteractionOverlay.SetForcedState(control, InteractionState.Pressed);
        return control;
    }


    /// <summary>
    /// Shows the keyboard focus ring.
    /// </summary>
    public static T FocusVisible<T>(this T control) where T : Control
    {
        Pin(control, ":focus");
        Pin(control, ":focus-visible");
        Pin(control, ":focus-within");
        return control;
    }


    public static T Disabled<T>(this T control) where T : Control
    {
        control.IsEnabled = false;
        return control;
    }


    public static T Checked<T>(this T control, bool? value = true) where T : ToggleButton
    {
        control.IsChecked = value;
        return control;
    }


    /// <summary>
    /// Shows a dropdown or combo box open without opening its popup.
    /// </summary>
    public static T Open<T>(this T control) where T : Control
    {
        Pin(control, ":flyout-open");
        Pin(control, ":dropdownopen");
        Pin(control, ":selected");
        return control;
    }


    public static T With<T>(this T control, params string[] classes) where T : Control
    {
        foreach (var c in classes) control.Classes.Add(c);
        return control;
    }


    public static T Left<T>(this T control) where T : Control
    {
        control.HorizontalAlignment = HorizontalAlignment.Left;
        control.VerticalAlignment = VerticalAlignment.Center;
        return control;
    }

    #endregion // Public methods


    #region Private methods

    // a control re-derives some pseudo-classes as it sets itself up, so they are pinned again once that's done
    private static void Pin(Control control, string pseudoClass)
    {
        ((IPseudoClasses)control.Classes).Add(pseudoClass);
        if (control is TemplatedControl templated)
        {
            templated.TemplateApplied += (_, _) => Dispatcher.UIThread.Post(() => ((IPseudoClasses)control.Classes).Add(pseudoClass), DispatcherPriority.Background);
        }
    }

    #endregion // Private methods
}
