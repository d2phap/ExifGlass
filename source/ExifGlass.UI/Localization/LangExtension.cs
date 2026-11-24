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
using Avalonia.Data;
using Avalonia.Markup.Xaml.MarkupExtensions;
using ExifGlass.Core.Localization;

namespace ExifGlass.UI.Localization;

/// <summary>
/// <c>{l:Lang Settings_Language}</c>: a <see cref="LangId"/> the XAML compiler checks, shown through a dynamic resource so it follows a language change.
/// </summary>
public sealed class LangExtension
{
    #region Constructors

    public LangExtension(LangId key)
    {
        Key = key;
    }

    #endregion // Constructors


    #region Properties

    public LangId Key { get; set; }

    #endregion // Properties


    #region Public methods

    public BindingBase ProvideValue(IServiceProvider serviceProvider) => new DynamicResourceExtension(Key).ProvideValue(serviceProvider);

    #endregion // Public methods
}
