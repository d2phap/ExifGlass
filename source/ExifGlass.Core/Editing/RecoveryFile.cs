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
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExifGlass.Core.Editing;

/// <summary>
/// D29: the edits staged when ImageGlass shut the app down, kept next to the settings until the next start offers them back.
/// </summary>
public sealed class RecoveryFile
{
    #region Fields and constants

    public const string FILE_NAME = "exifglass.recovery.json";

    #endregion // Fields and constants


    #region Properties

    public required string SourcePath { get; init; }

    /// <summary>
    /// The file's size and time when it was read, to tell whether it changed since.
    /// </summary>
    public long FileSize { get; init; }

    public DateTimeOffset? FileModified { get; init; }

    public DateTimeOffset SavedAt { get; init; }

    public List<TagEdit> Edits { get; init; } = [];

    public List<AddedTag> Added { get; init; } = [];

    public bool RemoveGps { get; init; }

    public bool StripAll { get; init; }

    public AiRedactionPlan? AiRedaction { get; init; }

    public int PendingCount => Edits.Count + Added.Count + (RemoveGps ? 1 : 0) + (StripAll ? 1 : 0) + (AiRedaction is null ? 0 : 1);

    public string FileName => Path.GetFileName(SourcePath);

    #endregion // Properties


    #region Public methods

    public static RecoveryFile Create(string sourcePath, long fileSize, DateTimeOffset? fileModified, EditSet edits, DateTimeOffset savedAt) => new()
    {
        SourcePath = sourcePath,
        FileSize = fileSize,
        FileModified = fileModified,
        SavedAt = savedAt,
        Edits = [.. edits.Edits.Values.OrderBy(e => e.Key, StringComparer.Ordinal)],
        Added = [.. edits.Added],
        RemoveGps = edits.RemoveGps,
        StripAll = edits.StripAll,
        AiRedaction = edits.AiRedaction,
    };


    /// <summary>
    /// The staged edits again; an edit of a tag the file no longer has is dropped and counted.
    /// </summary>
    public EditSet ToEditSet(Func<string, bool> hasTag, out int dropped)
    {
        var set = EditSet.Empty;
        dropped = 0;

        foreach (var edit in Edits)
        {
            if (!hasTag(edit.Key))
            {
                dropped++;
                continue;
            }

            set = edit.Value is { } value
                ? set.Set(edit.Key, edit.WriteName, value, edit.OriginalValue, edit.IsRaw)
                : set.Delete(edit.Key, edit.WriteName, edit.OriginalValue);
        }

        foreach (var added in Added) set = set.Add(added);

        return set.WithRemoveGps(RemoveGps).WithStripAll(StripAll).WithRemoveAi(AiRedaction);
    }


    /// <summary>
    /// Whether the file on disk differs from the one the edits were made on.
    /// </summary>
    public bool FileChanged(long size, DateTimeOffset? modified) => size != FileSize || modified != FileModified;


    /// <summary>
    /// Writes the file whole or not at all: a kill 3 s after SHUTDOWN can't leave half of it (R21).
    /// </summary>
    public static void Save(string directory, RecoveryFile file)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, FILE_NAME);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(file, RecoveryJsonContext.Default.RecoveryFile));
        File.Move(tmp, path, overwrite: true);
    }


    /// <summary>
    /// The recovery file in the folder; null when there is none or it can't be read.
    /// </summary>
    public static RecoveryFile? Load(string directory)
    {
        var path = Path.Combine(directory, FILE_NAME);
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize(File.ReadAllText(path), RecoveryJsonContext.Default.RecoveryFile) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return null;
        }
    }


    public static void Delete(string directory)
    {
        try
        {
            File.Delete(Path.Combine(directory, FILE_NAME));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // offered again at the next start
        }
    }

    #endregion // Public methods
}


[JsonSourceGenerationOptions(WriteIndented = true, IgnoreReadOnlyProperties = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RecoveryFile))]
internal sealed partial class RecoveryJsonContext : JsonSerializerContext;
