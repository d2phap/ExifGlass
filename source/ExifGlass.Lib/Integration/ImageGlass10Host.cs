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
#if !EG_MAS
using Avalonia.Threading;
using ExifGlass.Diagnostics;
using ImageGlass.SDK.Tools;

namespace ExifGlass.Integration;

/// <summary>
/// An ImageGlass 10 tool through ImageGlass.SDK (--pipe &lt;name&gt;). The SDK calls the hooks on its pipe thread; each only hands over to the UI thread.
/// </summary>
public sealed class ImageGlass10Host(string pipeName) : IImageSourceHost
{
    #region Fields and constants

    public const string TOOL_ID = "Tool_ExifGlass";

    private readonly Tool _tool = new();
    private long _generation;
    private bool _disposed;

    #endregion // Fields and constants


    #region Events

    public event EventHandler<FileRequestedEventArgs>? FileRequested;

    public event EventHandler? ActivateRequested;

    public event EventHandler<CloseRequestedEventArgs>? CloseRequested;

    #endregion // Events


    #region Public methods

    public void Start()
    {
        _tool.Host = this;
        _ = Task.Run(RunAsync);
    }


    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // fails a host call still waiting: on a dead pipe it never completes by itself (S6)
        _tool.Dispose();
    }

    #endregion // Public methods


    #region Private methods

    private async Task RunAsync()
    {
        string reason;
        try
        {
            await _tool.RunAsync(["--pipe", pipeName]).ConfigureAwait(false);
            reason = _tool.ShutdownRequested ? "shutdown" : "pipe-closed";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            reason = "run-failed";
            IpcLog.Write("RUN_FAILED", ("error", ex.GetType().Name + ": " + ex.Message));
        }

        IpcLog.Write("SESSION_END", ("reason", reason));
        if (reason == "shutdown") return;

        // D20: ImageGlass went away without SHUTDOWN, so the window stays as a standalone viewer
        Dispatcher.UIThread.Post(Dispose);
        IpcLog.Write("STANDALONE", ("reason", reason));
    }


    // the generation is taken before the request: any change that arrives while it is out makes the reply stale (S6)
    private async Task RequestPhotoAsync(string source)
    {
        var generation = Interlocked.Increment(ref _generation);
        ToolPhotoMetadata? photo;
        try
        {
            photo = await _tool.HostApi.GetPhotoMetadataAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            IpcLog.Write("HOST_CALL_FAILED", ("source", source), ("error", ex.GetType().Name + ": " + ex.Message));
            return;
        }

        if (photo?.FilePath is not { Length: > 0 } path) return;

        // checked on the UI thread, where the changes queue in order: a reply can't be ordered against the events around it on the pipe
        Dispatcher.UIThread.Post(() =>
        {
            if (generation != Volatile.Read(ref _generation))
            {
                IpcLog.Write("STALE_REPLY", ("source", source), ("path", path));
                return;
            }

            IpcLog.Write("META_REPLY", ("source", source), ("path", path));
            FileRequested?.Invoke(this, new FileRequestedEventArgs(path, isNavigation: false));
        });
    }


    private void PhotoChanged(string path)
    {
        Interlocked.Increment(ref _generation);
        Dispatcher.UIThread.Post(() => FileRequested?.Invoke(this, new FileRequestedEventArgs(path, isNavigation: true)));
    }

    #endregion // Private methods


    #region Nested types

    private sealed class Tool : ToolBase
    {
        public ImageGlass10Host Host { get; set; } = null!;

        public bool ShutdownRequested { get; private set; }

        public override string ToolId => TOOL_ID;


        protected override Task OnInitializedAsync()
        {
            IpcLog.Write("INIT");
            return Host.RequestPhotoAsync("init");
        }


        // sent once, right after INIT: the hotkey does nothing while the tool runs (S6)
        protected override Task OnExecuteAsync(CancellationToken ct)
        {
            IpcLog.Write("EXECUTE");
            Dispatcher.UIThread.Post(() => Host.ActivateRequested?.Invoke(Host, EventArgs.Empty));
            return Host.RequestPhotoAsync("execute");
        }


        protected override void OnPhotoChanged(PhotoChangedEventArgs e)
        {
            IpcLog.Write("PHOTO_CHANGED", ("path", e.FilePath));
            if (!string.IsNullOrEmpty(e.FilePath)) Host.PhotoChanged(e.FilePath);
        }


        // ImageGlass kills the tool 3 s after this (R21): the app's exit path runs on the UI thread, never through ShutdownRequested (R11)
        protected override Task OnShutdownAsync()
        {
            ShutdownRequested = true;
            IpcLog.Write("SHUTDOWN");
            Dispatcher.UIThread.Post(() => Host.CloseRequested?.Invoke(Host, new CloseRequestedEventArgs(isShutdown: true)));
            return Task.CompletedTask;
        }
    }

    #endregion // Nested types
}
#endif
