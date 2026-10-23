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
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ExifGlass.Core.Ai;
using ExifGlass.Core.ExifTool;
using ExifGlass.Core.Metadata;
using System.Diagnostics;
using System.Text.Json;

namespace ExifGlass.Services;

/// <summary>
/// Where the window's document stands.
/// </summary>
public enum LoadPhase
{
    Empty,

    /// <summary>
    /// A read is running; the previous document, if any, stays on screen until it ends.
    /// </summary>
    Loading,

    Ready,
}


/// <summary>
/// A read that failed: the file, and ExifTool's or the system's words for it.
/// </summary>
public sealed class LoadFailedEventArgs(string path, string message) : EventArgs
{
    #region Properties

    public string Path { get; } = path;

    public string Message { get; } = message;

    #endregion // Properties
}


/// <summary>
/// The open pipeline (P4): the newest request wins, the table comes first and the AI read follows; UI thread only, so its awaits need no lock.
/// </summary>
public sealed class DocumentLoader(IMetadataReader reader)
{
    #region Fields and constants

    private CancellationTokenSource? _cts;
    private int _generation;

    #endregion // Fields and constants


    #region Properties

    public LoadPhase Phase { get; private set; } = LoadPhase.Empty;

    /// <summary>
    /// The document on screen; one at a time, the previous one released when the next is shown.
    /// </summary>
    public MetadataDocument? Document { get; private set; }

    /// <summary>
    /// The file being read, while Phase is Loading.
    /// </summary>
    public string? PendingPath { get; private set; }

    /// <summary>
    /// The AI read runs after the table (plan 2.2 Fast); true from the table shown until it ends.
    /// </summary>
    public bool IsReadingAi { get; private set; }

    /// <summary>
    /// From the request to the document applied, for the last load that finished (plan 8: open a JPEG).
    /// </summary>
    public TimeSpan LastLoadTime { get; private set; }

    /// <summary>
    /// The document's preview, decoded at the Frame card's size; null until it is ready or when there is none.
    /// </summary>
    public Bitmap? Preview { get; private set; }

    /// <summary>
    /// Gets whether the document's preview is still being decoded.
    /// </summary>
    public bool IsPreviewPending { get; private set; }

    /// <summary>
    /// Device pixels per DIP that previews are decoded for; the window sets its own.
    /// </summary>
    public double PreviewScaling { get; set; } = 2;

    #endregion // Properties


    #region Events

    /// <summary>
    /// Occurs when the phase or the document changes, the AI data arriving included.
    /// </summary>
    public event EventHandler? Changed;

    public event EventHandler<LoadFailedEventArgs>? Failed;

    public event EventHandler? PreviewChanged;

    #endregion // Events


    #region Public methods

    /// <summary>
    /// Reads a file; a newer call cancels this one, and a read that ends late is dropped.
    /// </summary>
    public async Task OpenAsync(string path)
    {
        var generation = ++_generation;
        var watch = Stopwatch.StartNew();
        _cts?.Cancel();
        _cts?.Dispose();
        var cts = _cts = new CancellationTokenSource();
        var token = cts.Token;

        PendingPath = path;
        Phase = LoadPhase.Loading;
        IsReadingAi = false;
        Changed?.Invoke(this, EventArgs.Empty);

        MetadataDocument document;
        try
        {
            document = await reader.ReadAsync(path, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            if (generation == _generation) Fail(path, ex.Message);
            return;
        }

        if (generation != _generation) return;
        if (document.Tags.Count == 0)
        {
            Fail(path, document.Error ?? "ExifTool found no metadata in it");
            return;
        }

        Document = document;
        PendingPath = null;
        Phase = LoadPhase.Ready;
        IsReadingAi = true;
        LastLoadTime = watch.Elapsed;
        SetPreview(null, pending: true);
        Changed?.Invoke(this, EventArgs.Empty);

        // a file Skia decodes itself needs no ExifTool, so it decodes while the AI read runs; an embedded preview waits behind it
        var preview = PreviewService.IsDirect(document) ? PreviewService.LoadAsync(document, reader, PreviewScaling, token) : null;

        AiGenerationInfo? ai = null;
        try
        {
            ai = await reader.ReadAiAsync(path, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            Abandon(preview);
            return;
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            // the table is already shown; a failed AI read leaves it as it is
        }

        if (generation != _generation)
        {
            Abandon(preview);
            return;
        }

        IsReadingAi = false;
        if (ai is not null) Document = document.WithAi(ai);
        Changed?.Invoke(this, EventArgs.Empty);

        await ApplyPreviewAsync(preview ?? PreviewService.LoadAsync(document, reader, PreviewScaling, token), generation, token);
    }

    #endregion // Public methods


    #region Private methods

    private static bool IsReadFailure(Exception ex) => ex is ExifToolException or ExifToolUnavailableException or IOException or TimeoutException
        or UnauthorizedAccessException or InvalidDataException or JsonException or ArgumentException or ObjectDisposedException;


    // a newer file took over: its decode may still finish, and nothing else will free it
    private static void Abandon(Task<Bitmap?>? preview) =>
        preview?.ContinueWith(static t => t.Result?.Dispose(), CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);


    private async Task ApplyPreviewAsync(Task<Bitmap?> load, int generation, CancellationToken token)
    {
        Bitmap? bitmap;
        try
        {
            bitmap = await load;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }

        if (generation != _generation)
        {
            bitmap?.Dispose();
            return;
        }

        SetPreview(bitmap, pending: false);
    }


    // one preview in memory (plan 8); the old one goes after the views have let go of it
    private void SetPreview(Bitmap? bitmap, bool pending)
    {
        if (ReferenceEquals(Preview, bitmap) && IsPreviewPending == pending) return;

        var old = Preview;
        Preview = bitmap;
        IsPreviewPending = pending;
        PreviewChanged?.Invoke(this, EventArgs.Empty);
        if (old is not null && !ReferenceEquals(old, bitmap)) Dispatcher.UIThread.Post(old.Dispose, DispatcherPriority.Background);
    }


    // a failed read keeps the last good document (Appendix B item 3)
    private void Fail(string path, string message)
    {
        PendingPath = null;
        Phase = Document is null ? LoadPhase.Empty : LoadPhase.Ready;
        Failed?.Invoke(this, new LoadFailedEventArgs(path, message));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    #endregion // Private methods
}
