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
using System.Globalization;

namespace ExifGlass.Core.Editing;

/// <summary>
/// D7: the Mac App Store build's ExifTool sees only the app's container (S7), so it reads and writes copies staged there, and the app writes a saved copy over the user's file in place.
/// </summary>
public sealed class SandboxStaging : IDisposable
{
    #region Fields and constants

    public const string BACKUP_FOLDER = "Backups";

    // the file on screen and the one before it, whose reads may still be finishing
    private const int KEPT = 2;

    private readonly Lock _lock = new();
    private readonly List<Entry> _entries = [];
    private int _next;

    #endregion // Fields and constants


    #region Properties

    public string StageDirectory { get; }

    /// <summary>
    /// Where ExifTool's FILE_original backups go: yyyy-MM-dd/FILE_original folders.
    /// </summary>
    public string BackupDirectory { get; }

    #endregion // Properties


    #region Constructors

    public SandboxStaging(string stageDirectory, string backupDirectory)
    {
        StageDirectory = stageDirectory;
        BackupDirectory = backupDirectory;

        // copies a crash left behind
        TryDelete(stageDirectory);
    }

    #endregion // Constructors


    #region Public methods

    /// <summary>
    /// ~/Library/Caches/ExifGlass/stage, which the sandbox puts inside the app's container.
    /// </summary>
    public static string DefaultStageDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Caches", Common.AppInfo.PRODUCT_NAME, "stage");


    /// <summary>
    /// The copy ExifTool reads, made off the calling thread since a large file takes a while to copy.
    /// </summary>
    public Task<string> StageAsync(string path, CancellationToken cancellationToken = default) => Task.Run(() => Stage(path), cancellationToken);


    /// <summary>
    /// The copy ExifTool reads; copied again when the user's file changed since the last copy.
    /// </summary>
    public string Stage(string path)
    {
        var full = Path.GetFullPath(path);
        var stamp = FileStamp.Of(full);

        lock (_lock)
        {
            if (Find(full) is { } entry)
            {
                _entries.Remove(entry);
                if (entry.Stamp == stamp && File.Exists(entry.StagedPath))
                {
                    _entries.Add(entry);
                    return entry.StagedPath;
                }

                TryDelete(entry.Folder);
            }

            // a folder of its own keeps the name and extension, which ExifTool reads the format from
            var folder = Path.Combine(StageDirectory, (++_next).ToString(CultureInfo.InvariantCulture));
            Directory.CreateDirectory(folder);
            var staged = Path.Combine(folder, Path.GetFileName(full));
            File.Copy(full, staged, overwrite: true);
            CopyTimes(full, staged);

            _entries.Add(new Entry(full, staged, stamp));
            while (_entries.Count > KEPT)
            {
                TryDelete(_entries[0].Folder);
                _entries.RemoveAt(0);
            }

            return staged;
        }
    }


    /// <summary>
    /// After ExifTool wrote the copy: moves its FILE_original into the backups and writes the copy over the user's file in place; returns the backup, or null.
    /// </summary>
    public string? Commit(string path)
    {
        var full = Path.GetFullPath(path);

        lock (_lock)
        {
            var entry = Find(full) ?? throw new InvalidOperationException($"{full} wasn't staged");
            try
            {
                // S7 step 4: what changed since the copy would be lost
                if (FileStamp.Of(full) != entry.Stamp) throw new IOException($"{Path.GetFileName(full)} changed while it was being saved, so nothing was written to it");

                var original = entry.StagedPath + "_original";
                var backup = File.Exists(original) ? MoveToBackups(original, Path.GetFileName(full)) : null;

                try
                {
                    OverwriteInPlace(entry.StagedPath, full);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // a write cut short is undone from the backup the same way (S7 step 6)
                    if (backup is not null) TryOverwrite(backup, full);
                    throw new IOException(backup is null ? ex.Message : $"{ex.Message} The original is kept in {backup}", ex);
                }

                CopyTimes(full, entry.StagedPath);
                entry.Stamp = FileStamp.Of(full);
                return backup;
            }
            catch
            {
                Forget(entry);
                throw;
            }
        }
    }


    /// <summary>
    /// Drops the copy of a file, so the next read copies it again: a write that didn't reach the user's file may have changed it.
    /// </summary>
    public void Forget(string path)
    {
        var full = Path.GetFullPath(path);
        lock (_lock)
        {
            if (Find(full) is { } entry) Forget(entry);
        }
    }


    public void Dispose()
    {
        lock (_lock)
        {
            _entries.Clear();
            TryDelete(StageDirectory);
        }
    }

    #endregion // Public methods


    #region Private methods

    private Entry? Find(string fullPath) => _entries.Find(e => string.Equals(e.SourcePath, fullPath, StringComparison.Ordinal));


    private void Forget(Entry entry)
    {
        _entries.Remove(entry);
        TryDelete(entry.Folder);
    }


    private string MoveToBackups(string original, string fileName)
    {
        var folder = Directory.CreateDirectory(Path.Combine(BackupDirectory, DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))).FullName;
        var target = Path.Combine(folder, fileName + "_original");
        for (var n = 2; File.Exists(target); n++)
        {
            target = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(fileName)} ({n}){Path.GetExtension(fileName)}_original");
        }

        File.Move(original, target);
        return target;
    }


    // the same inode keeps Finder tags, xattrs, ACLs and the creation date (S7 W5); a rename would need the folder, which the sandbox doesn't grant
    private static void OverwriteInPlace(string source, string target)
    {
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.SequentialScan);
        using var output = new FileStream(target, FileMode.Open, FileAccess.Write, FileShare.Read, 1 << 16);
        input.CopyTo(output);
        output.SetLength(output.Position);
        output.Flush(flushToDisk: true);
    }


    private static void TryOverwrite(string source, string target)
    {
        try
        {
            OverwriteInPlace(source, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // the error names the backup, which still holds the original
        }
    }


    // the table shows the copy's File dates, so they are the user's file's
    private static void CopyTimes(string from, string to)
    {
        try
        {
            File.SetCreationTimeUtc(to, File.GetCreationTimeUtc(from));
            File.SetLastWriteTimeUtc(to, File.GetLastWriteTimeUtc(from));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // the copy keeps its own dates
        }
    }


    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // a copy ExifTool still holds goes with the next start
        }
    }

    #endregion // Private methods


    #region Nested types

    private sealed class Entry(string sourcePath, string stagedPath, FileStamp stamp)
    {
        public string SourcePath { get; } = sourcePath;

        public string StagedPath { get; } = stagedPath;

        public string Folder => Path.GetDirectoryName(StagedPath)!;

        public FileStamp Stamp { get; set; } = stamp;
    }


    private readonly record struct FileStamp(long Length, DateTime Modified)
    {
        public static FileStamp Of(string path)
        {
            var info = new FileInfo(path);
            if (!info.Exists) throw new FileNotFoundException($"{Path.GetFileName(path)} wasn't found", path);
            return new FileStamp(info.Length, info.LastWriteTimeUtc);
        }
    }

    #endregion // Nested types
}
