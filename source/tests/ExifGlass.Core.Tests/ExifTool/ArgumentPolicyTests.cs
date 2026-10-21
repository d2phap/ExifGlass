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
using ExifGlass.Core.ExifTool;
using System.Text;
using Xunit;

namespace ExifGlass.Core.Tests.ExifTool;

public class ArgumentPolicyTests
{
    #region Tests

    [Fact]
    public void TheDefaultsSendOnlyExtractionFlags()
    {
        var a = ArgumentPolicy.Analyze(ArgumentPolicy.DEFAULT_ARGUMENTS);

        Assert.True(a.IsValid);
        Assert.Equal(["-fast", "-m", "-q", "-charset", "UTF8"], a.PassThrough);
        Assert.Equal(["-G", "-t", "-H"], a.Tokens.Where(t => t.Class == ArgumentClass.Locked).Select(t => t.Flag));
        Assert.Equal("-charset UTF8", a.Tokens.Single(t => t.Flag == "-charset").Text);
        Assert.False(a.ShowRawValues || a.ShowTagNames || a.ShowDuplicates);
        Assert.Empty(a.Warnings);
    }


    [Fact]
    public void DisplayOptionsBecomeTableToggles()
    {
        var a = ArgumentPolicy.Analyze("-n -s -a -S -s2 -G1 -G0:1:4 -j -l");

        Assert.True(a.ShowRawValues);
        Assert.True(a.ShowTagNames);
        Assert.True(a.ShowDuplicates);
        Assert.Empty(a.PassThrough);
        Assert.All(a.Tokens.Take(5), t => Assert.Equal(ArgumentClass.Display, t.Class));
        Assert.All(a.Tokens.Skip(5), t => Assert.Equal(ArgumentClass.Locked, t.Class));
    }


    [Theory]
    [InlineData("-o out.txt", "Writes or changes files")]
    [InlineData("-w txt", "Writes or changes files")]
    [InlineData("-tagsFromFile src.jpg", "Writes or changes files")]
    [InlineData("-TAGSFROMFILE src.jpg", "Writes or changes files")]
    [InlineData("-Make=Canon", "Writes or changes files")]
    [InlineData("-XMP:Title<=file.txt", "Writes or changes files")]
    [InlineData("-overwrite_original", "Writes or changes files")]
    [InlineData("-X", "Changes the output format the app reads")]
    [InlineData("-csv", "Changes the output format the app reads")]
    [InlineData("-b", "Changes the output format the app reads")]
    [InlineData("-p $Make", "Changes the output format the app reads")]
    [InlineData("-g1", "Changes the output format the app reads")]
    [InlineData("-v3", "Changes the output format the app reads")]
    [InlineData("-listx", "Changes the output format the app reads")]
    [InlineData("-D", "Changes the output format the app reads")]
    [InlineData("-config my.cfg", "Belongs to the app's own ExifTool session")]
    [InlineData("-@ args.txt", "Belongs to the app's own ExifTool session")]
    [InlineData("-stay_open True", "Belongs to the app's own ExifTool session")]
    [InlineData("-execute", "Belongs to the app's own ExifTool session")]
    [InlineData("-echo4 hi", "Belongs to the app's own ExifTool session")]
    [InlineData("-use MWG", "Belongs to the app's own ExifTool session")]
    [InlineData("-if $Make", "Runs Perl code")]
    [InlineData("photo.jpg", "A file name: the app passes the file itself")]
    [InlineData("-%weird", "Not supported in the argument field")]
    public void RefusesWhatWritesRedirectsOrBreaksTheProtocol(string text, string reason)
    {
        var a = ArgumentPolicy.Analyze(text);

        Assert.False(a.IsValid);
        Assert.Contains(a.Refused, t => t.Reason == reason);
        Assert.DoesNotContain(a.PassThrough, p => text.StartsWith(p, StringComparison.Ordinal) && p.Length > 1 && p != "-p");
    }


    [Fact]
    public void PassesValuesOfAllowedOptions()
    {
        var a = ArgumentPolicy.Analyze("-api LargeFileSupport=1 -lang de -d \"%Y-%m-%d %H:%M\" -c %.6f -sep \"; \" -listItem 0 -x Composite:all -ext jpg -ee3 -u");

        Assert.True(a.IsValid, string.Join(", ", a.Refused.Select(r => r.Text + ": " + r.Reason)));
        Assert.Equal(["-api", "LargeFileSupport=1", "-lang", "de", "-d", "%Y-%m-%d %H:%M", "-c", "%.6f", "-sep", "; ", "-listItem", "0", "-x", "Composite:all", "-ext", "jpg", "-ee3", "-u"], a.PassThrough);
    }


    [Fact]
    public void OptionalValuesStayOptional()
    {
        var a = ArgumentPolicy.Analyze("-lang -fast");

        Assert.True(a.IsValid);
        Assert.Equal(["-lang", "-fast"], a.PassThrough);
    }


    [Theory]
    [InlineData("-api Filter=s/a/b/", "The API option Filter isn't allowed here")]
    [InlineData("-api UserParam=x", "The API option UserParam isn't allowed here")]
    [InlineData("-charset filename=cp1252", "The app sets the file-name character set itself")]
    [InlineData("-d", "Needs a value")]
    public void RefusesUnsafeValues(string text, string reason)
    {
        var a = ArgumentPolicy.Analyze(text);

        Assert.Equal(reason, Assert.Single(a.Refused).Reason);
        Assert.Empty(a.PassThrough);
    }


    [Fact]
    public void RefusesAValueThatSpansLines()
    {
        var a = ArgumentPolicy.Analyze(["-lang", "de\n-w\ntxt"]);

        Assert.Equal("A value can't span lines", Assert.Single(a.Refused).Reason);
    }


    [Fact]
    public void LongOptionsMatchInAnyCaseSingleLettersExactly()
    {
        var a = ArgumentPolicy.Analyze("-FAST -Api LargeFileSupport -N");

        Assert.Equal(["-fast", "-api", "LargeFileSupport", "-N"], a.PassThrough.Select((p, i) => i == 0 ? p.ToLowerInvariant() : p));
        Assert.False(a.ShowRawValues);
    }


    [Fact]
    public void SelectsAndExcludesTagsAndWarnsAboutFast2()
    {
        var a = ArgumentPolicy.Analyze("-EXIF:all --MakerNotes:all -Make -fast2");

        Assert.Equal(["-EXIF:all", "--MakerNotes:all", "-Make", "-fast2"], a.PassThrough);
        Assert.Equal("Exclude tags", a.Tokens[1].Description);
        Assert.Contains("-fast2", Assert.Single(a.Warnings));
    }


    [Theory]
    [InlineData("", 0)]
    [InlineData("  ", 0)]
    [InlineData("-a -b", 2)]
    [InlineData("-d \"%Y %m\" -fast", 3)]
    [InlineData("\"\"", 1)]
    public void SplitsLikeAShell(string text, int count) => Assert.Equal(count, ArgumentPolicy.Split(text).Count);


    [Fact]
    public void DescribesFlags()
    {
        Assert.Equal("Skip scanning for trailers, for faster reads", ArgumentPolicy.Describe("-fast"));
        Assert.Equal("Raw numerical values, no print conversion", ArgumentPolicy.Describe("-n"));
        Assert.Equal(string.Empty, ArgumentPolicy.Describe("-zzz"));
    }


    [Theory]
    [InlineData("-Make", true)]
    [InlineData("-XMP-dc:Title=x", true)]
    [InlineData("C:\\path\\end\\", true)]
    [InlineData(" leading", false)]
    [InlineData("trailing ", false)]
    [InlineData("#hash", false)]
    [InlineData("a\nb", false)]
    [InlineData("", false)]
    [InlineData("-Title = x", false)]
    [InlineData("-Title= x", false)]
    public void KnowsWhichArgumentsAreSafeAsPlainLines(string arg, bool safe) => Assert.Equal(safe, ArgFile.IsPlainSafe(arg));


    [Fact]
    public void EncodesUnsafeArgumentsAsCStringsAndRefusesDollarsInThem()
    {
        var sb = new StringBuilder();
        ArgFile.AppendLine(sb, "-Make");
        ArgFile.AppendLine(sb, "-XMP-dc:Title=line1\nline2\t\"q\"\\");

        Assert.Equal("-Make\n#[CSTR]-XMP-dc:Title=line1\\nline2\\t\"q\"\\\\\n", sb.ToString());
        Assert.True(ArgFile.CanEncode("-Title=$5 off"));
        Assert.False(ArgFile.CanEncode("-Title=$5\noff"));
        Assert.Throws<ArgumentException>(() => ArgFile.AppendLine(sb, "-Title=@home\nx"));
    }

    #endregion // Tests
}
