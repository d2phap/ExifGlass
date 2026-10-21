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
                if (Render.CompareToBaseline(crop, section.Id, theme) is { } failure) failures.Add(failure);
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


    // Avalonia 12 clips every templated control to its bounds, so a control whose template paints past them (a shadow, an outset focus ring) must turn ClipToBounds off
    [AvaloniaFact]
    public void NoShadowOrRingIsClippedByItsOwnControl()
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
                    if (border.TemplatedParent is not Visual owner) continue;

                    count++;
                    var reach = PaintReach(border);

                    // up to the control that owns the template; the gallery's own frames may clip on purpose
                    for (var v = border.GetVisualParent(); v is not null; v = v.GetVisualParent())
                    {
                        // both corners through the transforms, since a pressed part is scaled
                        if (v.ClipToBounds && border.TranslatePoint(reach.TopLeft, v) is { } from && border.TranslatePoint(reach.BottomRight, v) is { } to && !new Rect(v.Bounds.Size).Inflate(0.5).Contains(new Rect(from, to)))
                        {
                            clipped.Add($"{owner.GetType().Name}: {v.GetType().Name} clips {border.Name ?? "a part"}");
                            break;
                        }

                        if (v == owner) break;
                    }
                }
            }, settleMs: 100);

            Assert.True(count >= 100, $"only {count} template parts checked");
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

    // what a part paints: its own box (an outset focus ring) and where its outset shadows are still clearly visible, spread and offset plus one blur sigma (SquircleBorder's conversion)
    private static Rect PaintReach(SquircleBorder border)
    {
        var reach = new Rect(border.Bounds.Size);
        var shadows = border.BoxShadow;
        for (var i = 0; i < shadows.Count; i++)
        {
            var shadow = shadows[i];
            if (shadow.IsInset) continue;

            var sigma = shadow.Blur > 0 ? 0.288675 * shadow.Blur + 0.5 : 0;
            reach = reach.Union(new Rect(border.Bounds.Size).Inflate(shadow.Spread + sigma).Translate(new Vector(shadow.OffsetX, shadow.OffsetY)));
        }

        return reach;
    }

    #endregion // Private methods
}
