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
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using ExifGlass.UI.Controls;
using ExifGlass.UI.Gallery;
using ExifGlass.UI.Tokens;
using Xunit;

namespace ExifGlass.UI.Tests;

public class GalleryTests
{
    #region Fields and constants

    private const double WIDTH = 1180;
    private const double HEIGHT = 14000;

    // a pixel differs when a channel moves more than this; a section fails past MAX_DIFFERENT of its pixels
    private const int CHANNEL_TOLERANCE = 32;
    private const double MAX_DIFFERENT = 0.003;

    #endregion // Fields and constants


    #region Tests

    [AvaloniaTheory]
    [InlineData("light")]
    [InlineData("dark")]
    public void EverySectionRendersAndMatchesItsSnapshot(string theme)
    {
        MotionTokens.IsFrozen = true;
        try
        {
            var variant = theme == "dark" ? ThemeVariant.Dark : ThemeVariant.Light;
            var sections = DesignGalleryCatalog.Build();
            var content = DesignGallery.BuildThemed(sections, variant);
            var areas = new Dictionary<string, Rect>();

            var frame = Render.CaptureSettled(content, WIDTH, HEIGHT, variant, window =>
            {
                foreach (var visual in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(content))
                {
                    if (visual is Control { Tag: string id } section && section.TranslatePoint(default, window) is { } at)
                    {
                        areas[id] = new Rect(at, section.Bounds.Size).Inflate(new Thickness(16, 12));
                    }
                }
            });

            Assert.Equal(sections.Count, areas.Count);

            var failures = new List<string>();
            foreach (var section in sections)
            {
                var area = areas[section.Id];
                Assert.True(area.Bottom < HEIGHT, $"{section.Id} runs past the capture");

                var crop = Render.Crop(frame, area);
                Render.Save(crop, $"gallery/{section.Id}-{theme}");
                if (Compare(crop, section.Id, theme) is { } failure) failures.Add(failure);
            }

            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }
        finally
        {
            MotionTokens.IsFrozen = false;
        }
    }


    [AvaloniaFact]
    public void NoControlKeepsASimpleThemeTemplate()
    {
        MotionTokens.IsFrozen = true;
        try
        {
            var content = DesignGallery.BuildThemed(DesignGalleryCatalog.Build(), ThemeVariant.Light);
            TemplateAuditResult? result = null;
            Render.CaptureSettled(content, WIDTH, HEIGHT, ThemeVariant.Light, _ => result = TemplateAudit.Run(content), settleMs: 100);

            Assert.NotNull(result);
            Assert.True(result.Passed, "Still on a SimpleTheme template: " + string.Join(", ", result.Failures));
            Assert.True(result.Checked > 300, $"only {result.Checked} templated controls checked");
        }
        finally
        {
            MotionTokens.IsFrozen = false;
        }
    }


    // Avalonia 12 clips every templated control to its bounds, so a control whose template draws a shadow past them must turn ClipToBounds off
    [AvaloniaFact]
    public void NoShadowIsClippedByItsOwnControl()
    {
        MotionTokens.IsFrozen = true;
        try
        {
            var content = DesignGallery.BuildThemed(DesignGalleryCatalog.Build(), ThemeVariant.Light);
            var clipped = new SortedSet<string>(StringComparer.Ordinal);
            var count = 0;
            Render.CaptureSettled(content, WIDTH, HEIGHT, ThemeVariant.Light, _ =>
            {
                foreach (var border in content.GetVisualDescendants().OfType<SquircleBorder>())
                {
                    if (border.TemplatedParent is not Visual owner || ShadowReach(border) is not { } reach) continue;
                    count++;

                    // up to the control that owns the template; the gallery's own frames may clip on purpose
                    for (var v = border.GetVisualParent(); v is not null; v = v.GetVisualParent())
                    {
                        if (v.ClipToBounds && border.TranslatePoint(default, v) is { } at && !new Rect(v.Bounds.Size).Contains(reach.Translate(at)))
                        {
                            clipped.Add($"{owner.GetType().Name}: {v.GetType().Name} clips the shadow of {border.Name ?? "a part"}");
                            break;
                        }

                        if (v == owner) break;
                    }
                }
            }, settleMs: 100);

            Assert.True(count >= 5, $"only {count} shadows checked");
            Assert.True(clipped.Count == 0, string.Join(Environment.NewLine, clipped));
        }
        finally
        {
            MotionTokens.IsFrozen = false;
        }
    }


    [AvaloniaFact]
    public void TheGalleryWindowOpensAndAudits()
    {
        MotionTokens.IsFrozen = true;
        try
        {
            var window = new GalleryWindow();
            window.Show();
            window.Rebuild();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.AuditNow();

            Assert.NotNull(window.LastAudit);
            Assert.True(window.LastAudit.Passed, string.Join(", ", window.LastAudit.Failures));
            if (window.CaptureRenderedFrame() is { } frame) Render.Save(frame, "gallery-window");
            window.Close();
        }
        finally
        {
            MotionTokens.IsFrozen = false;
        }
    }

    #endregion // Tests


    #region Private methods

    // where an outset shadow is still clearly visible: its spread and offset plus one blur sigma (SquircleBorder's conversion)
    private static Rect? ShadowReach(SquircleBorder border)
    {
        Rect? reach = null;
        var shadows = border.BoxShadow;
        for (var i = 0; i < shadows.Count; i++)
        {
            var shadow = shadows[i];
            if (shadow.IsInset) continue;

            var sigma = shadow.Blur > 0 ? 0.288675 * shadow.Blur + 0.5 : 0;
            var box = new Rect(border.Bounds.Size).Inflate(shadow.Spread + sigma).Translate(new Vector(shadow.OffsetX, shadow.OffsetY));
            reach = reach is { } r ? r.Union(box) : box;
        }

        return reach;
    }


    // Windows keeps the baselines; other OSes render for review (fonts differ) unless they record their own with EG_UPDATE_SNAPSHOTS=1
    private static unsafe string? Compare(WriteableBitmap actual, string id, string theme)
    {
        var os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "mac" : "linux";
        var baselinePath = Path.Combine(Render.SourceDir, "tests", "ExifGlass.UI.Tests", "Snapshots", os, $"{id}-{theme}.png");

        if (Environment.GetEnvironmentVariable("EG_UPDATE_SNAPSHOTS") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);
            actual.Save(baselinePath, new PngBitmapEncoderOptions());
            return null;
        }

        if (!File.Exists(baselinePath)) return OperatingSystem.IsWindows() ? $"{id}-{theme}: no baseline (EG_UPDATE_SNAPSHOTS=1 records it)" : null;

        // both through the same PNG decode, so pixel format and channel order match
        using var stream = File.OpenRead(baselinePath);
        using var expected = new Bitmap(stream);
        using var encoded = new MemoryStream();
        actual.Save(encoded, new PngBitmapEncoderOptions());
        encoded.Position = 0;
        using var decoded = new Bitmap(encoded);
        if (expected.PixelSize != decoded.PixelSize) return $"{id}-{theme}: size {decoded.PixelSize} vs baseline {expected.PixelSize}";

        var size = expected.PixelSize;
        var stride = size.Width * 4;
        var reference = new byte[stride * size.Height];
        var current = new byte[stride * size.Height];
        fixed (byte* p = reference) expected.CopyPixels(new PixelRect(size), (nint)p, reference.Length, stride);
        fixed (byte* p = current) decoded.CopyPixels(new PixelRect(size), (nint)p, current.Length, stride);

        var different = 0;
        for (var i = 0; i < current.Length; i += 4)
        {
            if (Math.Abs(current[i] - reference[i]) > CHANNEL_TOLERANCE || Math.Abs(current[i + 1] - reference[i + 1]) > CHANNEL_TOLERANCE || Math.Abs(current[i + 2] - reference[i + 2]) > CHANNEL_TOLERANCE)
            {
                different++;
            }
        }

        var share = (double)different / (size.Width * size.Height);
        return share > MAX_DIFFERENT ? $"{id}-{theme}: {share:P2} of pixels differ from the baseline" : null;
    }

    #endregion // Private methods
}
