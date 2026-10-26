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
using ExifGlass.Core.Editing;
using ExifGlass.Core.ExifTool;
using ExifGlass.Core.Metadata;
using ExifGlass.Core.Settings;
using ExifGlass.Diagnostics;
using System.ComponentModel;
using System.Diagnostics;

namespace ExifGlass.Services;

/// <summary>
/// Where the app's ExifTool stands.
/// </summary>
public enum ExifToolStatus
{
    Starting,
    Ready,

    /// <summary>
    /// No ExifTool anywhere: not at the Settings path, not bundled, not on PATH.
    /// </summary>
    Missing,

    /// <summary>
    /// Found but it didn't start or answer.
    /// </summary>
    Failed,
}


/// <summary>
/// The reads a document load needs; the app's is ExifTool, tests bring their own.
/// </summary>
public interface IMetadataReader
{
    #region Methods

    Task<MetadataDocument> ReadAsync(string path, CancellationToken cancellationToken);

    /// <summary>
    /// The AI read and parse; null when the file has no AI data or provenance.
    /// </summary>
    Task<AiGenerationInfo?> ReadAiAsync(string path, CancellationToken cancellationToken);

    /// <summary>
    /// The largest embedded preview image's bytes; null when the file has none.
    /// </summary>
    Task<byte[]?> ReadPreviewAsync(string path, CancellationToken cancellationToken) => Task.FromResult<byte[]?>(null);

    /// <summary>
    /// Writes the staged edits in one ExifTool call; ExifTool keeps FILE_original.
    /// </summary>
    Task<SaveResult> WriteAsync(string path, EditSet edits, CancellationToken cancellationToken) =>
        Task.FromResult(new SaveResult(SaveOutcome.Failed, [AppText.SAVE_UNAVAILABLE], [], null));

    /// <summary>
    /// A binary tag's bytes (D12); null when there are none.
    /// </summary>
    Task<byte[]?> ReadBinaryAsync(string path, string writeName, CancellationToken cancellationToken) => Task.FromResult<byte[]?>(null);

    #endregion // Methods
}


/// <summary>
/// A read asked for while there is no ExifTool to run it (Appendix B item 3).
/// </summary>
public sealed class ExifToolUnavailableException(string message) : Exception(message);


/// <summary>
/// Owns the app's one ExifTool: finds it, warms it up in the background right after launch (plan 4.4), and runs the table and AI reads.
/// </summary>
public sealed class MetadataService : IMetadataReader, IAsyncDisposable
{
    #region Fields and constants

    private readonly Func<AppSettings> _settings;
    private readonly Lock _lock = new();
    private ExifToolService? _exifTool;
    private Task _started = Task.CompletedTask;
    private Task<TagDb?>? _tagDb;
    private ExifToolStatus _status = ExifToolStatus.Starting;

    #endregion // Fields and constants


    #region Constructors

    /// <summary>
    /// Reads the ExifTool path and arguments from <paramref name="settings"/> each time they are needed.
    /// </summary>
    public MetadataService(Func<AppSettings> settings) => _settings = settings;

    #endregion // Constructors


    #region Properties

    public ExifToolStatus Status
    {
        get
        {
            lock (_lock) return _status;
        }
    }

    /// <summary>
    /// What went wrong when the status is Missing or Failed.
    /// </summary>
    public string? Problem { get; private set; }

    public ExifToolLocation? Location => _exifTool?.Location;

    public string? Version => _exifTool?.Version;

    #endregion // Properties


    #region Events

    /// <summary>
    /// Occurs when Status changes; raised on a background thread.
    /// </summary>
    public event EventHandler? StatusChanged;

    #endregion // Events


    #region Public methods

    /// <summary>
    /// Finds ExifTool and starts it on the thread pool, never on the UI path (plan 8: warm-up).
    /// </summary>
    public void Start() => _started = Task.Run(StartCoreAsync);


    /// <summary>
    /// Stops the current ExifTool and starts again: Retry, or a new path from Settings.
    /// </summary>
    public async Task RestartAsync()
    {
        var old = _exifTool;
        _exifTool = null;
        SetStatus(ExifToolStatus.Starting, null);
        if (old is not null) await old.DisposeAsync().ConfigureAwait(false);

        Start();
        await _started.ConfigureAwait(false);
    }


    public async Task<MetadataDocument> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var service = await ReadyAsync(cancellationToken).ConfigureAwait(false);

        // loaded with the first read, not at launch: the idle heap stays small (plan 8), and it takes about 2 ms
        Task<TagDb?> load;
        lock (_lock) load = _tagDb ??= Task.Run(() => service.GetTagDbAsync());
        var tagDb = await load.ConfigureAwait(false);
        var arguments = ArgumentPolicy.Analyze(_settings().ExifToolArguments).PassThrough;

        return await service.ReadAsync(path, arguments, tagDb, cancellationToken).ConfigureAwait(false);
    }


    public async Task<AiGenerationInfo?> ReadAiAsync(string path, CancellationToken cancellationToken)
    {
        var service = await ReadyAsync(cancellationToken).ConfigureAwait(false);
        return await service.ReadAiAsync(path, cancellationToken).ConfigureAwait(false);
    }


    public async Task<byte[]?> ReadPreviewAsync(string path, CancellationToken cancellationToken)
    {
        var service = await ReadyAsync(cancellationToken).ConfigureAwait(false);
        return await service.ReadPreviewAsync(path, cancellationToken).ConfigureAwait(false);
    }


    public async Task<SaveResult> WriteAsync(string path, EditSet edits, CancellationToken cancellationToken)
    {
        var service = await ReadyAsync(cancellationToken).ConfigureAwait(false);
        using var plan = WritePlan.Build(path, edits);
        return await service.WriteAsync(plan, cancellationToken).ConfigureAwait(false);
    }


    public async Task<byte[]?> ReadBinaryAsync(string path, string writeName, CancellationToken cancellationToken)
    {
        var service = await ReadyAsync(cancellationToken).ConfigureAwait(false);
        return await service.ReadBinaryAsync(path, writeName, cancellationToken).ConfigureAwait(false);
    }


    /// <summary>
    /// The terminal-equivalent read command for the command bar (plan 4.4), with the file or a placeholder.
    /// </summary>
    public IReadOnlyList<CommandToken> ReadCommand(string? path)
    {
        var location = Location;
        return WritePlan.ReadTokens(Executable(location), location?.ConfigPath, ArgumentPolicy.Split(_settings().ExifToolArguments), path);
    }


    /// <summary>
    /// The write Save runs, for the command bar while edits are staged (DS 7.3); values inline, no temp file.
    /// </summary>
    public IReadOnlyList<CommandToken> WriteCommand(string path, EditSet edits)
    {
        var location = Location;
        using var plan = WritePlan.Preview(path, edits);
        return plan.Tokens(Executable(location), location?.ConfigPath);
    }


    /// <summary>
    /// Asks ExifTool to exit (-stay_open False), killing it after the grace period.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        var service = _exifTool;
        _exifTool = null;
        if (service is not null) await service.DisposeAsync().ConfigureAwait(false);
    }

    #endregion // Public methods


    #region Private methods

    private async Task StartCoreAsync()
    {
        var location = ExifToolLocator.Locate(_settings().ExifToolPath);
        if (location is null)
        {
            SetStatus(ExifToolStatus.Missing, "No ExifTool was found");
            return;
        }

        var service = new ExifToolService(location);
        _exifTool = service;
        lock (_lock) _tagDb = null;

        try
        {
            var watch = Stopwatch.StartNew();
            await service.StartAsync().ConfigureAwait(false);
            IpcLog.Write("EXIFTOOL_READY", ("pid", service.ProcessId), ("warmMs", watch.Elapsed.TotalMilliseconds));
            SetStatus(ExifToolStatus.Ready, location.CustomPathMissing ? "The ExifTool set in Settings wasn't found" : null);
        }
        catch (Exception ex) when (ex is ExifToolException or IOException or TimeoutException or Win32Exception or InvalidOperationException)
        {
            SetStatus(ExifToolStatus.Failed, ex.Message);
        }
    }


    private static string Executable(ExifToolLocation? location) => location?.Path ?? (OperatingSystem.IsWindows() ? "exiftool.exe" : "exiftool");


    private async Task<ExifToolService> ReadyAsync(CancellationToken cancellationToken)
    {
        await _started.WaitAsync(cancellationToken).ConfigureAwait(false);

        if (_exifTool is { } service && Status == ExifToolStatus.Ready) return service;
        throw new ExifToolUnavailableException(Problem ?? AppText.EXIFTOOL_MISSING_TITLE);
    }


    private void SetStatus(ExifToolStatus status, string? problem)
    {
        lock (_lock)
        {
            _status = status;
            Problem = problem;
        }

        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion // Private methods
}
