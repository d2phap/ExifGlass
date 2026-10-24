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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using ExifGlass.Core.Editing;
using ExifGlass.Core.Metadata;
using ExifGlass.Services;
using ExifGlass.UI.Controls;
using ExifGlass.UI.Tokens;
using SkiaSharp;
using System.Runtime.InteropServices;
using Xunit;

namespace ExifGlass.UI.Tests;

/// <summary>
/// The rail's and inspector's controls (P6): the composition bar, group rows, the cards' rules and the preview decoder.
/// </summary>
public class PanelTests
{
    #region Tests

    [AvaloniaFact]
    public void TheCompositionBarWeighsGroupsByTagsAndDropsHiddenOnes()
    {
        MotionTokens.IsFrozen = true;
        try
        {
            var bar = new CompositionBar { Segments = [new("File", 17, true), new("EXIF", 57, true), new("XMP", 0, true)] };
            Assert.Equal([17, 57, 0.5], bar.Weights);

            // the same groups glide; a hidden one goes to nothing
            bar.Segments = [new("File", 17, true), new("EXIF", 57, false), new("XMP", 0, true)];
            Assert.Equal([17, 0, 0.5], bar.Weights);

            bar.Segments = [new("PNG", 8, true)];
            Assert.Equal([8.0], bar.Weights);
        }
        finally
        {
            MotionTokens.IsFrozen = false;
        }
    }


    [AvaloniaTheory]
    [InlineData(10, true, false)]
    [InlineData(80, false, true)]
    public void AGroupRowTogglesFromItsCheckboxAndJumpsFromTheRest(double x, bool toggles, bool jumps)
    {
        var row = new RailGroupRow { Width = 220, Group = "EXIF", Text = "EXIF", Count = "57" };
        var window = Show(row);
        var (toggled, jumped) = (0, 0);
        row.ToggleRequested += (_, _) => toggled++;
        row.JumpRequested += (_, _) => jumped++;

        var at = row.TranslatePoint(new Point(x, 16), window)!.Value;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);

        Assert.Equal(toggles ? 1 : 0, toggled);
        Assert.Equal(jumps ? 1 : 0, jumped);
        window.Close();
    }


    [AvaloniaFact]
    public void EachPartOfAGroupRowHoversOnItsOwn()
    {
        var row = new RailGroupRow { Width = 220, Group = "EXIF", Text = "EXIF", Count = "57" };
        var window = Show(row);
        var (check, jump) = (Part<Button>(row, "PART_Check"), Part<Button>(row, "PART_Jump"));

        window.MouseMove(row.TranslatePoint(new Point(10, 16), window)!.Value);
        Assert.True(check.IsPointerOver);
        Assert.False(jump.IsPointerOver);

        window.MouseMove(row.TranslatePoint(new Point(120, 16), window)!.Value);
        Assert.False(check.IsPointerOver);
        Assert.True(jump.IsPointerOver);
        Assert.Equal(34, jump.TranslatePoint(default, row)!.Value.X, 1);

        // the box sits 10 in, under the overline, and 12 from the dot (owner at G7: was 20)
        var box = Part<Control>(row, "PART_BoxFill").TranslatePoint(default, row)!.Value.X;
        Assert.Equal(10, box, 1);
        Assert.Equal(box + 16 + 12, Part<Control>(row, "PART_Dot").TranslatePoint(default, row)!.Value.X, 1);
        window.Close();
    }


    [AvaloniaFact]
    public void AGroupRowActsOnlyWhereItWasPressedAndFromTheKeyboard()
    {
        var row = new RailGroupRow { Width = 220, Group = "EXIF", Text = "EXIF", Count = "57" };
        var window = Show(row);
        var (toggled, jumped) = (0, 0);
        row.ToggleRequested += (_, _) => toggled++;
        row.JumpRequested += (_, _) => jumped++;

        // pressed on the checkbox, released over the name: neither, as a button dragged off
        window.MouseDown(row.TranslatePoint(new Point(10, 16), window)!.Value, MouseButton.Left);
        window.MouseUp(row.TranslatePoint(new Point(120, 16), window)!.Value, MouseButton.Left);
        Assert.Equal((0, 0), (toggled, jumped));

        // two tab stops: Space on the checkbox, Enter on the name
        Part<Button>(row, "PART_Check").Focus();
        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        Part<Button>(row, "PART_Jump").Focus();
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Assert.Equal((1, 1), (toggled, jumped));
        window.Close();
    }


    [Fact]
    public void ExposureCellsKeepTheSpecOrderAndCountShuttersAsOneOverN()
    {
        ExposureItem[] items = [new("Shutter", 250, 0, "1/", "", "Program AE"), new("Aperture", 2.0, 1, "f/", "", "max f/1.4"), new("ISO", double.NaN, 0, "", "", "")];
        var cells = ExposureCard.BuildCells(items);

        Assert.Equal(["APERTURE", "SHUTTER", "ISO", "FOCAL"], cells.Select(c => c.Label));
        Assert.Equal(CountUpFormat.OneDecimal, cells[0].Format);
        Assert.Equal("f/", cells[0].Prefix);
        Assert.Equal(CountUpFormat.Shutter, cells[1].Format);
        Assert.Null(cells[1].Prefix);
        Assert.False(cells[2].HasValue);
        Assert.False(cells[3].HasValue);
        Assert.Equal("1/250", CountUpText.FormatValue(cells[1].Value, cells[1].Format));

        // the card hides when none of the four has a value
        Assert.True(ExposureCard.HasReadout(items));
        Assert.False(ExposureCard.HasReadout([new("ISO", double.NaN, 0, "", "", ""), new("Look", 3, 0, "", "", "")]));
        Assert.False(ExposureCard.HasReadout(null));
    }


    [Fact]
    public void DetailsKeepTheSpecRowsWithTheirIconsAndHideWithoutThem()
    {
        DetailItem[] items = [new("Camera", "FUJIFILM X-T5"), new("Look", "Classic Chrome", "Film simulation"), new("Location", "22.2759° N", "", "https://www.openstreetmap.org/")];
        var rows = DetailsCard.BuildRows(items);

        Assert.Equal(["Camera", "Location"], rows.Select(r => r.Label));
        Assert.Equal([IconId.Camera, IconId.Location], rows.Select(r => r.Icon));
        Assert.True(rows[0].IsFirst);
        Assert.False(rows[1].IsFirst);
        Assert.True(rows[1].HasLink);

        Assert.True(DetailsCard.HasRows(items));
        Assert.Empty(DetailsCard.BuildRows([new("Look", "Classic Chrome")]));
        Assert.False(DetailsCard.HasRows([new("Look", "Classic Chrome")]));
        Assert.False(DetailsCard.HasRows(null));
    }


    [AvaloniaFact]
    public void TilesSitInTwoColumnsWithALoneLastTileAcross()
    {
        var grid = new TileGrid { Width = 201 };
        for (var i = 0; i < 3; i++) grid.Children.Add(new Border { Height = 30 });
        var window = Show(grid);

        Assert.Equal(new Rect(0, 0, 100, 30), grid.Children[0].Bounds);
        Assert.Equal(new Rect(101, 0, 100, 30), grid.Children[1].Bounds);
        Assert.Equal(new Rect(0, 31, 201, 30), grid.Children[2].Bounds);
        window.Close();
    }


    [AvaloniaTheory]
    [InlineData(1.5, 278, 185.33)]
    [InlineData(0.5, 110, 220)]
    public void TheFrameBoxIsAtMost278WideAnd220High(double aspect, double width, double height)
    {
        var marker = new Border { Width = 28, Height = 20 };
        FrameBox.SetAnchor(marker, new Point(0.5, 0.25));
        var box = new FrameBox { Aspect = aspect, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top, Children = { marker } };
        var window = Show(box, 400);

        // layout rounds to whole pixels
        Assert.InRange(box.Bounds.Width, width - 1, width + 1);
        Assert.InRange(box.Bounds.Height, height - 1, height + 1);
        Assert.InRange(marker.Bounds.X, width / 2 - 15, width / 2 - 13);
        Assert.InRange(marker.Bounds.Y, height / 4 - 11, height / 4 - 9);
        window.Close();
    }


    [AvaloniaFact]
    public void TheFrameCardWritesSizeRatioAndMegapixelsAndTakesThePreviewsShape()
    {
        var card = new FrameCard { Frame = new FrameInfo(7728, 5152, "3:2", 39.8, 50, 50) };
        Assert.Equal("7728 × 5152", card.Dimensions);
        Assert.Equal("3:2", card.Ratio);
        Assert.Equal("39.8 MP", card.Megapixels);
        Assert.Equal(new Point(0.5, 0.5), card.FocusPoint);
        Assert.Equal(1.5, card.Aspect, 3);

        using var portrait = Solid(20, 40);
        card.Preview = portrait;
        Assert.Equal(0.5, card.Aspect, 3);

        Assert.Equal("Unknown size", new FrameCard { Frame = new FrameInfo(0, 0, "—", 0, null, null) }.Dimensions);
        Assert.Equal("—", new FrameCard().Dimensions);
    }


    [AvaloniaFact]
    public void TheSelectedTagCardHasNoEditUiForReadOnlyTagsAndSwapsDeleteForUndo()
    {
        var view = new SelectedTagView("EXIF:IFD0:Artist", "EXIF", "0x013B", "Artist", "Phap", "Phap", "string", "-IFD0:Artist", false);
        var card = new SelectedTagCard { Width = 304, View = view };
        var window = Show(card);

        Assert.True(Part<Button>(card, "PART_Edit").IsVisible);
        Assert.True(Part<Button>(card, "PART_Delete").IsVisible);
        Assert.False(Part<Button>(card, "PART_Undo").IsVisible);

        card.View = view with { State = EditState.Deleted };
        Assert.False(Part<Button>(card, "PART_Delete").IsVisible);
        Assert.True(Part<Button>(card, "PART_Undo").IsVisible);

        card.View = view with { State = EditState.Edited, OriginalValue = "Duong" };
        Assert.True(Part<Control>(card, "PART_Was").IsVisible);
        Assert.Equal("Was: Duong", card.View.WasText);

        card.View = view with { IsReadOnly = true };
        Assert.False(Part<Button>(card, "PART_Edit").IsVisible);
        Assert.False(Part<Button>(card, "PART_Delete").IsVisible);
        Assert.False(Part<Control>(card, "PART_Divider").IsVisible);

        card.View = view;
        card.IsEditing = true;
        Assert.False(Part<Control>(card, "PART_Well").IsVisible);
        Assert.True(Part<Control>(card, "PART_EditPanel").IsVisible);
        Assert.False(Part<Button>(card, "PART_Edit").IsVisible);
        window.Close();
    }


    [AvaloniaFact]
    public void TheAiCardOffersCopyOnlyWhenThereIsSomethingToCopy()
    {
        static ExifGlass.Core.Ai.AiGenerationInfo Info(params ExifGlass.Core.Ai.AiPrompt[] prompts) => new([new ExifGlass.Core.Ai.AiSourceInfo
        {
            Source = ExifGlass.Core.Ai.AiSourceKind.C2pa,
            Generator = "ChatGPT",
            Kind = ExifGlass.Core.Ai.AiKind.Generated,
            Prompts = prompts,
        }]);

        var card = new AiCard { Width = 304, Info = Info() };
        card.Info = new([new ExifGlass.Core.Ai.AiSourceInfo
        {
            Source = ExifGlass.Core.Ai.AiSourceKind.C2pa,
            Generator = "ChatGPT",
            Kind = ExifGlass.Core.Ai.AiKind.Generated,
            Parameters = [new ExifGlass.Core.Ai.AiField("claimGenerator", "Claim generator", "ChatGPT", "Claim generator")],
        }]);
        var window = Show(card);
        Assert.Equal("ChatGPT", card.Generator);
        Assert.Equal(UiText.COPY_AI_JSON, card.MainActionText);
        Assert.Equal(AiAction.CopyJson, Part<Button>(card, "PART_MainAction").Tag);
        Assert.False(Part<Control>(card, "PART_PromptSection").IsVisible);
        Assert.True(Part<Control>(card, "PART_ProvenanceNote").IsVisible);

        card.Info = Info(new ExifGlass.Core.Ai.AiPrompt("a lighthouse", ExifGlass.Core.Ai.AiPromptRole.Positive));
        Assert.Equal(UiText.COPY_PARAMETERS, card.MainActionText);
        Assert.Equal(AiAction.CopyGenerationParameters, Part<Button>(card, "PART_MainAction").Tag);
        Assert.True(Part<Control>(card, "PART_PromptSection").IsVisible);
        Assert.False(Part<Control>(card, "PART_ProvenanceNote").IsVisible);
        window.Close();
    }


    [AvaloniaFact]
    public void ThePreviewIsDecodedAtTheFrameSizeAndTurnedUpright()
    {
        // left half red, right half blue; the codec owns the stream it reads
        using var full = PreviewService.Decode(Encode(1200, 800), 1, 2)!;
        Assert.Equal(new PixelSize(556, 371), full.PixelSize);

        // orientation 6 turns it 90 degrees clockwise: the red half goes on top
        using var turned = PreviewService.Decode(Encode(1200, 800), 6, 1)!;
        Assert.Equal(turned.PixelSize.Width, Math.Min(turned.PixelSize.Width, turned.PixelSize.Height));
        var (top, bottom) = (Pixel(turned, turned.PixelSize.Width / 2, 2), Pixel(turned, turned.PixelSize.Width / 2, turned.PixelSize.Height - 3));
        Assert.True(top.Red > 200 && top.Blue < 50, $"top {top}");
        Assert.True(bottom.Blue > 200 && bottom.Red < 50, $"bottom {bottom}");

        Assert.Null(PreviewService.Decode(new MemoryStream([1, 2, 3]), 1, 2));
    }

    #endregion // Tests


    #region Private methods

    private static Window Show(Control content, double width = 320)
    {
        var window = new Window { Width = width, Height = 600, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }


    private static T Part<T>(Control control, string name) where T : Control
    {
        Dispatcher.UIThread.RunJobs();
        return Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(control).OfType<T>().First(c => c.Name == name);
    }


    private static MemoryStream Encode(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.DrawRect(0, 0, width / 2f, height, new SKPaint { Color = SKColors.Red });
            canvas.DrawRect(width / 2f, 0, width / 2f, height, new SKPaint { Color = SKColors.Blue });
        }

        var stream = new MemoryStream();
        bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
        stream.Position = 0;
        return stream;
    }


    private static Bitmap Solid(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            return new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Premul, handle.AddrOfPinnedObject(), new PixelSize(width, height), new Vector(96, 96), width * 4);
        }
        finally
        {
            handle.Free();
        }
    }


    private static (byte Red, byte Blue) Pixel(Bitmap bitmap, int x, int y)
    {
        var pixels = new byte[bitmap.PixelSize.Width * bitmap.PixelSize.Height * 4];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new PixelRect(bitmap.PixelSize), handle.AddrOfPinnedObject(), pixels.Length, bitmap.PixelSize.Width * 4);
        }
        finally
        {
            handle.Free();
        }

        var i = (y * bitmap.PixelSize.Width + x) * 4;
        return (pixels[i + 2], pixels[i]);
    }

    #endregion // Private methods
}
