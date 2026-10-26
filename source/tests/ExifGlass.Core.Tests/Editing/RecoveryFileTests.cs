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
using Xunit;

namespace ExifGlass.Core.Tests.Editing;

/// <summary>
/// D29's recovery file: every kind of staged edit survives the round trip; a tag the file lost is dropped.
/// </summary>
public class RecoveryFileTests : IDisposable
{
    #region Fields and constants

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "eg-recovery-" + Guid.NewGuid().ToString("N"));

    #endregion // Fields and constants


    #region Tests

    [Fact]
    public void KeepsEveryStagedEditThroughTheFile()
    {
        var plan = new AiRedactionPlan(["-PNG:Parameters="], ["PNG:Parameters"], ["A note"]);
        var edits = EditSet.Empty
            .Set("EXIF:IFD0:Artist", "EXIF:Artist", "Phap", "Someone")
            .Set("EXIF:ExifIFD:FNumber", "EXIF:FNumber", "2.8", "2.8", isRaw: true)
            .Delete("XMP:XMP-dc:Title", "XMP:Title", "Old title")
            .Add(new AddedTag("IPTC", "Keywords", "cat; dog"))
            .WithRemoveGps(true)
            .WithStripAll(true)
            .WithRemoveAi(plan);
        var modified = new DateTimeOffset(2026, 10, 26, 9, 30, 0, TimeSpan.Zero);

        RecoveryFile.Save(_dir, RecoveryFile.Create(@"D:\photos\Ảnh 1.jpg", 1234, modified, edits, modified.AddMinutes(5)));
        var loaded = RecoveryFile.Load(_dir);

        Assert.NotNull(loaded);
        Assert.Equal(@"D:\photos\Ảnh 1.jpg", loaded.SourcePath);
        Assert.Equal(7, loaded.PendingCount);
        Assert.False(loaded.FileChanged(1234, modified));
        Assert.True(loaded.FileChanged(1235, modified));
        Assert.True(loaded.FileChanged(1234, modified.AddSeconds(1)));

        var restored = loaded.ToEditSet(_ => true, out var dropped);
        Assert.Equal(0, dropped);
        Assert.Equal(edits.PendingCount, restored.PendingCount);
        Assert.Equal(EditState.Edited, restored.StateOf("EXIF:IFD0:Artist"));
        Assert.True(restored.Edits["EXIF:ExifIFD:FNumber"].IsRaw);
        Assert.Equal(EditState.Deleted, restored.StateOf("XMP:XMP-dc:Title"));
        Assert.Equal("cat; dog", Assert.Single(restored.Added).Value);
        Assert.True(restored.RemoveGps && restored.StripAll);
        Assert.Equal(plan.Arguments, restored.AiRedaction!.Arguments);
        Assert.Equal(plan.Warnings, restored.AiRedaction.Warnings);
    }


    [Fact]
    public void DropsTheEditsOfTagsTheFileNoLongerHas()
    {
        var edits = EditSet.Empty
            .Set("EXIF:IFD0:Artist", "EXIF:Artist", "Phap", "Someone")
            .Delete("XMP:XMP-dc:Title", "XMP:Title", "Old title");

        var restored = RecoveryFile.Create("a.jpg", 1, null, edits, DateTimeOffset.UtcNow).ToEditSet(k => k == "EXIF:IFD0:Artist", out var dropped);

        Assert.Equal(1, dropped);
        Assert.Equal(1, restored.PendingCount);
        Assert.Equal(EditState.Edited, restored.StateOf("EXIF:IFD0:Artist"));
    }


    [Fact]
    public void ReadsNothingFromAMissingOrBrokenFile()
    {
        Assert.Null(RecoveryFile.Load(_dir));

        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, RecoveryFile.FILE_NAME), "{ not json");
        Assert.Null(RecoveryFile.Load(_dir));

        RecoveryFile.Delete(_dir);
        Assert.False(File.Exists(Path.Combine(_dir, RecoveryFile.FILE_NAME)));
    }

    #endregion // Tests


    #region Public methods

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
            // nothing written
        }

        GC.SuppressFinalize(this);
    }

    #endregion // Public methods
}
