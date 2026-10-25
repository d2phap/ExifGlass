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
using Avalonia.Input;
using Avalonia.Media;
using ExifGlass.Core.Platform;
using ExifGlass.UI.Motion;
using ExifGlass.UI.Tokens;

namespace ExifGlass.UI.Windows;

/// <summary>
/// The base of the Settings and About windows (DS 6.15, plan 5.6): owned, native title bar, resizable from the spec width, placement saved (O15), Esc closes.
/// </summary>
public class OwnedWindow : Window
{
    #region Fields and constants

    private const double OPEN_SCALE = 0.94;

    // the title bar and borders around the client area, before the window knows its own (S4)
    private const double FRAME_ALLOWANCE = 40;
    private const double WORK_AREA_GAP = 10;

    private readonly ScaleTransform _scale = new(1, 1);
    private Choreo? _motion;
    private bool _closingAnimated;

    #endregion // Fields and constants


    #region Properties

    protected override Type StyleKeyOverride => typeof(Window);


    /// <summary>
    /// Gets whether the content runs the spec's open and close motion: only where the OS doesn't animate the window itself (S4).
    /// </summary>
    public bool UsesContentMotion => PlatformServices.Shell?.AnimatesOwnedWindows != true;

    #endregion // Properties


    #region Constructors

    public OwnedWindow()
    {
        AppWindow.Prepare(this);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = true;
        CanMinimize = false;
        CanMaximize = false;
        ShowInTaskbar = false;
        SizeToContent = SizeToContent.Manual;
    }

    #endregion // Constructors


    #region Public methods

    /// <summary>
    /// Restores the saved placement under a key (Settings, About) before showing; the first time, centred on the owner, and as tall as the content allows when <paramref name="fitHeight"/> (DS 6.15).
    /// </summary>
    public string UsePlacement(string key, Size firstSize, IWindowPlacementStore store, Window owner, bool fitHeight = false)
    {
        MinWidth = firstSize.Width;
        var first = store.Get(key) is null;
        var decision = WindowPlacement.Attach(this, key, firstSize, store, owner);
        if (first && fitHeight) FitContentHeight(owner);

        return decision;
    }


    /// <summary>
    /// Shows the window owned by <paramref name="owner"/> and above it, also when another tool keeps the owner on top: a plain owned window would open under it.
    /// </summary>
    public void ShowAbove(Window owner)
    {
        Topmost = owner.Topmost || (owner.TryGetPlatformHandle()?.Handle is { } handle && PlatformServices.Shell?.IsTopmost(handle) == true);
        Show(owner);
    }

    #endregion // Public methods


    #region Protected methods

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (!UsesContentMotion || Content is not Visual content) return;

        // 260 ms: opacity 0 to 1 and scale 0.94 to 1, (0.2, 0.9, 0.3, 1.1)
        content.RenderTransformOrigin = RelativePoint.Center;
        content.RenderTransform = _scale;
        _motion?.Stop(jumpToEnd: false);
        _motion = new Choreo()
            .Add(MotionTokens.ChildWindowOpen, MotionTokens.StandardEasing, p => content.Opacity = p)
            .Add(MotionTokens.ChildWindowOpen, MotionTokens.ChildWindowOpenEasing, p => _scale.ScaleX = _scale.ScaleY = OPEN_SCALE + (1 - OPEN_SCALE) * p);
        _ = _motion.PlayAsync(this);
    }


    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel || _closingAnimated || !UsesContentMotion || MotionTokens.IsFrozen || Content is not Visual content) return;

        // 180 ms fade of the content, then the window closes for real
        e.Cancel = true;
        _closingAnimated = true;
        var from = content.Opacity;
        _motion?.Stop(jumpToEnd: false);
        _motion = new Choreo().Add(MotionTokens.ChildWindowClose, MotionTokens.ChildWindowCloseEasing, p => content.Opacity = from * (1 - p));
        _motion.PlayAsync(this).ContinueWith(_ => Avalonia.Threading.Dispatcher.UIThread.Post(Close), TaskScheduler.Default);
    }


    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // unless a control used it: an open dropdown closes first
        if (!e.Handled && e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    #endregion // Protected methods


    #region Private methods

    // DS 6.15: no taller than the work area less 10; once open it keeps that height, so the saved size is what the user saw
    private void FitContentHeight(Window owner)
    {
        if (owner.Screens.ScreenFromWindow(owner) is { } screen)
        {
            MaxHeight = Math.Max(MinHeight, screen.WorkingArea.Height / screen.Scaling - FRAME_ALLOWANCE - WORK_AREA_GAP);
        }

        ClearValue(HeightProperty);
        SizeToContent = SizeToContent.Height;
        Opened += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            SizeToContent = SizeToContent.Manual;
            ClearValue(MaxHeightProperty);
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    #endregion // Private methods
}
