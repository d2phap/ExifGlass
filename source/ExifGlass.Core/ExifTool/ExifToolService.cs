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
using System.Buffers.Text;
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
            InterpreterPath = location.Interpreter,
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

    public int ProcessId => _daemon.ProcessId;

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
    public Task<MetadataDocument> ReadAsync(string path, IReadOnlyList<string> passThrough, TagDb? tagDb = null, CancellationToken cancellationToken = default) =>
        ReadAsync(path, path, passThrough, tagDb, cancellationToken);


    /// <summary>
    /// The table read of <paramref name="exifToolPath"/>, a container copy (D7), shown as <paramref name="shownPath"/>: the document and its File:Directory name the user's file.
    /// </summary>
    public async Task<MetadataDocument> ReadAsync(string exifToolPath, string shownPath, IReadOnlyList<string> passThrough, TagDb? tagDb, CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        var pool = _pool;
        var read = await _daemon.ExecuteAsync(ExifToolCommands.TableRead(exifToolPath, passThrough),
            (in ExifToolOutput o, CancellationToken ct) => ReadTable(o, pool, ct), cancellationToken).ConfigureAwait(false);

        if (!string.Equals(exifToolPath, shownPath, StringComparison.Ordinal)) ShowAs(read, shownPath);
        return MetadataDocument.Create(shownPath, read, tagDb, watch.Elapsed);
    }


    /// <summary>
    /// The AI read and parse; null when the file has no AI data or provenance.
    /// </summary>
    public Task<AiGenerationInfo?> ReadAiAsync(string path, CancellationToken cancellationToken = default) => ReadAiAsync(path, path, cancellationToken);


    /// <summary>
    /// The AI read of a container copy (D7); the sidecar is looked for next to the user's file.
    /// </summary>
    public async Task<AiGenerationInfo?> ReadAiAsync(string exifToolPath, string shownPath, CancellationToken cancellationToken)
    {
        using var tags = await ReadAiTagsAsync(exifToolPath, cancellationToken).ConfigureAwait(false);
        var sidecar = AiEngine.ReadSidecar(shownPath);

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
            (in ExifToolOutput o, CancellationToken _) => SaveResult.Parse(o.StdoutText, o.Stderr, o.Status, path), cancellationToken, writes: true);
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


    // the copy's folder is the container's stage; the table shows the folder the user opened
    private static void ShowAs(TableReadResult read, string shownPath)
    {
        read.SourceFile = shownPath;
        var directory = Path.GetDirectoryName(Path.GetFullPath(shownPath)) ?? string.Empty;
        for (var i = 0; i < read.Tags.Count; i++)
        {
            var tag = read.Tags[i];
            if (tag.Group0 == "File" && tag.Name == "Directory") read.Tags[i] = tag.WithValue(directory);
        }
    }


    // -b values come as "base64:..." in -j output, the largest the best; JsonDocument would copy the reply into a pooled array each thread keeps (plan 8)
    private static byte[]? LargestBinary(in ReadOnlySequence<byte> json)
    {
        if (json.IsEmpty) return null;

        var reader = new Utf8JsonReader(json);
        byte[]? best = null;
        try
        {
            while (reader.Read())
            {
                if (reader.TokenType != JsonTokenType.String) continue;

                var length = reader.HasValueSequence ? reader.ValueSequence.Length : reader.ValueSpan.Length;
                if (length <= BASE64_PREFIX.Length || (best is not null && (length - BASE64_PREFIX.Length) * 3 / 4 <= best.Length)) continue;

                var text = new byte[length];
                var written = reader.CopyString(text);
                if (!text.AsSpan(0, written).StartsWith("base64:"u8)) continue;

                var payload = text.AsSpan(BASE64_PREFIX.Length, written - BASE64_PREFIX.Length);
                if (Base64.DecodeFromUtf8InPlace(payload, out var decoded) == OperationStatus.Done) best = payload[..decoded].ToArray();
            }
        }
        catch (JsonException)
        {
            // a cut reply: keep what was found before it
        }

        return best;
    }

    #endregion // Private methods
}
