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
using ExifGlass.Core.Metadata;
using System.Buffers;
using System.Diagnostics;
using System.Text.Json;

namespace ExifGlass.Core.ExifTool;

/// <summary>
/// Every ExifTool call the app makes, on one daemon: the table read, the AI read, writes, previews and the tag table.
/// </summary>
public sealed class ExifToolService : IAsyncDisposable
{
    #region Fields and constants

    private const string BASE64_PREFIX = "base64:";

    private readonly ExifToolDaemon _daemon;
    private readonly StringPool _pool = new();

    #endregion // Fields and constants


    #region Constructors

    public ExifToolService(ExifToolLocation location, TimeSpan? timeout = null)
    {
        Location = location;
        _daemon = new ExifToolDaemon(new ExifToolDaemonOptions
        {
            ExePath = location.Path,
            ConfigPath = location.ConfigPath,
            Timeout = timeout ?? TimeSpan.FromSeconds(30),
            PrimingFiles = PrimingFiles.Ensure(),
        });
    }

    #endregion // Constructors


    #region Properties

    public ExifToolLocation Location { get; }

    public ExifToolDaemon Daemon => _daemon;

    public string? Version => _daemon.Version;

    /// <summary>
    /// What this ExifTool is too old to read for the AI card (R19); empty for the bundled one.
    /// </summary>
    public IReadOnlyList<ExifToolFeature> MissingAiFeatures => Version is null ? [] : ExifToolVersion.MissingAiFeatures(Version);

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Starts ExifTool in the background: -ver, then the priming read.
    /// </summary>
    public Task<string> StartAsync(CancellationToken cancellationToken = default) => _daemon.StartAsync(cancellationToken);


    /// <summary>
    /// The table read: one JSON read with the allowlisted user flags, parsed straight from the pipe.
    /// </summary>
    public async Task<MetadataDocument> ReadAsync(string path, IReadOnlyList<string> passThrough, TagDb? tagDb = null, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        var pool = _pool;
        var read = await _daemon.ExecuteAsync(ExifToolCommands.TableRead(path, passThrough),
            (in ExifToolOutput o, CancellationToken ct) => ReadTable(o, pool, ct), cancellationToken).ConfigureAwait(false);

        return MetadataDocument.Create(path, read, tagDb, watch.Elapsed);
    }


    /// <summary>
    /// The AI read and parse; null when the file has no AI data or provenance.
    /// </summary>
    public async Task<AiGenerationInfo?> ReadAiAsync(string path, CancellationToken cancellationToken = default)
    {
        using var tags = await ReadAiTagsAsync(path, cancellationToken).ConfigureAwait(false);
        var sidecar = AiEngine.ReadSidecar(path);

        // multi-megabyte workflows parse off the daemon's thread so the next read isn't held up
        return await Task.Run(() => AiEngine.Parse(tags, sidecar, cancellationToken), cancellationToken).ConfigureAwait(false);
    }


    /// <summary>
    /// The AI read's tags, for the parsers and the self-test; the caller disposes them.
    /// </summary>
    public Task<AiTagSet> ReadAiTagsAsync(string path, CancellationToken cancellationToken = default)
    {
        var pool = _pool;
        return _daemon.ExecuteAsync(ExifToolCommands.AiRead(path),
            (in ExifToolOutput o, CancellationToken _) => AiTagSet.Parse(o.Stdout, pool), cancellationToken);
    }


    /// <summary>
    /// One write; ExifTool keeps FILE_original next to the file.
    /// </summary>
    public Task<SaveResult> WriteAsync(WritePlan plan, CancellationToken cancellationToken = default)
    {
        var path = plan.SourcePath;
        return _daemon.ExecuteAsync(ExifToolCommands.Write(path, plan.Arguments),
            (in ExifToolOutput o, CancellationToken _) => SaveResult.Parse(o.StdoutText, o.Stderr, o.Status, path), cancellationToken);
    }


    /// <summary>
    /// The largest embedded preview (JpgFromRaw, PreviewImage, ThumbnailImage), or null.
    /// </summary>
    public Task<byte[]?> ReadPreviewAsync(string path, CancellationToken cancellationToken = default) =>
        _daemon.ExecuteAsync(ExifToolCommands.PreviewRead(path), static (in ExifToolOutput o, CancellationToken _) => LargestBinary(o.Stdout), cancellationToken);


    /// <summary>
    /// A binary tag's bytes (D12), or null.
    /// </summary>
    public Task<byte[]?> ReadBinaryAsync(string path, string writeName, CancellationToken cancellationToken = default) =>
        _daemon.ExecuteAsync(ExifToolCommands.BinaryRead(path, writeName), static (in ExifToolOutput o, CancellationToken _) => LargestBinary(o.Stdout), cancellationToken);


    /// <summary>
    /// The tag table of this ExifTool, for a custom one whose version differs from the embedded table's.
    /// </summary>
    public async Task<TagDb?> BuildTagDbAsync(CancellationToken cancellationToken = default)
    {
        var version = Version ?? await StartAsync(cancellationToken).ConfigureAwait(false);
        return await _daemon.ExecuteAsync(ExifToolCommands.ListTags(),
            (in ExifToolOutput o, CancellationToken _) => o.Stdout.Length > 0 ? TagDb.FromListx(o.Stdout.ToArray(), version) : null, cancellationToken).ConfigureAwait(false);
    }


    /// <summary>
    /// The embedded table when it matches this ExifTool's version, else one built from it, else the embedded one anyway.
    /// </summary>
    public async Task<TagDb?> GetTagDbAsync(CancellationToken cancellationToken = default)
    {
        var embedded = TagDb.LoadEmbedded();
        if (embedded is not null && (Location.Source == ExifToolSource.Bundled || embedded.ExifToolVersion == Version)) return embedded;

        try
        {
            return await BuildTagDbAsync(cancellationToken).ConfigureAwait(false) ?? embedded;
        }
        catch (Exception ex) when (ex is ExifToolException or TimeoutException or IOException or InvalidDataException)
        {
            return embedded;
        }
    }


    public ValueTask DisposeAsync() => _daemon.DisposeAsync();

    #endregion // Public methods


    #region Private methods

    private static TableReadResult ReadTable(in ExifToolOutput o, StringPool pool, CancellationToken cancellationToken)
    {
        var result = TableReader.Read(o.Stdout, pool, cancellationToken);

        // a missing file prints nothing on stdout, only "Error: File not found" on stderr
        if (result.Tags.Count == 0 && result.Error is null && o.Stderr.Length > 0)
        {
            var line = o.Stderr.Split('\n').FirstOrDefault(l => l.StartsWith("Error:", StringComparison.Ordinal));
            if (line is not null) result.Error = line[6..].Split(" - ")[0].Trim();
        }

        return result;
    }


    // -b values come as "base64:..." inside -j output; the largest is the best preview
    private static byte[]? LargestBinary(in ReadOnlySequence<byte> json)
    {
        if (json.IsEmpty) return null;

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0) return null;

        string? best = null;
        foreach (var p in doc.RootElement[0].EnumerateObject())
        {
            if (p.Value.ValueKind != JsonValueKind.String) continue;

            var s = p.Value.GetString()!;
            if (s.StartsWith(BASE64_PREFIX, StringComparison.Ordinal) && (best is null || s.Length > best.Length)) best = s;
        }

        if (best is null) return null;

        try
        {
            return Convert.FromBase64String(best[BASE64_PREFIX.Length..]);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    #endregion // Private methods
}
