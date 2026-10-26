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
using ExifGlass.Core.Query;
using ExifGlass.Core.Tests.Metadata;
using System.Diagnostics;
using System.Text;
using Xunit;

namespace ExifGlass.Core.Tests.Query;

public class QueryEngineTests
{
    #region Tests

    [Fact]
    public void ListsGroupsAndRowsInFileOrder()
    {
        var r = QueryEngine.Run(Camera(), ViewQuery.Default, TestContext.Current.CancellationToken);

        Assert.Equal(["ExifTool", "File", "EXIF", "MakerNotes", "Composite"], r.Groups.Select(g => g.Name));
        Assert.IsType<GroupHeaderItem>(r.Items[0]);
        Assert.Equal(25, r.Total);
        Assert.Equal(5, r.GroupCount);
        Assert.Equal("25 tags", r.Summary);
        Assert.False(r.IsFiltering);
        Assert.Equal(25, r.RowsInView.Count);
        Assert.Equal(Enumerable.Range(1, 25), r.RowsInView.Select(x => x.Index));
        Assert.Equal("15", r.Groups[2].CountLabel(false));
    }


    [Fact]
    public void SearchesNameValueTagNameAndIdWithEveryMatch()
    {
        var r = QueryEngine.Run(Camera(), ViewQuery.Default with { Search = "  f  " }, TestContext.Current.CancellationToken);

        Assert.True(r.IsFiltering);
        var fnumber = r.RowsInView.Single(x => x.TagName == "FNumber");
        Assert.Equal([new TextRange(0, 1)], fnumber.NameMatches);

        var byValue = QueryEngine.Run(Camera(), ViewQuery.Default with { Search = "classic" }, TestContext.Current.CancellationToken);
        Assert.Equal("FilmMode", Assert.Single(byValue.RowsInView).TagName);
        Assert.Equal([new TextRange(0, 7)], byValue.RowsInView[0].ValueMatches);
        Assert.Equal("1 of 25 tags", byValue.Summary);
        Assert.Equal("1 of 2", byValue.Groups.Single(g => g.Name == "MakerNotes").CountLabel(true));

        var byId = QueryEngine.Run(Camera(), ViewQuery.Default with { Search = "0x829D" }, TestContext.Current.CancellationToken);
        Assert.Equal("FNumber", Assert.Single(byId.RowsInView).TagName);

        // matches the tag name too, when the shown description differs
        var byTagName = QueryEngine.Run(Camera(), ViewQuery.Default with { Search = "LensModel" }, TestContext.Current.CancellationToken);
        Assert.Single(byTagName.RowsInView);
        Assert.Empty(byTagName.RowsInView[0].NameMatches);
    }


    [Fact]
    public void FindsAllNonOverlappingMatches()
    {
        Assert.Equal([new TextRange(0, 2), new TextRange(2, 2)], QueryEngine.FindAll("aaaa", "AA"));
        Assert.Equal([new TextRange(3, 3)], QueryEngine.FindAll("ab cAt", "cat"));
        Assert.Empty(QueryEngine.FindAll("ab", "abc"));
        Assert.Empty(QueryEngine.FindAll("ab", string.Empty));
    }


    [Fact]
    public void QuickFiltersCombineWithAndAndCountOverTheDocument()
    {
        var doc = Camera();
        var r = QueryEngine.Run(doc, ViewQuery.Default, TestContext.Current.CancellationToken);

        Assert.Equal(3, r.FilterCounts[QuickFilter.DateTime]);
        Assert.Equal(2, r.FilterCounts[QuickFilter.Gps]);
        Assert.Equal(10, r.FilterCounts[QuickFilter.Converted]);
        Assert.Equal(17, r.FilterCounts[QuickFilter.HasTagId]);
        Assert.Equal(0, r.FilterCounts[QuickFilter.Ai]);

        var both = QueryEngine.Run(doc, ViewQuery.Default with { Filters = new HashSet<QuickFilter> { QuickFilter.Gps, QuickFilter.Converted } }, TestContext.Current.CancellationToken);
        Assert.Equal(["GPSAltitude", "GPSPosition"], both.RowsInView.Select(x => x.TagName));

        // with chips on, each chip counts within them; the document's counts stay for the Edit menu
        Assert.Equal(r.FilterCounts, r.ChipCounts);
        var gps = QueryEngine.Run(doc, ViewQuery.Default with { Filters = new HashSet<QuickFilter> { QuickFilter.Gps } }, TestContext.Current.CancellationToken);
        Assert.Equal(2, gps.ChipCounts[QuickFilter.Gps]);
        Assert.Equal(2, gps.ChipCounts[QuickFilter.Converted]);
        Assert.Equal(0, gps.ChipCounts[QuickFilter.DateTime]);
        Assert.Equal(10, gps.FilterCounts[QuickFilter.Converted]);
    }


    [Fact]
    public void QuickFiltersCountWithinTheShownGroups()
    {
        var doc = Camera();
        var all = QueryEngine.Run(doc, ViewQuery.Default, TestContext.Current.CancellationToken);

        var noExif = QueryEngine.Run(doc, ViewQuery.Default with { HiddenGroups = new HashSet<string> { "EXIF" } }, TestContext.Current.CancellationToken);
        Assert.Equal(1, noExif.ChipCounts[QuickFilter.Gps]);
        Assert.Equal(all.FilterCounts, noExif.FilterCounts);

        var gpsWithoutComposite = QueryEngine.Run(doc, ViewQuery.Default with
        {
            HiddenGroups = new HashSet<string> { "Composite" },
            Filters = new HashSet<QuickFilter> { QuickFilter.Gps },
        }, TestContext.Current.CancellationToken);
        Assert.Equal(1, gpsWithoutComposite.ChipCounts[QuickFilter.Gps]);
        Assert.Equal(1, gpsWithoutComposite.ChipCounts[QuickFilter.Converted]);

        // the hidden groups are kept for every file, so this one may lack some of them
        var unknown = QueryEngine.Run(doc, ViewQuery.Default with { HiddenGroups = new HashSet<string> { "XMP" } }, TestContext.Current.CancellationToken);
        Assert.Equal(all.FilterCounts, unknown.ChipCounts);
    }


    [Fact]
    public void HidesAndCollapsesGroups()
    {
        var query = ViewQuery.Default with
        {
            HiddenGroups = new HashSet<string> { "File" },
            CollapsedGroups = new HashSet<string> { "EXIF" },
        };

        var r = QueryEngine.Run(Camera(), query, TestContext.Current.CancellationToken);

        Assert.True(r.IsFiltering);
        Assert.True(r.Groups.Single(g => g.Name == "File").IsHidden);
        Assert.DoesNotContain(r.Items.OfType<GroupHeaderItem>(), h => h.Group.Name == "File");

        var exif = r.Items.OfType<GroupHeaderItem>().Single(h => h.Group.Name == "EXIF");
        Assert.True(exif.Group.IsCollapsed);
        Assert.DoesNotContain(r.Items.OfType<RowItem>(), x => x.Row.Group == "EXIF");

        // export takes rows in view, collapsed groups included
        Assert.Contains(r.RowsInView, x => x.Group == "EXIF");
        Assert.DoesNotContain(r.RowsInView, x => x.Group == "File");
    }


    [Fact]
    public void SortsWithinGroupsAndKeepsFileOrderForTies()
    {
        var byValue = QueryEngine.Run(Camera(), ViewQuery.Default with { SortColumn = SortColumn.Value, SortDirection = SortDirection.Ascending }, TestContext.Current.CancellationToken);
        var exif = byValue.RowsInView.Where(x => x.Group == "EXIF").Select(x => x.TagName).ToList();

        // numbers by raw value (-1/3 is -0.33, Aperture-priority AE is 3), then text in natural order
        Assert.Equal(["ExposureCompensation", "ExposureTime", "MaxApertureValue", "ExposureProgram", "FNumber", "GPSAltitude", "FocalLength", "FocalLengthIn35mmFormat", "ISO"], exif.Take(9));
        Assert.Equal(["OffsetTimeOriginal", "DateTimeOriginal", "Software", "Make", "Model", "LensModel"], exif.Skip(9));
        Assert.Equal("ExifTool", byValue.Groups[0].Name);

        var byNameDesc = QueryEngine.Run(Camera(), ViewQuery.Default with { SortColumn = SortColumn.TagName, SortDirection = SortDirection.Descending }, TestContext.Current.CancellationToken);
        Assert.Equal("Software", byNameDesc.RowsInView.First(x => x.Group == "EXIF").TagName);

        var byIndex = QueryEngine.Run(Camera(), ViewQuery.Default with { SortColumn = SortColumn.Index, SortDirection = SortDirection.Descending }, TestContext.Current.CancellationToken);
        Assert.Equal([25, 24], byIndex.RowsInView.Where(x => x.Group == "Composite").Select(x => x.Index));
        Assert.Equal(1, byIndex.RowsInView.First().Index);

        var byId = QueryEngine.Run(Camera(), ViewQuery.Default with { SortColumn = SortColumn.TagId, SortDirection = SortDirection.Ascending }, TestContext.Current.CancellationToken);
        Assert.Equal("0x0006", byId.RowsInView.First(x => x.Group == "EXIF").DisplayId);

        var none = QueryEngine.Run(Camera(), ViewQuery.Default with { SortColumn = SortColumn.Value, SortDirection = SortDirection.None }, TestContext.Current.CancellationToken);
        Assert.Equal(Enumerable.Range(1, 25), none.RowsInView.Select(x => x.Index));
    }


    [Fact]
    public void AppliesTheDisplayOptions()
    {
        var doc = MetadataDocumentTests.Doc("""[{"EXIF:IFD0:Make": {"desc": "Make", "val": "Canon"}, "MakerNotes:Canon:Copy1:Make": {"desc": "Make", "val": "Canon 2"}, "EXIF:ExifIFD:ExposureTime": {"desc": "Exposure Time", "num": 0.004, "val": "1/250"}}]""");

        var plain = QueryEngine.Run(doc, ViewQuery.Default, TestContext.Current.CancellationToken);
        Assert.Equal(2, plain.Total);
        Assert.Equal("1/250", plain.RowsInView[1].Value);
        Assert.Equal("Exposure Time", plain.RowsInView[1].Name);

        var all = QueryEngine.Run(doc, ViewQuery.Default with { ShowDuplicates = true, ShowRawValues = true, ShowTagNames = true }, TestContext.Current.CancellationToken);
        Assert.Equal(3, all.Total);
        Assert.Equal("0.004", all.RowsInView.Single(x => x.TagName == "ExposureTime").Value);
        Assert.Equal("ExposureTime", all.RowsInView.Single(x => x.TagName == "ExposureTime").Name);
    }


    [Fact]
    public void ShowsStagedEditsDeletesAndNewTags()
    {
        var doc = Camera();
        var make = doc.Find("EXIF", "Make")!;
        var model = doc.Find("EXIF", "Model")!;
        var edits = EditSet.Empty
            .Set(make.Key, make.WriteName, "Fujifilm", make.Value)
            .Delete(model.Key, model.WriteName, model.Value)
            .Add(new AddedTag("XMP", "Title", "Lanterns"))
            .Add(new AddedTag("EXIF", "Artist", "Me"));

        var r = QueryEngine.Run(doc, ViewQuery.Default with { Edits = edits, Search = "fuji" }, TestContext.Current.CancellationToken);

        var edited = r.RowsInView.Single(x => x.Key == make.Key);
        Assert.Equal((EditState.Edited, "Fujifilm", "FUJIFILM"), (edited.EditState, edited.Value, edited.OriginalValue));

        var all = QueryEngine.Run(doc, ViewQuery.Default with { Edits = edits }, TestContext.Current.CancellationToken);
        Assert.Equal(EditState.Deleted, all.RowsInView.Single(x => x.Key == model.Key).EditState);
        Assert.Equal("X-T5", all.RowsInView.Single(x => x.Key == model.Key).Value);
        Assert.Equal(EditState.Added, all.RowsInView.Single(x => x.Key == "New:XMP:Title").EditState);
        Assert.Equal("XMP", all.Groups.Last().Name);
        Assert.Contains(all.RowsInView, x => x.Key == "New:EXIF:Artist" && x.Group == "EXIF");
    }


    [Fact]
    public void PutsTheAiGroupFirstAndFiltersAiRowsAndSources()
    {
        var doc = MetadataDocumentTests.Doc("""[{"PNG:Parameters": {"desc": "Parameters", "id": "parameters", "val": "a cat\nSteps: 20, Sampler: Euler, CFG scale: 7, Seed: 5"}, "PNG:ImageWidth": {"val": 16}}]""");
        using var tags = AiTagSet.FromPairs([("PNG:Main:Parameters", doc.Tags[0].Value)]);
        var ai = doc.WithAi(AiEngine.Parse(tags, null, TestContext.Current.CancellationToken));

        var r = QueryEngine.Run(ai, ViewQuery.Default, TestContext.Current.CancellationToken);

        Assert.Equal(GroupInfo.AI_GROUP, r.Groups[0].Name);
        Assert.Equal("Parsed by ExifGlass", r.Groups[0].Description);
        Assert.Equal("AI:generator", r.RowsInView[0].Key);
        Assert.True(r.RowsInView[0].IsAi);
        Assert.Equal(0, r.RowsInView[0].Index);

        var onlyAi = QueryEngine.Run(ai, ViewQuery.Default with { Filters = new HashSet<QuickFilter> { QuickFilter.Ai } }, TestContext.Current.CancellationToken);
        Assert.Contains(onlyAi.RowsInView, x => x.Key == "PNG:Parameters");
        Assert.DoesNotContain(onlyAi.RowsInView, x => x.Key == "PNG:ImageWidth");
        Assert.Equal(onlyAi.RowsInView.Count, r.FilterCounts[QuickFilter.Ai]);

        var seed = QueryEngine.Run(ai, ViewQuery.Default with { Search = "seed", ShowTagNames = true }, TestContext.Current.CancellationToken);
        Assert.Equal("PNG:Parameters", seed.RowsInView.Single(x => x.IsAi).SourceKey);
    }


    [Fact]
    public void CountsAiFieldsApartFromTags()
    {
        var ai = AiDocument();

        var r = QueryEngine.Run(ai, ViewQuery.Default, TestContext.Current.CancellationToken);
        Assert.Equal(2, r.Total);
        Assert.Equal("2 tags", r.Summary);
        Assert.Equal(r.RowsInView.Count(x => x.IsAi), r.AiTotal);
        Assert.Equal($"{r.AiTotal} AI fields", r.AiSummary);
        Assert.Equal("PNG:Parameters", r.Groups[0].SourceKey);
        Assert.Null(r.Groups[1].SourceKey);

        var cat = QueryEngine.Run(ai, ViewQuery.Default with { Search = "cat" }, TestContext.Current.CancellationToken);
        Assert.Equal("1 of 2 tags", cat.Summary);
        Assert.Equal($"1 of {r.AiTotal} AI fields", cat.AiSummary);

        Assert.Equal(string.Empty, QueryEngine.Run(Camera(), ViewQuery.Default, TestContext.Current.CancellationToken).AiSummary);
    }


    [Fact]
    public void FiltersAiPromptsModelsAndProvenance()
    {
        var ai = AiDocument();

        var prompts = QueryEngine.Run(ai, ViewQuery.Default with { Filters = new HashSet<QuickFilter> { QuickFilter.AiPrompts } }, TestContext.Current.CancellationToken);
        Assert.Equal(["a cat <lora:ink:0.5>", "ugly"], prompts.RowsInView.Select(x => x.Value));
        Assert.Equal(2, prompts.FilterCounts[QuickFilter.AiPrompts]);

        var models = QueryEngine.Run(ai, ViewQuery.Default with { Filters = new HashSet<QuickFilter> { QuickFilter.AiModels } }, TestContext.Current.CancellationToken);
        Assert.Equal(["LoRA"], models.RowsInView.Select(x => x.Name));

        var provenance = QueryEngine.Run(ai, ViewQuery.Default with { Filters = new HashSet<QuickFilter> { QuickFilter.AiProvenance } }, TestContext.Current.CancellationToken);
        Assert.Equal(["Source type", "Digital Source Type"], provenance.RowsInView.Select(x => x.Name));
        Assert.True(provenance.RowsInView[1].Tag!.IsAiProvenance);
        Assert.False(ai.GetByKey("PNG:Parameters")!.IsAiProvenance);
    }


    [Fact]
    public void ReturnsAnEmptyViewWithoutADocument()
    {
        var r = QueryEngine.Run(null, ViewQuery.Default, TestContext.Current.CancellationToken);

        Assert.Same(ViewResult.Empty, r);
        Assert.Equal("0 tags", r.Summary);
    }


    [Fact]
    public void HandlesAThousandTagsWithinTheBudget()
    {
        var sb = new StringBuilder("[{");
        for (var i = 0; i < 1000; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append($"\"Group{i % 12}:Sub:Tag{i}\": {{\"desc\": \"Tag number {i}\", \"id\": \"0x{i:x4}\", \"num\": {i}, \"val\": \"{i} units of something {i % 7}\"}}");
        }

        var doc = MetadataDocumentTests.Doc(sb.Append("}]").ToString());
        var query = ViewQuery.Default with { Search = "units", Filters = new HashSet<QuickFilter> { QuickFilter.HasTagId }, SortColumn = SortColumn.Value, SortDirection = SortDirection.Descending };

        QueryEngine.Run(doc, query, TestContext.Current.CancellationToken);
        var watch = Stopwatch.StartNew();
        const int RUNS = 20;
        for (var i = 0; i < RUNS; i++) QueryEngine.Run(doc, query, TestContext.Current.CancellationToken);
        var perRun = watch.Elapsed.TotalMilliseconds / RUNS;

        Assert.True(perRun <= 8, $"{perRun:0.00} ms per run (budget 8 ms, §8)");
    }


    [Fact]
    public void StopsWhenCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var sb = new StringBuilder("[{");
        for (var i = 0; i < 2000; i++) sb.Append(i == 0 ? "" : ",").Append($"\"G:T{i}\": {{\"val\": {i}}}");
        var doc = MetadataDocumentTests.Doc(sb.Append("}]").ToString());

        Assert.Throws<OperationCanceledException>(() => QueryEngine.Run(doc, ViewQuery.Default, cts.Token));
    }


    [Theory]
    [InlineData("a2", "a10", -1)]
    [InlineData("A10", "a2", 1)]
    [InlineData("x007", "x7", -1)]
    [InlineData("abc", "ABC", 1)]
    [InlineData("same", "same", 0)]
    [InlineData("ab", "abc", -1)]
    [InlineData("0x0006", "0x010F", -1)]
    public void ComparesNaturally(string a, string b, int sign) => Assert.Equal(sign, Math.Sign(NaturalComparer.Instance.Compare(a, b)));


    [Fact]
    public void AViewKeepsItsDocumentAndQuery()
    {
        var doc = Camera();
        var query = ViewQuery.Default with { Search = "date", SortColumn = SortColumn.TagName, SortDirection = SortDirection.Descending };

        var view = QueryEngine.Run(doc, query);

        Assert.Same(doc, view.Document);
        Assert.Same(query, view.Query);
        Assert.Null(ViewResult.Empty.Document);
    }


    [Fact]
    public void SortsAndCollapsesReuseTheDocumentsRows()
    {
        var doc = Camera();
        var first = QueryEngine.Run(doc, ViewQuery.Default).RowsInView[3];

        var sorted = QueryEngine.Run(doc, ViewQuery.Default with { SortColumn = SortColumn.TagName, SortDirection = SortDirection.Ascending });
        var collapsed = QueryEngine.Run(doc, ViewQuery.Default with { CollapsedGroups = new HashSet<string>(["EXIF"]) });
        Assert.Same(first, sorted.RowsInView.Single(r => r.Key == first.Key));
        Assert.Same(first, collapsed.RowsInView.Single(r => r.Key == first.Key));

        // other display options or edits make other rows
        var raw = QueryEngine.Run(doc, ViewQuery.Default with { ShowRawValues = true });
        Assert.NotSame(first, raw.RowsInView.Single(r => r.Key == first.Key));
        var edited = QueryEngine.Run(doc, ViewQuery.Default with { Edits = Core.Editing.EditSet.Empty.Delete(first.Key, "x", first.Value) });
        Assert.Equal(Core.Editing.EditState.Deleted, edited.RowsInView.Single(r => r.Key == first.Key).EditState);
        Assert.Equal(sorted.FilterCounts, raw.FilterCounts);
    }


    [Fact]
    public void ComparesNulls()
    {
        Assert.Equal(-1, NaturalComparer.Instance.Compare(null, "a"));
        Assert.Equal(1, NaturalComparer.Instance.Compare("a", null));
        Assert.Equal(0, NaturalComparer.Instance.Compare(null, null));
    }

    #endregion // Tests


    #region Private methods

    private static MetadataDocument Camera() => MetadataDocumentTests.Doc(MetadataDocumentTests.CAMERA_JSON);


    // an A1111 PNG with an IPTC source type beside it
    private static MetadataDocument AiDocument()
    {
        const string PARAMETERS = "a cat <lora:ink:0.5>\nNegative prompt: ugly\nSteps: 20, Sampler: Euler, CFG scale: 7, Seed: 5";
        const string DST = "http://cv.iptc.org/newscodes/digitalsourcetype/trainedAlgorithmicMedia";
        var doc = MetadataDocumentTests.Doc("""
            [{"PNG:Parameters": {"desc": "Parameters", "id": "parameters", "val": "a cat <lora:ink:0.5>\nNegative prompt: ugly\nSteps: 20, Sampler: Euler, CFG scale: 7, Seed: 5"},
              "XMP:XMP-iptcExt:DigitalSourceType": {"desc": "Digital Source Type", "id": "DigitalSourceType", "val": "http://cv.iptc.org/newscodes/digitalsourcetype/trainedAlgorithmicMedia"}}]
            """);
        using var tags = AiTagSet.FromPairs([("PNG:Main:Parameters", PARAMETERS), ("XMP:XMP-iptcExt:Main:DigitalSourceType", DST)]);
        return doc.WithAi(AiEngine.Parse(tags, null, TestContext.Current.CancellationToken));
    }

    #endregion // Private methods
}
