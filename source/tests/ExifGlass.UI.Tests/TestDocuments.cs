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
using ExifGlass.Core.Metadata;
using ExifGlass.Services;
using System.Buffers;
using System.Globalization;
using System.Text;

namespace ExifGlass.UI.Tests;

/// <summary>
/// Documents made in memory, so shell tests need no ExifTool.
/// </summary>
public static class TestDocuments
{
    #region Fields and constants

    private static readonly string[] Groups = ["ExifTool:ExifTool", "File:System", "EXIF:IFD0", "IPTC:IPTC", "MakerNotes:FujiFilm", "XMP:XMP-x", "Composite:Composite"];

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// A document of <paramref name="tags"/> tags spread over seven groups, as ExifTool's -j -l -G0:1:4 read gives them.
    /// </summary>
    public static MetadataDocument Make(string path, int tags = 24)
    {
        var json = new StringBuilder("[{\"SourceFile\": \"").Append(path.Replace('\\', '/')).Append('"');
        json.Append(", \"ExifTool:ExifTool:ExifToolVersion\": {\"id\": \"ExifToolVersion\", \"desc\": \"ExifTool Version Number\", \"val\": 13.59}");

        for (var i = 1; i < tags; i++)
        {
            var group = Groups[1 + (i - 1) % (Groups.Length - 1)];
            json.Append(CultureInfo.InvariantCulture, $", \"{group}:Tag{i}\": {{\"id\": \"0x{i:x4}\", \"desc\": \"Tag {i}\", \"val\": \"Value {i}\"}}");
        }

        json.Append("}]");
        var read = TableReader.Read(new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(json.ToString())), new StringPool());
        return MetadataDocument.Create(path, read);
    }

    #endregion // Public methods
}


/// <summary>
/// A reader whose reads finish when a test says so; a path not held back finishes at once.
/// </summary>
public sealed class FakeReader : IMetadataReader
{
    #region Fields and constants

    private readonly Dictionary<string, TaskCompletionSource<MetadataDocument>> _held = new(StringComparer.Ordinal);

    #endregion // Fields and constants


    #region Properties

    public int Reads { get; private set; }

    public int AiReads { get; private set; }

    /// <summary>
    /// Gets or sets the tag count of the documents made for paths that aren't held.
    /// </summary>
    public int Tags { get; set; } = 24;

    /// <summary>
    /// Gets or sets a failure every read throws.
    /// </summary>
    public Exception? Failure { get; set; }

    #endregion // Properties


    #region Public methods

    public void Hold(string path) => _held[path] = new TaskCompletionSource<MetadataDocument>(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Release(string path, int tags = 24) => _held[path].TrySetResult(TestDocuments.Make(path, tags));


    public Task<MetadataDocument> ReadAsync(string path, CancellationToken cancellationToken)
    {
        Reads++;
        if (Failure is { } failure) return Task.FromException<MetadataDocument>(failure);
        if (!_held.TryGetValue(path, out var held)) return Task.FromResult(TestDocuments.Make(path, Tags));

        return held.Task.WaitAsync(cancellationToken);
    }


    public Task<AiGenerationInfo?> ReadAiAsync(string path, CancellationToken cancellationToken)
    {
        AiReads++;
        return Task.FromResult<AiGenerationInfo?>(null);
    }

    #endregion // Public methods
}

