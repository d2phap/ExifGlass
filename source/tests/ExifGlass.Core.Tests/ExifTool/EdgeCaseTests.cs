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
using ExifGlass.Core.ExifTool;
using ExifGlass.Core.Metadata;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Xunit;

namespace ExifGlass.Core.Tests.ExifTool;

/// <summary>
/// P12's edge cases against the real ExifTool: empty, unknown, cut-short and locked files, UNC paths, a folder that refuses writes, and a file that changes while edits wait.
/// </summary>
public class EdgeCaseTests(ExifToolFixture exifTool)
{
    #region Tests

    [Fact]
    public async Task AnEmptyFileReadsAsAnErrorWithItsFileTags()
    {
        var service = exifTool.Require();
        var path = Temp("empty.jpg", []);
        try
        {
            var doc = await service.ReadAsync(path, [], null, TestContext.Current.CancellationToken);

            Assert.Equal("File is empty", doc.Error);
            Assert.NotNull(doc.Find("File", "FileSize"));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }


    [Fact]
    public async Task AFileExifToolDoesntKnowReadsAsAnError()
    {
        var service = exifTool.Require();
        var path = Temp("noise.bin", [.. Enumerable.Range(0, 4096).Select(i => (byte)(i * 37))]);
        try
        {
            var doc = await service.ReadAsync(path, [], null, TestContext.Current.CancellationToken);

            Assert.Equal("Unknown file type", doc.Error);
            Assert.NotNull(doc.Find("File", "FileName"));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }


    [Fact]
    public async Task ACutShortJpegKeepsWhatWasReadAndSaysWhy()
    {
        var service = exifTool.Require();
        var whole = await File.ReadAllBytesAsync(TestEnvironment.Fixture("negative_camera.jpg"), TestContext.Current.CancellationToken);
        var path = Temp("cut.jpg", whole[..^200]);
        try
        {
            var doc = await service.ReadAsync(path, [], null, TestContext.Current.CancellationToken);

            // the image data is cut, the EXIF block before it survives, and ExifTool warns
            Assert.Null(doc.Error);
            Assert.Equal("Canon", doc.Find("EXIF", "Make")!.Value);
            Assert.Equal(["JPEG format error"], doc.Warnings);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }


    [Fact]
    public async Task AFileAnotherAppLocksSaysSoNotThatItIsMissing()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Only Windows locks a file against readers");

        var service = exifTool.Require();
        var path = TestEnvironment.CopyToTemp(TestEnvironment.Fixture("negative_camera.jpg"));
        try
        {
            MetadataDocument doc;
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                doc = await service.ReadAsync(path, [], null, TestContext.Current.CancellationToken);
            }

            Assert.Equal(ExifToolService.FILE_NOT_OPENED, doc.Error);
            Assert.Equal("Canon", (await service.ReadAsync(path, [], null, TestContext.Current.CancellationToken)).Find("EXIF", "Make")!.Value);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }


    [Fact]
    public async Task ReadsAndWritesThroughAUncPath()
    {
        var service = exifTool.Require();
        var path = TestEnvironment.CopyToTemp(TestEnvironment.Fixture("negative_camera.jpg"), "ảnh unc.jpg");
        try
        {
            // the drive's administrative share, as a network path to this machine
            var root = Path.GetPathRoot(path)!;
            var unc = OperatingSystem.IsWindows() ? $@"\\localhost\{root[0]}${path[(root.Length - 1)..]}" : null;
            if (unc is null || !File.Exists(unc)) Assert.Skip($"{unc ?? "UNC paths"} isn't reachable here");

            var ct = TestContext.Current.CancellationToken;
            Assert.Equal("Canon", (await service.ReadAsync(unc, [], null, ct)).Find("EXIF", "Make")!.Value);

            using var plan = WritePlan.Build(unc, EditSet.Empty.Set("EXIF:IFD0:Make", "IFD0:Make", "Nikon", "Canon"));
            var result = await service.WriteAsync(plan, ct);

            Assert.Equal(SaveOutcome.Updated, result.Outcome);
            Assert.Equal("Nikon", (await service.ReadAsync(path, [], null, ct)).Find("EXIF", "Make")!.Value);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }


    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task ASaveIntoAFolderThatRefusesWritesFailsAndLeavesTheFile()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("The folder's write is denied with a Windows ACL");

        var service = exifTool.Require();
        var path = TestEnvironment.CopyToTemp(TestEnvironment.Fixture("negative_camera.jpg"));
        var dir = new DirectoryInfo(Path.GetDirectoryName(path)!);
        var before = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        var deny = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.CreateFiles | FileSystemRights.WriteData | FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles, AccessControlType.Deny);
        var acl = dir.GetAccessControl();
        acl.AddAccessRule(deny);
        dir.SetAccessControl(acl);
        try
        {
            using var plan = WritePlan.Build(path, EditSet.Empty.Set("EXIF:IFD0:Make", "IFD0:Make", "Nikon", "Canon"));
            var result = await service.WriteAsync(plan, TestContext.Current.CancellationToken);

            Assert.Equal(SaveOutcome.Failed, result.Outcome);
            Assert.NotEmpty(result.Errors);
            Assert.Equal(before, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
            Assert.False(File.Exists(path + "_original"));
        }
        finally
        {
            acl.RemoveAccessRule(deny);
            dir.SetAccessControl(acl);
            dir.Delete(true);
        }
    }


    [Fact]
    public async Task EditsSavedAfterTheFileChangedOnDiskKeepTheOtherChange()
    {
        var service = exifTool.Require();
        var path = TestEnvironment.CopyToTemp(TestEnvironment.Fixture("negative_camera.jpg"));
        var ct = TestContext.Current.CancellationToken;
        try
        {
            var doc = await service.ReadAsync(path, [], null, ct);
            var make = doc.Find("EXIF", "Make")!;
            var edits = EditSet.Empty.Set(make.Key, make.WriteName, "Nikon", make.Value);

            // another app writes a tag while the edit waits
            using (var other = WritePlan.Build(path, EditSet.Empty.Add(new AddedTag("XMP", "Title", "written elsewhere"))))
            {
                Assert.Equal(SaveOutcome.Updated, (await service.WriteAsync(other, ct)).Outcome);
            }

            File.Delete(path + "_original");
            using var plan = WritePlan.Build(path, edits);
            var result = await service.WriteAsync(plan, ct);

            // ExifTool applies the waiting edit to the file as it is now: the other app's tag stays, the backup is the changed file
            Assert.Equal(SaveOutcome.Updated, result.Outcome);
            var after = await service.ReadAsync(path, [], null, ct);
            Assert.Equal("Nikon", after.Find("EXIF", "Make")!.Value);
            Assert.Equal("written elsewhere", after.Find("XMP", "Title")!.Value);
            Assert.Equal("written elsewhere", (await service.ReadAsync(path + "_original", [], null, ct)).Find("XMP", "Title")!.Value);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    #endregion // Tests


    #region Private methods

    private static string Temp(string name, byte[] bytes)
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("exifglass-edge-").FullName, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    #endregion // Private methods
}
