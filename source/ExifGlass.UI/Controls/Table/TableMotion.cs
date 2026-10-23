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
using Avalonia.Controls.Documents;
using Avalonia.VisualTree;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.Threading;
using ExifGlass.Core.Query;
using ExifGlass.UI.Tokens;
using System.Diagnostics;

namespace ExifGlass.UI.Controls;

public enum ViewMotionKind
{
    None,

    /// <summary>
    /// A new read: rows fade and rise in, staggered (first-load reveal).
    /// </summary>
    Reveal,

    /// <summary>
    /// The same document re-queried (search, sort, filters, the AI data arriving): rows glide from where they were.
    /// </summary>
    Flip,

    Collapse,

    Expand,
}


/// <summary>
/// How a new view reaches the screen, and the group for a collapse or expand.
/// </summary>
public readonly record struct ViewMotion(ViewMotionKind Kind, string? Group = null);


/// <summary>
/// The table's motion on the render thread (DS 5.2, S2): FLIP on composition Translation, the staggered first-load reveal, and a group's collapse as a snapshot ghost plus FLIP.
/// </summary>
internal sealed class TableMotion(MetadataTable table)
{
    #region Fields and constants

    private const float RISE_NEW = 8;
    private const float RISE_REVEAL = 10;
    private const int REVEAL_STAGGER_MAX = 24;

    private static readonly TimeSpan Instant = TimeSpan.FromMilliseconds(1);

    // what each moving row still owes, so a FLIP interrupted by the next starts from where the row is drawn
    private readonly Dictionary<string, (long Start, double Dy, double Ms)> _running = new(StringComparer.Ordinal);
    private readonly HashSet<CompositionVisual> _animated = [];
    private Control? _ghostHost;

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Picks the motion from what changed between two views of the table.
    /// </summary>
    public static ViewMotion Decide(ViewResult? old, ViewResult view)
    {
        if (view.Document is null) return default;
        if (old?.Document is null || !old.Document.IsSameRead(view.Document)) return new(ViewMotionKind.Reveal);

        var a = old.Query;
        var b = view.Query;
        var sameShape = a.Search == b.Search && a.SortColumn == b.SortColumn && a.SortDirection == b.SortDirection
            && a.ShowDuplicates == b.ShowDuplicates && a.ShowRawValues == b.ShowRawValues && a.ShowTagNames == b.ShowTagNames
            && ReferenceEquals(a.Edits, b.Edits) && a.Filters.SetEquals(b.Filters) && a.HiddenGroups.SetEquals(b.HiddenGroups)
            && ReferenceEquals(old.Document, view.Document);
        if (!sameShape) return new(ViewMotionKind.Flip);

        // exactly one group turned: its collapse or expand motion; several (Collapse all) glide as a FLIP
        string? changed = null;
        foreach (var group in a.CollapsedGroups)
        {
            if (b.CollapsedGroups.Contains(group)) continue;
            if (changed is not null) return new(ViewMotionKind.Flip);
            changed = group;
        }

        var expanding = changed is not null;
        foreach (var group in b.CollapsedGroups)
        {
            if (a.CollapsedGroups.Contains(group)) continue;
            if (changed is not null) return new(ViewMotionKind.Flip);
            changed = group;
        }

        if (changed is null) return new(ViewMotionKind.Flip);
        return new(expanding ? ViewMotionKind.Expand : ViewMotionKind.Collapse, changed);
    }


    /// <summary>
    /// The realized rows' Y by item key, with what a running FLIP still owes.
    /// </summary>
    public Dictionary<string, double> Snapshot()
    {
        var map = new Dictionary<string, double>(StringComparer.Ordinal);
        var now = Stopwatch.GetTimestamp();
        foreach (var row in table.RealizedRows())
        {
            if (row.DataContext is TableItem item) map[item.Key] = row.Bounds.Y + Owed(item.Key, now);
        }

        return map;
    }


    /// <summary>
    /// Moves realized rows from their snapshot Y over 480 ms; rows without one fade and rise 8 px over 320 ms, or slide by the shift when they come up from below a collapsed group.
    /// </summary>
    public void Play(Dictionary<string, double> before, double? shiftForNew = null, string? exceptGroup = null)
    {
        if (MotionTokens.IsFrozen) return;

        var now = Stopwatch.GetTimestamp();
        foreach (var row in table.RealizedRows())
        {
            if (row.DataContext is not TableItem item || ElementComposition.GetElementVisual(row) is not { } visual) continue;

            if (before.TryGetValue(item.Key, out var oldY))
            {
                var dy = oldY - row.Bounds.Y;
                if (Math.Abs(dy) < 0.5) StopTranslation(visual, item.Key);
                else Translate(visual, item.Key, dy, MotionTokens.Flip, now);
            }
            else if (shiftForNew is { } shift && GroupOf(item) != exceptGroup) Translate(visual, item.Key, shift, MotionTokens.Flip, now);
            else
            {
                Fade(visual, 0, 1, MotionTokens.RowEnter, MotionTokens.RowEnterEasing, TimeSpan.Zero);
                Translate(visual, item.Key, RISE_NEW, MotionTokens.RowEnter, now);
            }
        }
    }


    /// <summary>
    /// The first-load reveal (DS 5.2): rows fade in over 400 ms and rise 10 px over 600 ms, staggered 140 + i x 22 ms up to i = 24; headers fade, staggered 100 + g x 40 ms.
    /// </summary>
    public void Reveal()
    {
        if (MotionTokens.IsFrozen) return;

        foreach (var row in table.RealizedRows())
        {
            if (row.DataContext is not TableItem item || ElementComposition.GetElementVisual(row) is not { } visual) continue;

            var delay = item.IsHeader
                ? TimeSpan.FromMilliseconds(100 + item.Order * 40)
                : TimeSpan.FromMilliseconds(140 + Math.Min(item.Order, REVEAL_STAGGER_MAX) * 22);
            Fade(visual, 0, 1, MotionTokens.RevealFade, MotionTokens.RevealFadeEasing, delay);
            if (item.IsHeader) continue;

            var rise = visual.Compositor.CreateVector3DKeyFrameAnimation();
            rise.InsertKeyFrame(0f, new Vector3D(0, RISE_REVEAL, 0));
            rise.InsertKeyFrame(1f, new Vector3D(0, 0, 0), MotionTokens.RevealRiseEasing);
            rise.Duration = MotionTokens.RevealRise;
            rise.DelayTime = delay;
            rise.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            visual.StartAnimation("Translation", rise);
            _animated.Add(visual);
        }
    }


    /// <summary>
    /// A row the grid hands to another item drops its motion.
    /// </summary>
    public void Recycled(Control row)
    {
        if (ElementComposition.GetElementVisual(row) is not { } visual || !_animated.Remove(visual)) return;

        Settle(visual);
    }


    /// <summary>
    /// Ends every row's motion where it would have landed: a new document starts clean.
    /// </summary>
    public void StopAll()
    {
        foreach (var visual in _animated) Settle(visual);

        _animated.Clear();
        _running.Clear();
        RemoveGhost();
    }


    /// <summary>
    /// Copies what a collapse hides, or what an expand pushes down, before the new view is applied; null when there is nothing to show (S2 ghost).
    /// </summary>
    public Ghost? Capture(ViewMotion motion)
    {
        if (MotionTokens.IsFrozen || motion.Group is not { } group || table.Presenter is not { } presenter) return null;

        var viewport = presenter.Bounds.Height;
        var key = TableItem.HeaderKey(group);
        double top = 0;
        double bottom = viewport;
        foreach (var row in table.RealizedRows())
        {
            if (row.DataContext is not TableItem { IsHeader: true } item) continue;

            if (item.Key == key) top = Math.Max(0, row.Bounds.Bottom);
            else if (motion.Kind == ViewMotionKind.Collapse && row.Bounds.Y > 0.5 && row.Bounds.Y < bottom && row.Bounds.Y >= top - 0.5) bottom = row.Bounds.Y;
        }

        // a collapse ghosts the group's visible rows; an expand ghosts everything under the header
        if (motion.Kind == ViewMotionKind.Expand) bottom = viewport;
        var height = bottom - top;
        if (height < 1) return null;

        return Ghost.Copy(table, presenter, top, height);
    }


    /// <summary>
    /// Plays a collapse (the ghost's clip shrinks while the rows below glide up) or an expand (the ghost slides down and uncovers the group); both 420 ms Emphasized.
    /// </summary>
    public void PlayGroup(Dictionary<string, double> before, ViewMotion motion, Ghost? ghost)
    {
        if (MotionTokens.IsFrozen) return;

        if (motion.Kind == ViewMotionKind.Collapse)
        {
            Play(before, shiftForNew: ghost?.Height, exceptGroup: motion.Group);
            if (ghost is not null) ShowGhost(ghost, shrink: true, slide: 0);
            return;
        }

        if (ghost is null)
        {
            Play(before);
            return;
        }

        // how far the content below moved: the group's rows now shown under its header
        var revealed = 0.0;
        foreach (var row in table.RealizedRows())
        {
            if (row.DataContext is TableItem { Row: { } r } && r.Group == motion.Group) revealed += row.Bounds.Height;
        }

        ShowGhost(ghost, shrink: false, slide: Math.Min(revealed, ghost.Height));
    }

    #endregion // Public methods


    #region Private methods

    private static string? GroupOf(TableItem item) => item.Group?.Name ?? item.Row?.Group;


    private void Translate(CompositionVisual visual, string key, double dy, TimeSpan duration, long now)
    {
        var animation = visual.Compositor.CreateVector3DKeyFrameAnimation();
        animation.InsertKeyFrame(0f, new Vector3D(0, dy, 0));
        animation.InsertKeyFrame(1f, new Vector3D(0, 0, 0), MotionTokens.FlipEasing);
        animation.Duration = duration;
        visual.StartAnimation("Translation", animation);
        _running[key] = (now, dy, duration.TotalMilliseconds);
        _animated.Add(visual);
    }


    private void Fade(CompositionVisual visual, float from, float to, TimeSpan duration, Avalonia.Animation.Easings.Easing easing, TimeSpan delay)
    {
        var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(0f, from);
        animation.InsertKeyFrame(1f, to, easing);
        animation.Duration = duration;
        animation.DelayTime = delay;
        animation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        visual.StartAnimation("Opacity", animation);
        _animated.Add(visual);
    }


    // a 1 ms animation replaces a running one; setting the same client value would send nothing
    private void StopTranslation(CompositionVisual visual, string key)
    {
        if (!_running.Remove(key)) return;

        var animation = visual.Compositor.CreateVector3DKeyFrameAnimation();
        animation.InsertKeyFrame(1f, new Vector3D(0, 0, 0));
        animation.Duration = Instant;
        visual.StartAnimation("Translation", animation);
    }


    private static void Settle(CompositionVisual visual)
    {
        var move = visual.Compositor.CreateVector3DKeyFrameAnimation();
        move.InsertKeyFrame(1f, new Vector3D(0, 0, 0));
        move.Duration = Instant;
        visual.StartAnimation("Translation", move);

        var fade = visual.Compositor.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(1f, 1f);
        fade.Duration = Instant;
        visual.StartAnimation("Opacity", fade);
    }


    private double Owed(string key, long now)
    {
        if (!_running.TryGetValue(key, out var run)) return 0;

        var t = Stopwatch.GetElapsedTime(run.Start, now).TotalMilliseconds / run.Ms;
        if (t >= 1)
        {
            _running.Remove(key);
            return 0;
        }

        return run.Dy * (1 - MotionTokens.FlipEasing.Ease(t));
    }


    private void ShowGhost(Ghost ghost, bool shrink, double slide)
    {
        RemoveGhost();
        var host = ghost.Host;
        Canvas.SetTop(host, ghost.Top);
        table.GhostLayer.Children.Add(host);
        _ghostHost = host;

        // start once the ghost is on the compositor
        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(_ghostHost, host) || ElementComposition.GetElementVisual(host) is not { } visual) return;

            if (shrink)
            {
                var size = visual.Compositor.CreateVectorKeyFrameAnimation();
                size.InsertKeyFrame(0f, new Vector(ghost.Width, ghost.Height));
                size.InsertKeyFrame(1f, new Vector(ghost.Width, 0), MotionTokens.GroupCollapseEasing);
                size.Duration = MotionTokens.GroupCollapse;
                visual.StartAnimation("Size", size);
            }
            else
            {
                var move = visual.Compositor.CreateVector3DKeyFrameAnimation();
                move.InsertKeyFrame(0f, new Vector3D(0, 0, 0));
                move.InsertKeyFrame(1f, new Vector3D(0, slide, 0), MotionTokens.GroupCollapseEasing);
                move.Duration = MotionTokens.GroupCollapse;
                visual.StartAnimation("Translation", move);
            }

            DispatcherTimer.RunOnce(() =>
            {
                if (ReferenceEquals(_ghostHost, host)) RemoveGhost();
            }, MotionTokens.GroupCollapse + TimeSpan.FromMilliseconds(40));
        }, DispatcherPriority.Render);
    }


    private void RemoveGhost()
    {
        if (_ghostHost is not { } host) return;

        _ghostHost = null;
        table.GhostLayer.Children.Remove(host);
    }

    #endregion // Private methods


    #region Nested types

    /// <summary>
    /// What a group's motion slides or clips away: copies of the rows in its area, made of the same cell controls, so no snapshot is read back from the GPU (no large buffers, no gen-2 GC; plan 8).
    /// </summary>
    public sealed class Ghost
    {
        private Ghost(Control host, double top, double width, double height)
        {
            Host = host;
            Top = top;
            Width = width;
            Height = height;
        }

        public Control Host { get; }

        public double Top { get; }

        public double Width { get; }

        public double Height { get; }

        public static Ghost? Copy(MetadataTable table, Control presenter, double top, double height)
        {
            var width = presenter.Bounds.Width;
            var canvas = new Canvas { Width = width, Height = height, ClipToBounds = true, IsHitTestVisible = false };
            canvas[!Panel.BackgroundProperty] = canvas.GetResourceObservable(TokenKeys.BG_PANEL_BRUSH).ToBinding();
            canvas[!TextElement.ForegroundProperty] = canvas.GetResourceObservable(TokenKeys.TEXT_PRIMARY_BRUSH).ToBinding();
            canvas[!TextElement.FontSizeProperty] = canvas.GetResourceObservable(TokenKeys.FONT_SIZE_BODY).ToBinding();

            foreach (var row in table.RealizedRows())
            {
                if (row.DataContext is not TableItem item || row.Bounds.Bottom <= top || row.Bounds.Y >= top + height) continue;

                var copy = item.IsHeader ? HeaderCopy(item, row, table.IsNarrow) : RowCopy(item, row);
                Canvas.SetTop(copy, row.Bounds.Y - top);
                canvas.Children.Add(copy);
            }

            return canvas.Children.Count == 0 ? null : new Ghost(canvas, top, width, height);
        }

        private static Control HeaderCopy(TableItem item, Control row, bool narrow)
        {
            var view = new GroupHeaderView { Margin = new Thickness(12, 0), ShowsDescription = !narrow };
            view.Show(item.Group!, animate: false);
            var header = new Border { Width = row.Bounds.Width, Height = row.Bounds.Height, Child = view };
            header[!Border.BackgroundProperty] = header.GetResourceObservable(TokenKeys.BG_GROUP_HEADER_BRUSH).ToBinding();
            return Separated(header, row, TokenKeys.EMPHASIS_A07_BRUSH);
        }

        // each shown cell again at its place, with its padding and its content control
        private static Control RowCopy(TableItem item, DataGridRow row)
        {
            var canvas = new Canvas { Width = row.Bounds.Width, Height = row.Bounds.Height };
            foreach (var cell in row.GetVisualDescendants().OfType<DataGridCell>())
            {
                if (!cell.IsVisible || cell.Bounds.Width < 1 || cell.TranslatePoint(default, row) is not { } at) continue;

                Control? content = cell.Content switch
                {
                    IndexCell => new IndexCell(),
                    IdCell => new IdCell(),
                    NameCell => new NameCell(),
                    ValueCell => new ValueCell(),
                    _ => null,
                };
                if (content is null) continue;

                content.DataContext = item;
                var box = new Border { Width = cell.Bounds.Width, Height = cell.Bounds.Height, Padding = cell.Padding, Child = content };
                Canvas.SetLeft(box, at.X);
                Canvas.SetTop(box, at.Y);
                canvas.Children.Add(box);
            }

            return Separated(canvas, row, TokenKeys.EMPHASIS_A05_BRUSH);
        }

        private static Control Separated(Control content, Control row, string brushKey)
        {
            var line = new Border { Height = 1, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom };
            line[!Border.BackgroundProperty] = line.GetResourceObservable(brushKey).ToBinding();
            return new Panel { Width = row.Bounds.Width, Height = row.Bounds.Height, Children = { content, line } };
        }
    }

    #endregion // Nested types
}
