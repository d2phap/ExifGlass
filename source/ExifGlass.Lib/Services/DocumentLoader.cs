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
    /// From the request to the document applied, for the last load that finished (plan 8: open a JPEG).
    /// </summary>
    public TimeSpan LastLoadTime { get; private set; }

    #endregion // Properties


    #region Events

    /// <summary>
    /// Occurs when the phase or the document changes, the AI data arriving included.
    /// </summary>
    public event EventHandler? Changed;

    public event EventHandler<LoadFailedEventArgs>? Failed;

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
        LastLoadTime = watch.Elapsed;
        Changed?.Invoke(this, EventArgs.Empty);

        AiGenerationInfo? ai;
        try
        {
            ai = await reader.ReadAiAsync(path, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            // the table is already shown; a failed AI read leaves it as it is
            return;
        }

        if (generation != _generation || ai is null) return;

        Document = document.WithAi(ai);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    #endregion // Public methods


    #region Private methods

    private static bool IsReadFailure(Exception ex) => ex is ExifToolException or ExifToolUnavailableException or IOException or TimeoutException
        or UnauthorizedAccessException or InvalidDataException or JsonException or ArgumentException or ObjectDisposedException;


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
