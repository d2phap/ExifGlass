/*
ExifGlass - Standalone Exif tool for ImageGlass
Copyright (C) 2023-2024 DUONG DIEU PHAP
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
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Styling;

namespace ExifGlass;

public static class Extensions
{
    /// <summary>
    /// Sets dynamic resource.
    /// </summary>
    public static T SetDynamicResource<T>(this T control, AvaloniaProperty prop, object resourceKey) where T : Control
    {
        control[!prop] = new DynamicResourceExtension(resourceKey);
        return control;
    }


    /// <summary>
    /// Sets dynamic resource.
    /// </summary>
    public static Style SetDynamicResource(this Style style, AvaloniaProperty prop, object resourceKey)
    {
        style.Setters.Add(new Setter()
        {
            Property = prop,
            Value = new DynamicResourceExtension(resourceKey)
        });
        return style;
    }

}
