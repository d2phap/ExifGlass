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

public class EditingTests
{
    #region Tests

    [Fact]
    public void StagesEditsDeletesAndBulkActionsImmutably()
    {
        var a = EditSet.Empty;
        var b = a.Set("EXIF:IFD0:Make", "IFD0:Make", "Fujifilm", "FUJIFILM")
            .Delete("EXIF:IFD0:Model", "IFD0:Model", "X-T5")
            .Add(new AddedTag("XMP", "Title", "t"))
            .WithRemoveGps(true)
            .WithStripAll(true)
            .WithRemoveAi(true);

        Assert.True(a.IsEmpty);
        Assert.Equal(6, b.PendingCount);
        Assert.Equal(EditState.Edited, b.StateOf("EXIF:IFD0:Make"));
        Assert.Equal(EditState.Deleted, b.StateOf("EXIF:IFD0:Model"));
        Assert.Equal(EditState.None, b.StateOf("EXIF:IFD0:Other"));
        Assert.True(b.Edits["EXIF:IFD0:Model"].IsDelete);

        // undo a delete, revert an edit, restage the same value, remove an added tag
        var c = b.Revert("EXIF:IFD0:Model").Set("EXIF:IFD0:Make", "IFD0:Make", "FUJIFILM", "FUJIFILM").RemoveAdded("xmp:title")
            .WithRemoveGps(false).WithStripAll(false).WithRemoveAi(false);
        Assert.True(c.IsEmpty);
        Assert.Same(c, c.Revert("nope"));

        // adding the same tag again replaces it
        var d = a.Add(new AddedTag("XMP", "Title", "1")).Add(new AddedTag("xmp", "title", "2"));
        Assert.Equal("2", Assert.Single(d.Added).Value);

        // a raw edit of the same text is still an edit
        Assert.Single(a.Set("k", "G:N", "4", "4", isRaw: true).Edits);
    }


    [Fact]
    public void BuildsWriteArgumentsInOrder()
    {
        var edits = EditSet.Empty
            .WithStripAll(true).WithRemoveGps(true).WithRemoveAi(true)
            .Set("EXIF:IFD0:Make", "IFD0:Make", "Fujifilm", "FUJIFILM")
            .Set("EXIF:ExifIFD:FNumber", "ExifIFD:FNumber", "2.8", "4", isRaw: true)
            .Delete("EXIF:IFD0:Model", "IFD0:Model", "X")
            .Add(new AddedTag("XMP", "Title", "Lanterns"));
        var redaction = new AiRedactionPlan(["-PNG:Parameters="], ["PNG:Parameters"], ["something stays"]);

        using var plan = WritePlan.Build("C:/photos/DSCF4127.JPG", edits, redaction);

        Assert.Equal(["-all=", "-gps:all=", "-PNG:Parameters=", "-ExifIFD:FNumber#=2.8", "-IFD0:Make=Fujifilm", "-IFD0:Model=", "-XMP:Title=Lanterns"], plan.Arguments);
        Assert.Equal(["something stays"], plan.Warnings);
        Assert.Equal("DSCF4127.JPG_original", plan.BackupName);
        Assert.False(plan.IsEmpty);
        Assert.True(WritePlan.Build("a.jpg", EditSet.Empty).IsEmpty);
    }


    [Fact]
    public void WritesIptcAsUtf8AndAwkwardValuesFromFiles()
    {
        var dir = Directory.CreateTempSubdirectory("exifglass-plan-").FullName;
        try
        {
            var edits = EditSet.Empty
                .Add(new AddedTag("IPTC", "Keywords", "từ khóa"))
                .Set("XMP:XMP-dc:Description", "XMP-dc:Description", "two\nlines with $5", "x")
                .Set("XMP:XMP-dc:Title", "XMP-dc:Title", " padded ", "x")
                .Set("XMP:XMP-dc:Rights", "XMP-dc:Rights", "line\nbreak", "x");

            string file;
            using (var plan = WritePlan.Build("a.jpg", edits, null, dir))
            {
                Assert.Equal("-codedcharacterset=utf8", plan.Arguments[^1]);
                Assert.Contains("-IPTC:Keywords=từ khóa", plan.Arguments);
                Assert.Contains("-XMP-dc:Rights=line\nbreak", plan.Arguments);

                file = plan.Arguments.Single(a => a.StartsWith("-XMP-dc:Description<=", StringComparison.Ordinal))["-XMP-dc:Description<=".Length..];
                Assert.Equal("two\nlines with $5", File.ReadAllText(file));
                Assert.Contains(plan.Arguments, a => a.StartsWith("-XMP-dc:Title<=", StringComparison.Ordinal));
            }

            Assert.False(File.Exists(file));
        }
        finally
        {
            Directory.Delete(dir, true);
        }

        using var iptcEdit = WritePlan.Build("a.jpg", EditSet.Empty.Set("IPTC:Keywords", "IPTC:Keywords", "x", "y"));
        Assert.Equal("-codedcharacterset=utf8", iptcEdit.Arguments[^1]);
    }


    [Fact]
    public void MakesCommandBarTokens()
    {
        using var plan = WritePlan.Build("C:/My Photos/a.jpg", EditSet.Empty.Set("k", "IFD0:Make", "Fuji film", "x").WithRemoveGps(true));

        var tokens = plan.Tokens("exiftool");

        Assert.Equal(["exiftool", "-gps:all=", "\"-IFD0:Make=Fuji film\"", "\"C:/My Photos/a.jpg\""], tokens.Select(t => t.Text));
        Assert.Equal([CommandTokenKind.Executable, CommandTokenKind.Write, CommandTokenKind.Write, CommandTokenKind.Path], tokens.Select(t => t.Kind));
        Assert.Equal("exiftool -gps:all= \"-IFD0:Make=Fuji film\" \"C:/My Photos/a.jpg\"", WritePlan.ToCommandLine(tokens));

        var read = WritePlan.ReadTokens("C:/Program Files/exiftool.exe", "C:/app/exifglass.config", ["-fast", "-charset", "UTF8"], null);
        Assert.Equal("\"C:/Program Files/exiftool.exe\" -config C:/app/exifglass.config -fast -charset UTF8 <file>", WritePlan.ToCommandLine(read));
        Assert.Equal(CommandTokenKind.Value, read[5].Kind);
        Assert.Equal("exiftool \"\" a.jpg", WritePlan.ToCommandLine(WritePlan.ReadTokens("exiftool", null, [""], "a.jpg")));
    }


    [Fact]
    public void ReadsExifToolsWriteSummary()
    {
        var path = Path.Combine(Path.GetTempPath(), "exifglass-save-" + Guid.NewGuid().ToString("N") + ".jpg");
        File.WriteAllText(path + "_original", "x");
        try
        {
            var ok = SaveResult.Parse("    1 image files updated\nWarning: [minor] Fixed something - " + path + "\n", "Warning: IPTCDigest is not current\n", 0, path);
            Assert.Equal(SaveOutcome.Updated, ok.Outcome);
            Assert.Equal(path + "_original", ok.BackupPath);
            Assert.Equal(["IPTCDigest is not current", "[minor] Fixed something"], ok.Warnings.Order(StringComparer.Ordinal));

            var same = SaveResult.Parse("    0 image files updated\n    1 image files unchanged\n", string.Empty, 0, path);
            Assert.Equal(SaveOutcome.Unchanged, same.Outcome);
            Assert.Null(same.BackupPath);

            var failed = SaveResult.Parse("    0 image files updated\n    1 files weren't updated due to errors\n", "Error: File not found - " + path.Replace('\\', '/') + "\n", 1, path);
            Assert.Equal(SaveOutcome.Failed, failed.Outcome);
            Assert.Equal(["File not found"], failed.Errors);

            Assert.Equal(["ExifTool stopped with status 2"], SaveResult.Parse(string.Empty, string.Empty, 2, path).Errors);
            Assert.Equal(["ExifTool didn't update the file"], SaveResult.Parse(string.Empty, string.Empty, null, path).Errors);
        }
        finally
        {
            File.Delete(path + "_original");
        }
    }

    #endregion // Tests
}
