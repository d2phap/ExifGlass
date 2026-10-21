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
using Avalonia.Controls;
using Avalonia.Input;

namespace ExifGlass.Views;

public partial class AppToolbar : UserControl
{
    #region Constructors

    public AppToolbar()
    {
        InitializeComponent();
    }

    #endregion // Constructors


    #region Public methods

    /// <summary>
    /// Ctrl+K (DS 7.6): focuses the search with its text selected, as Tab would.
    /// </summary>
    public void FocusSearch()
    {
        if (!Search.IsEffectivelyEnabled) return;

        Search.Focus(NavigationMethod.Tab);
        Search.SelectAll();
    }

    #endregion // Public methods
}
