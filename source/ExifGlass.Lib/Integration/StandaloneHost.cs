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
namespace ExifGlass.Integration;

/// <summary>
/// Started by the user: the command-line file, if any. The picker, drops and macOS Open With go to the view model directly.
/// </summary>
public sealed class StandaloneHost(string? file) : IImageSourceHost
{
    #region Events

    public event EventHandler<FileRequestedEventArgs>? FileRequested;

    public event EventHandler? ActivateRequested
    {
        add { }
        remove { }
    }

    public event EventHandler<CloseRequestedEventArgs>? CloseRequested
    {
        add { }
        remove { }
    }

    #endregion // Events


    #region Public methods

    public void Start()
    {
        if (!string.IsNullOrEmpty(file)) FileRequested?.Invoke(this, new FileRequestedEventArgs(file, isNavigation: false));
    }


    public void Dispose()
    {
    }

    #endregion // Public methods
}
