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


    /// <summary>
    /// An A1111 PNG as the table read gives it: PNG:Parameters with the infotext, an IPTC source type beside it, and the size tags.
    /// </summary>
    public static MetadataDocument MakeAi(string path)
    {
        var json = new StringBuilder("[{\"SourceFile\": \"").Append(path.Replace('\\', '/')).Append('"');
        json.Append(", \"ExifTool:ExifTool:ExifToolVersion\": {\"id\": \"ExifToolVersion\", \"desc\": \"ExifTool Version Number\", \"val\": 13.59}");
        json.Append(", \"PNG:ImageWidth\": {\"id\": \"0x0000\", \"desc\": \"Image Width\", \"val\": 832}");
        json.Append(", \"PNG:Parameters\": {\"id\": \"parameters\", \"desc\": \"Parameters\", \"val\": ").Append(JsonString(A1111)).Append('}');
        json.Append(", \"XMP:XMP-iptcExt:DigitalSourceType\": {\"id\": \"DigitalSourceType\", \"desc\": \"Digital Source Type\", \"val\": \"").Append(SOURCE_TYPE).Append("\"}");
        json.Append("}]");
        var read = TableReader.Read(new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(json.ToString())), new StringPool());
        return MetadataDocument.Create(path, read);
    }


    /// <summary>
    /// A raw file with a thumbnail ExifTool can extract and nothing for the Exposure or Details cards.
    /// </summary>
    public static MetadataDocument MakeRaw(string path)
    {
        var json = new StringBuilder("[{\"SourceFile\": \"").Append(path.Replace('\\', '/')).Append('"');
        json.Append(", \"ExifTool:ExifTool:ExifToolVersion\": {\"id\": \"ExifToolVersion\", \"desc\": \"ExifTool Version Number\", \"val\": 13.59}");
        json.Append(", \"File:System:FileType\": {\"id\": \"FileType\", \"desc\": \"File Type\", \"val\": \"ARW\"}");
        json.Append(", \"EXIF:IFD1:ThumbnailImage\": {\"id\": \"0x0201\", \"desc\": \"Thumbnail Image\", \"val\": \"(Binary data 9 bytes, use -b option to extract)\"}");
        json.Append("}]");
        var read = TableReader.Read(new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(json.ToString())), new StringPool());
        return MetadataDocument.Create(path, read);
    }


    /// <summary>
    /// What the AI read finds in MakeAi's file.
    /// </summary>
    public static AiGenerationInfo Ai()
    {
        using var tags = AiTagSet.FromPairs([("PNG:Main:Parameters", A1111), ("XMP:XMP-iptcExt:Main:DigitalSourceType", SOURCE_TYPE)]);
        return AiEngine.Parse(tags)!;
    }

    #endregion // Public methods


    #region Private methods

    private static string JsonString(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";

    #endregion // Private methods


    #region Constants

    public const string SOURCE_TYPE = "http://cv.iptc.org/newscodes/digitalsourcetype/trainedAlgorithmicMedia";

    public const string A1111 = "a lighthouse on a basalt cliff, (volumetric fog:1.2), <lora:add_detail:0.8>\nNegative prompt: lowres, easynegative\n"
        + "Steps: 30, Sampler: DPM++ 2M, Schedule type: Karras, CFG scale: 6.5, Seed: 2841937305, Size: 1024x1536, Model hash: 31e35c80fc, "
        + "Model: sd_xl_base_1.0, VAE hash: 235745af8d, VAE: sdxl_vae, Denoising strength: 0.35, Clip skip: 2, Hires upscale: 1.5, Hires steps: 15, "
        + "Hires upscaler: 4x-UltraSharp, Lora hashes: \"add_detail: 7c6bad76eb54\", TI hashes: \"easynegative: c74b4e810b03\", Version: v1.10.1";

    #endregion // Constants
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

    /// <summary>
    /// Gets or sets what the AI read returns.
    /// </summary>
    public AiGenerationInfo? Ai { get; set; }

    /// <summary>
    /// Gets or sets how the documents of paths that aren't held are made; TestDocuments.Make by default.
    /// </summary>
    public Func<string, MetadataDocument>? Maker { get; set; }

    /// <summary>
    /// Gets or sets an AI read that ends when the test completes it, for a slow one.
    /// </summary>
    public TaskCompletionSource<AiGenerationInfo?>? AiHold { get; set; }

    #endregion // Properties


    #region Public methods

    public void Hold(string path) => _held[path] = new TaskCompletionSource<MetadataDocument>(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Release(string path, int tags = 24) => _held[path].TrySetResult(TestDocuments.Make(path, tags));


    public Task<MetadataDocument> ReadAsync(string path, CancellationToken cancellationToken)
    {
        Reads++;
        if (Failure is { } failure) return Task.FromException<MetadataDocument>(failure);
        if (!_held.TryGetValue(path, out var held)) return Task.FromResult(Maker?.Invoke(path) ?? TestDocuments.Make(path, Tags));

        return held.Task.WaitAsync(cancellationToken);
    }


    public Task<AiGenerationInfo?> ReadAiAsync(string path, CancellationToken cancellationToken)
    {
        AiReads++;
        return AiHold is { } hold ? hold.Task.WaitAsync(cancellationToken) : Task.FromResult(Ai);
    }

    #endregion // Public methods
}

