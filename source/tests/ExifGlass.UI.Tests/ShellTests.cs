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
using ExifGlass.Services;
using ExifGlass.ViewModels;
using Xunit;

namespace ExifGlass.UI.Tests;

/// <summary>
/// The shell's logic without a window: the DS 2.1 bands and the open pipeline.
/// </summary>
public class ShellTests
{
    #region Layout bands

    [Theory]
    [InlineData(1400, PanelMode.Docked, PanelMode.Docked, ToolbarMode.Full)]
    [InlineData(1200, PanelMode.Docked, PanelMode.Docked, ToolbarMode.Full)]
    [InlineData(1100, PanelMode.Docked, PanelMode.Overlay, ToolbarMode.Full)]
    [InlineData(900, PanelMode.Docked, PanelMode.Overlay, ToolbarMode.Full)]
    [InlineData(760, PanelMode.Overlay, PanelMode.Overlay, ToolbarMode.Compact)]
    [InlineData(620, PanelMode.Overlay, PanelMode.Overlay, ToolbarMode.Compact)]
    [InlineData(480, PanelMode.Overlay, PanelMode.Overlay, ToolbarMode.IconOnly)]
    [InlineData(420, PanelMode.Overlay, PanelMode.Overlay, ToolbarMode.IconOnly)]
    public void TheFourBandsFollowDs21(double width, PanelMode rail, PanelMode inspector, ToolbarMode toolbar)
    {
        var layout = new ShellLayout();
        layout.Update(width);

        Assert.Equal((rail, inspector, toolbar), (layout.Rail, layout.Inspector, layout.Toolbar));
    }


    [Fact]
    public void ABandHoldsUntilTheWidthFallsPastItsHysteresis()
    {
        var layout = new ShellLayout();
        layout.Update(1300);

        Assert.False(layout.Update(1200 - ShellLayout.HYSTERESIS + 1));
        Assert.Equal(PanelMode.Docked, layout.Inspector);

        Assert.True(layout.Update(1200 - ShellLayout.HYSTERESIS - 1));
        Assert.Equal(PanelMode.Overlay, layout.Inspector);

        // coming back up, the threshold itself is needed again
        Assert.False(layout.Update(1199));
        Assert.True(layout.Update(1200));
        Assert.Equal(PanelMode.Docked, layout.Inspector);
    }


    [Theory]
    [InlineData(800, true, false, true, false)]
    [InlineData(660, true, false, true, false)]
    [InlineData(600, false, false, true, false)]
    [InlineData(540, false, true, true, false)]
    [InlineData(500, false, true, false, false)]
    [InlineData(440, false, true, false, true)]
    public void TheTableWidthRulesFollowPlanP4(double width, bool labels, bool idHidden, bool groupCount, bool narrow)
    {
        var table = new TableLayout();
        table.Update(width);

        Assert.Equal((labels, idHidden, groupCount, narrow), (table.LabelsFit, table.IdColumnsHidden, table.GroupCountFits, table.IsNarrow));
    }


    [Fact]
    public void ThemeCyclesSystemLightDark()
    {
        Assert.Equal(Core.Settings.AppTheme.Light, ThemeService.Next(Core.Settings.AppTheme.System));
        Assert.Equal(Core.Settings.AppTheme.Dark, ThemeService.Next(Core.Settings.AppTheme.Light));
        Assert.Equal(Core.Settings.AppTheme.System, ThemeService.Next(Core.Settings.AppTheme.Dark));
    }

    #endregion // Layout bands


    #region Open pipeline

    [Fact]
    public async Task TheNewestRequestWinsAndALateReadIsDropped()
    {
        var reader = new FakeReader();
        reader.Hold("a.jpg");
        reader.Hold("b.jpg");
        var loader = new DocumentLoader(reader);

        var first = loader.OpenAsync("a.jpg");
        Assert.Equal(LoadPhase.Loading, loader.Phase);
        Assert.Equal("a.jpg", loader.PendingPath);

        var second = loader.OpenAsync("b.jpg");
        reader.Release("b.jpg");
        await second;
        Assert.Equal("b.jpg", loader.Document?.SourcePath);
        Assert.Equal(LoadPhase.Ready, loader.Phase);

        // a's read was cancelled; had it finished late it would still not replace b
        await first;
        Assert.Equal("b.jpg", loader.Document?.SourcePath);
    }


    [Fact]
    public async Task ThePreviousDocumentStaysUntilTheNextIsReady()
    {
        var reader = new FakeReader();
        var loader = new DocumentLoader(reader);
        await loader.OpenAsync("a.jpg");

        reader.Hold("b.jpg");
        var next = loader.OpenAsync("b.jpg");
        Assert.Equal(LoadPhase.Loading, loader.Phase);
        Assert.Equal("a.jpg", loader.Document?.SourcePath);

        reader.Release("b.jpg");
        await next;
        Assert.Equal("b.jpg", loader.Document?.SourcePath);
    }


    [Fact]
    public async Task TheAiReadFollowsTheTable()
    {
        var reader = new FakeReader();
        var loader = new DocumentLoader(reader);
        var phases = new List<LoadPhase>();
        loader.Changed += (_, _) => phases.Add(loader.Phase);

        await loader.OpenAsync("a.png");

        Assert.Equal((1, 1), (reader.Reads, reader.AiReads));
        Assert.Equal([LoadPhase.Loading, LoadPhase.Ready], phases);
    }


    [Fact]
    public async Task AFailedReadKeepsTheLastGoodDocument()
    {
        var reader = new FakeReader();
        var loader = new DocumentLoader(reader);
        await loader.OpenAsync("a.jpg");

        string? failed = null;
        loader.Failed += (_, e) => failed = e.Message;
        reader.Failure = new ExifToolException("File not found");
        await loader.OpenAsync("missing.jpg");

        Assert.Equal("File not found", failed);
        Assert.Equal(LoadPhase.Ready, loader.Phase);
        Assert.Equal("a.jpg", loader.Document?.SourcePath);
    }


    [Fact]
    public async Task AFailedFirstReadGoesBackToEmpty()
    {
        var reader = new FakeReader { Failure = new ExifToolUnavailableException("No ExifTool was found") };
        var loader = new DocumentLoader(reader);

        await loader.OpenAsync("a.jpg");

        Assert.Equal(LoadPhase.Empty, loader.Phase);
        Assert.Null(loader.Document);
    }

    #endregion // Open pipeline
}
