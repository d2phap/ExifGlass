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
using ExifGlass.Core.Editing;
using Xunit;

namespace ExifGlass.Core.Tests.Editing;

/// <summary>
/// D7's container staging, which the Mac App Store build runs: copies in, a save written over the user's file in place, the backup in the container.
/// </summary>
public sealed class SandboxStagingTests(ExifToolFixture exifTool) : IDisposable
{
    #region Fields and constants

    private readonly string _root = Directory.CreateTempSubdirectory("exifglass-staging-").FullName;

    #endregion // Fields and constants


    #region Tests

    [Fact]
    public void CopiesAgainOnlyWhenTheFileChanged()
    {
        using var staging = NewStaging();
        var file = UserFile("photo one.jpg", "first");
        var created = File.GetCreationTimeUtc(file);

        var staged = staging.Stage(file);
        Assert.Equal("photo one.jpg", Path.GetFileName(staged));
        Assert.StartsWith(staging.StageDirectory, staged);
        Assert.Equal(File.GetLastWriteTimeUtc(file), File.GetLastWriteTimeUtc(staged));
        Assert.Equal(created, File.GetCreationTimeUtc(staged));
        Assert.Equal(staged, staging.Stage(file));

        File.WriteAllText(file, "second, longer");
        var again = staging.Stage(file);
        Assert.NotEqual(staged, again);
        Assert.False(File.Exists(staged));
        Assert.Equal("second, longer", File.ReadAllText(again));
    }


    [Fact]
    public void KeepsTheLastTwoFiles()
    {
        using var staging = NewStaging();
        var a = staging.Stage(UserFile("a.jpg", "a"));
        var b = staging.Stage(UserFile("b.jpg", "b"));
        var c = staging.Stage(UserFile("c.jpg", "c"));

        Assert.False(File.Exists(a));
        Assert.True(File.Exists(b) && File.Exists(c));

        staging.Dispose();
        Assert.False(Directory.Exists(staging.StageDirectory));
    }


    [Fact]
    public void CommitWritesOverTheFileInPlaceAndKeepsTheBackupInTheContainer()
    {
        using var staging = NewStaging();
        var file = UserFile("DSCF4127.JPG", "the original bytes, longer than the edit");
        var created = File.GetCreationTimeUtc(file);

        var staged = staging.Stage(file);
        File.Copy(staged, staged + "_original");
        File.WriteAllText(staged, "edited");

        var backup = staging.Commit(file);
        Assert.Equal("edited", File.ReadAllText(file));
        // Linux has no settable birth time: .NET reports min(ctime, mtime), which the write moves
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()) Assert.Equal(created, File.GetCreationTimeUtc(file));
        Assert.False(File.Exists(file + "_original"));
        Assert.Equal(Path.Combine(staging.BackupDirectory, DateTime.Now.ToString("yyyy-MM-dd"), "DSCF4127.JPG_original"), backup);
        Assert.Equal("the original bytes, longer than the edit", File.ReadAllText(backup!));

        // the saved copy is the file now, so the read after the save needs no new copy
        Assert.Equal(staged, staging.Stage(file));

        File.Copy(staged, staged + "_original");
        Assert.EndsWith("DSCF4127 (2).JPG_original", staging.Commit(file));
    }


    [Fact]
    public void CommitRefusesAFileThatChangedSinceTheCopy()
    {
        using var staging = NewStaging();
        var file = UserFile("a.jpg", "one");
        var staged = staging.Stage(file);
        File.WriteAllText(staged, "edited");
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(1));

        Assert.Throws<IOException>(() => staging.Commit(file));
        Assert.Equal("one", File.ReadAllText(file));

        // the edited copy is gone, so the next read copies the file again
        Assert.Equal("one", File.ReadAllText(staging.Stage(file)));
    }


    [Fact]
    public async Task ExifToolWritesTheCopyAndTheTagReachesTheUsersFile()
    {
        var service = exifTool.Require();
        var ct = TestContext.Current.CancellationToken;
        using var staging = NewStaging();
        var file = TestEnvironment.CopyToTemp(TestEnvironment.Fixture("a1111.jpg"), "my photo.jpg");

        var staged = await staging.StageAsync(file, ct);
        using (var plan = WritePlan.Build(staged, EditSet.Empty.Set("New:XMP:Title", "XMP:Title", "Staged title", string.Empty)))
        {
            Assert.Equal(SaveOutcome.Updated, (await service.WriteAsync(plan, ct)).Outcome);
        }

        var backup = staging.Commit(file);
        Assert.False(File.Exists(file + "_original"));
        Assert.True(File.Exists(backup));

        var doc = await service.ReadAsync(await staging.StageAsync(file, ct), file, [], null, ct);
        Assert.Equal(file, doc.SourcePath);
        Assert.Equal("Staged title", doc.Find("XMP", "Title")?.Value);
        Assert.Equal(Path.GetDirectoryName(file), doc.Find("File", "Directory")?.Value);

        var direct = await service.ReadAsync(file, [], null, ct);
        Assert.Equal("Staged title", direct.Find("XMP", "Title")?.Value);
    }

    #endregion // Tests


    #region Private methods

    private SandboxStaging NewStaging() => new(Path.Combine(_root, "stage"), Path.Combine(_root, "Backups"));


    private string UserFile(string name, string text)
    {
        var dir = Directory.CreateDirectory(Path.Combine(_root, "user")).FullName;
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, text);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-3));
        return path;
    }

    #endregion // Private methods


    #region IDisposable

    public void Dispose() => Directory.Delete(_root, true);

    #endregion // IDisposable
}
