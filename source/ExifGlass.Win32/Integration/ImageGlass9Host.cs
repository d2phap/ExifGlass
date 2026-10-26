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
using Avalonia.Threading;
using ExifGlass.Diagnostics;
using ImageGlass.Tools;

namespace ExifGlass.Integration;

/// <summary>
/// An ImageGlass 9 external tool through ImageGlass.Tools (--ig-tool-pipe-code=). Its events arrive on the pipe thread; each only hands over to the UI thread.
/// </summary>
public sealed class ImageGlass9Host(string? file) : IImageSourceHost
{
    #region Fields and constants

    private readonly Tool _tool = new();
    private int _closing;
    private bool _disposed;

    #endregion // Fields and constants


    #region Events

    public event EventHandler<FileRequestedEventArgs>? FileRequested;

    public event EventHandler? ActivateRequested
    {
        add { }
        remove { }
    }

    public event EventHandler<CloseRequestedEventArgs>? CloseRequested;

    #endregion // Events


    #region Public methods

    public void Start()
    {
        _tool.ToolMessageReceived += Tool_MessageReceived;
        _tool.ToolClosingRequest += Tool_ClosingRequest;

        // the photo ImageGlass 9 shows comes on the command line, before any event
        if (!string.IsNullOrEmpty(file)) FileRequested?.Invoke(this, new FileRequestedEventArgs(file, isNavigation: false));

        _ = ConnectAsync();
    }


    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _tool.ToolMessageReceived -= Tool_MessageReceived;
        _tool.ToolClosingRequest -= Tool_ClosingRequest;
        _tool.Dispose();
    }

    #endregion // Public methods


    #region Private methods

    private async Task ConnectAsync()
    {
        try
        {
            await _tool.ConnectAsync().ConfigureAwait(false);
            IpcLog.Write("CONNECTED", ("pipe", ImageGlassTool.GetServerNameFromCmdLineArgs()));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // the window stays as a standalone viewer of the command-line file
            IpcLog.Write("CONNECT_FAILED", ("error", ex.GetType().Name + ": " + ex.Message));
        }
    }

    #endregion // Private methods


    #region Event handlers

    private void Tool_MessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        if (!e.MessageName.Equals(ImageGlassEvents.IMAGE_LOADING, StringComparison.OrdinalIgnoreCase)) return;

        var args = IgImageLoadingEventArgs.Deserialize(e.MessageData);
        IpcLog.Write("IMAGE_LOADING", ("path", args?.FilePath));
        if (args?.FilePath is not { Length: > 0 } path) return;

        Dispatcher.UIThread.Post(() => FileRequested?.Invoke(this, new FileRequestedEventArgs(path, isNavigation: true)));
    }


    // ImageGlass 9 sends TOOL_TERMINATE only when the tool is turned off; when it exits the pipe just drops, which closes the app too (D20 is ImageGlass 10's)
    private void Tool_ClosingRequest(object? sender, DisconnectedEventArgs e)
    {
        if (Interlocked.Exchange(ref _closing, 1) == 1) return;

        IpcLog.Write("IG9_CLOSING", ("reason", _tool.TerminateRequested ? "terminate" : "pipe-closed"));
        Dispatcher.UIThread.Post(() => CloseRequested?.Invoke(this, new CloseRequestedEventArgs(isShutdown: false)));
    }

    #endregion // Event handlers


    #region Nested types

    // ImageGlass.Tools raises ToolClosingRequest for TOOL_TERMINATE and for a dropped pipe alike; the log tells them apart
    private sealed class Tool : ImageGlassTool
    {
        public bool TerminateRequested { get; private set; }

        protected override void OnToolMessageReceived(MessageReceivedEventArgs e)
        {
            if (e.MessageName == ImageGlassEvents.TOOL_TERMINATE) TerminateRequested = true;
            base.OnToolMessageReceived(e);
        }
    }

    #endregion // Nested types
}
