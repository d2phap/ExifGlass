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
using ExifGlass.Core.Integration;
using ExifGlass.Core.Metadata;
using ExifGlass.Diagnostics;
using ExifGlass.Services;
using ExifGlass.ViewModels;
using System.Diagnostics;

namespace ExifGlass.Integration;

/// <summary>
/// What a host may ask of the main window.
/// </summary>
public interface IHostWindow
{
    #region Methods

    void ActivateFromHost();

    /// <summary>
    /// Closes as the user would: D8 asks first when edits are staged.
    /// </summary>
    void Close();

    /// <summary>
    /// Closes without D8's dialog, at ImageGlass's SHUTDOWN (D29 kept the edits).
    /// </summary>
    void CloseForHost();

    #endregion // Methods
}


/// <summary>
/// Feeds a host's requests to the window (P10): S6's load policy; the file being read or the unchanged file on screen isn't read again; while a dialog is up the newest request waits for it; the SHUTDOWN exit path inside ImageGlass's 3 s.
/// </summary>
public sealed class HostSession : IDisposable
{
    #region Fields and constants

    private readonly IImageSourceHost _host;
    private readonly MainViewModel _viewModel;
    private readonly IHostWindow _window;
    private readonly Func<ValueTask> _stopExifTool;
    private readonly NavigationThrottle _throttle;

    private bool _holding;
    private string? _held;
    private bool _closing;
    private MetadataDocument? _shown;

    #endregion // Fields and constants


    #region Constructors

    /// <summary>
    /// <paramref name="stopExifTool"/> ends ExifTool within its stop budget, before the window closes.
    /// </summary>
    public HostSession(IImageSourceHost host, MainViewModel viewModel, IHostWindow window, Func<ValueTask> stopExifTool, TimeProvider? time = null)
    {
        _host = host;
        _viewModel = viewModel;
        _window = window;
        _stopExifTool = stopExifTool;
        _throttle = new NavigationThrottle(Open, static a => Dispatcher.UIThread.Post(a), time);
    }

    #endregion // Constructors


    #region Properties

    public IImageSourceHost Host => _host;

    #endregion // Properties


    #region Public methods

    public void Start()
    {
        _host.FileRequested += Host_FileRequested;
        _host.ActivateRequested += Host_ActivateRequested;
        _host.CloseRequested += Host_CloseRequested;
        _viewModel.Loader.Changed += Loader_Changed;
        _viewModel.Loader.Failed += Loader_Failed;
        _host.Start();
    }


    /// <summary>
    /// D29 at the start: the host's requests wait while the restore question is up; a restored file stays on screen.
    /// </summary>
    public async Task OfferRecoveryAsync()
    {
        _holding = true;
        bool restored;
        try
        {
            restored = await _viewModel.OfferRecoveryAsync();
        }
        finally
        {
            _holding = false;
        }

        var held = _held;
        _held = null;
        if (!restored && held is not null) Open(held);
    }


    public void Dispose()
    {
        _host.FileRequested -= Host_FileRequested;
        _host.ActivateRequested -= Host_ActivateRequested;
        _host.CloseRequested -= Host_CloseRequested;
        _viewModel.Loader.Changed -= Loader_Changed;
        _viewModel.Loader.Failed -= Loader_Failed;
        _throttle.Dispose();
        _host.Dispose();
    }

    #endregion // Public methods


    #region Private methods

    // the throttle's start: S6's adopt and dedupe, then D8 once for however many requests come while it asks
    private void Open(string path)
    {
        if (_closing) return;
        if (_holding)
        {
            _held = path;
            return;
        }

        var loader = _viewModel.Loader;
        if (loader.Phase == LoadPhase.Loading && SamePath(loader.PendingPath, path))
        {
            IpcLog.Write("ADOPT_INFLIGHT", ("path", path));
            return;
        }

        if (loader.Phase != LoadPhase.Loading && loader.Document is { } doc && SamePath(doc.SourcePath, path) && Unchanged(doc))
        {
            IpcLog.Write("DEDUPE", ("path", path));
            return;
        }

        if (_viewModel.HasPending)
        {
            _ = AskThenOpenAsync(path);
            return;
        }

        IpcLog.Write("LOAD_START", ("path", path));
        _ = _viewModel.OpenFileAsync(path);
    }


    private async Task AskThenOpenAsync(string path)
    {
        _holding = true;
        _held = path;
        bool go;
        try
        {
            go = await _viewModel.ConfirmLeaveAsync();
        }
        finally
        {
            _holding = false;
        }

        var newest = _held;
        _held = null;
        if (go && newest is not null) Open(newest);
    }


    // D29 and R21: ImageGlass kills the app 3 s after SHUTDOWN, so the edits go to a file, ExifTool stops first and nothing asks
    private async Task ShutdownAsync()
    {
        if (_closing) return;
        _closing = true;

        var start = Stopwatch.GetTimestamp();
        _throttle.Stop();
        var kept = await _viewModel.KeepForRecoveryAsync();
        _viewModel.Loader.Cancel();
        await _stopExifTool();
        IpcLog.Write("EXIFTOOL_STOPPED", ("ms", IpcLog.Ms(start, Stopwatch.GetTimestamp())), ("recovery", kept));

        _window.CloseForHost();
    }


    private static bool SamePath(string? a, string? b) => a is not null && b is not null
        && string.Equals(a.Replace('\\', '/'), b.Replace('\\', '/'), OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);


    private static bool Unchanged(MetadataDocument doc)
    {
        try
        {
            var file = new FileInfo(doc.SourcePath);
            return file.Exists && file.Length == doc.FileSize && file.LastWriteTimeUtc == doc.FileModified;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    #endregion // Private methods


    #region Event handlers

    private void Host_FileRequested(object? sender, FileRequestedEventArgs e)
    {
        if (e.IsNavigation) _throttle.Navigate(e.Path);
        else _throttle.Request(e.Path);
    }


    private void Host_ActivateRequested(object? sender, EventArgs e) => _window.ActivateFromHost();


    private void Host_CloseRequested(object? sender, CloseRequestedEventArgs e)
    {
        if (e.IsShutdown)
        {
            _ = ShutdownAsync();
            return;
        }

        // ImageGlass 9 has no kill deadline: the window closes as the user would close it
        _throttle.Stop();
        IpcLog.Write("CLOSE");
        _window.Close();
    }


    // the test host's evidence: which file is on screen, once its AI read is done, and the memory then
    private void Loader_Changed(object? sender, EventArgs e)
    {
        var loader = _viewModel.Loader;
        if (!IpcLog.IsOn || loader.Phase == LoadPhase.Loading || loader.Document is not { } doc) return;

        if (!doc.IsSameRead(_shown))
        {
            _shown = doc;
            IpcLog.Write("LOADED", ("path", doc.SourcePath), ("ms", loader.LastLoadTime.TotalMilliseconds));
        }

        if (loader.IsReadingAi || _throttle.Pending is not null || _holding) return;
        if (IpcLog.CollectsOnSettle)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        IpcLog.Write("SETTLED", ("path", doc.SourcePath), ("ai", doc.Ai is not null), ("ws", Environment.WorkingSet), ("heap", GC.GetTotalMemory(false)));
    }


    private void Loader_Failed(object? sender, LoadFailedEventArgs e) => IpcLog.Write("LOAD_FAILED", ("path", e.Path), ("error", e.Message));

    #endregion // Event handlers
}
