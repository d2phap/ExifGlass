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
/// A file a host asks for: a viewer's photo change waits out a burst, any other request reads at once.
/// </summary>
public sealed class FileRequestedEventArgs(string path, bool isNavigation) : EventArgs
{
    #region Properties

    public string Path { get; } = path;

    /// <summary>
    /// A photo change while the user browses (PHOTO_CHANGED, IMAGE_LOADING), throttled by the S6 policy.
    /// </summary>
    public bool IsNavigation { get; } = isNavigation;

    #endregion // Properties
}


/// <summary>
/// Why a host asks the app to end.
/// </summary>
public sealed class CloseRequestedEventArgs(bool isShutdown) : EventArgs
{
    #region Properties

    /// <summary>
    /// ImageGlass 10's SHUTDOWN: it kills the tool 3 s later (R21), so no dialog and the staged edits go to the recovery file (D29).
    /// </summary>
    public bool IsShutdown { get; } = isShutdown;

    #endregion // Properties
}


/// <summary>
/// Where file requests come from (P10): the command line, ImageGlass 10 or ImageGlass 9. Events are raised on the UI thread.
/// </summary>
public interface IImageSourceHost : IDisposable
{
    #region Events

    event EventHandler<FileRequestedEventArgs>? FileRequested;

    /// <summary>
    /// Occurs when the host wants the window in front (ImageGlass 10's EXECUTE).
    /// </summary>
    event EventHandler? ActivateRequested;

    event EventHandler<CloseRequestedEventArgs>? CloseRequested;

    #endregion // Events


    #region Methods

    /// <summary>
    /// Starts taking requests: emits the command-line file or connects the pipe.
    /// </summary>
    void Start();

    #endregion // Methods
}
