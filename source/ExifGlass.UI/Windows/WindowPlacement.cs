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
using Avalonia.Platform;
using Avalonia.Threading;
using ExifGlass.Core.Settings;

namespace ExifGlass.UI.Windows;

/// <summary>
/// Where window placements are kept; the app stores them in its settings (AppSettings.Windows).
/// </summary>
public interface IWindowPlacementStore
{
    #region Methods

    WindowPlacementState? Get(string key);

    void Set(string key, WindowPlacementState placement);

    #endregion // Methods
}


/// <summary>
/// Placements kept in memory only: the gallery and tests.
/// </summary>
public sealed class MemoryPlacementStore : IWindowPlacementStore
{
    #region Fields and constants

    private readonly Dictionary<string, WindowPlacementState> _all = new(StringComparer.Ordinal);

    #endregion // Fields and constants


    #region Public methods

    public WindowPlacementState? Get(string key) => _all.TryGetValue(key, out var p) ? p.Clone() : null;

    public void Set(string key, WindowPlacementState placement) => _all[key] = placement.Clone();

    #endregion // Public methods
}


/// <summary>
/// Saves and restores a window's state, size and location (O15, plan 5.6): v2's race-free capture plus the off-screen guard and the frame extents (S4).
/// </summary>
public static class WindowPlacement
{
    #region Public methods

    /// <summary>
    /// Restores a window before it shows, then tracks it until it closes; returns what restore decided, for the log.
    /// </summary>
    public static string Attach(Window window, string key, Size firstSize, IWindowPlacementStore store, Window? owner = null)
    {
        var minSize = new Size(window.MinWidth, window.MinHeight);
        var screens = ScreensOf(window);
        string decision;

        if (store.Get(key) is not { Width: > 0, Height: > 0 } saved)
        {
            // first open: the main window centred on its screen at the first size, a child centred on its owner
            window.Width = firstSize.Width;
            window.Height = firstSize.Height;
            window.WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;

            if (owner is null && screens.Count > 0)
            {
                var screen = Primary(screens);
                var first = PlacementMath.Centre(screen, firstSize, minSize, default);
                Apply(window, first);
            }

            decision = "first open: centred";
        }
        else
        {
            var fallback = owner is not null && owner.Screens.ScreenFromWindow(owner) is { } ownerScreen ? Info(ownerScreen) : (ScreenInfo?)null;
            var result = PlacementMath.Restore(saved, minSize, screens, fallback);
            Apply(window, result);
            decision = result.Decision;

            if (saved.Maximized)
            {
                // the platform window must exist first (v2); the normal bounds above stay the restore-down size
                window.Opened += (_, _) => window.WindowState = WindowState.Maximized;
                decision += ", maximized on open";
            }
        }

        new Tracker(window, key, store).Start();
        return decision;
    }

    #endregion // Public methods


    #region Private methods

    private static void Apply(Window window, PlacementResult result)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Width = result.Size.Width;
        window.Height = result.Size.Height;
        window.Position = result.Position;
    }


    private static List<ScreenInfo> ScreensOf(Window window)
    {
        var list = new List<ScreenInfo>();
        if (window.Screens is not { } screens) return list;

        foreach (var screen in screens.All) list.Add(Info(screen));
        return list;
    }


    private static ScreenInfo Info(Screen screen) => new(screen.Bounds, screen.WorkingArea, screen.Scaling, screen.IsPrimary);


    private static ScreenInfo Primary(List<ScreenInfo> screens)
    {
        foreach (var s in screens)
        {
            if (s.IsPrimary) return s;
        }

        return screens[0];
    }

    #endregion // Private methods


    #region Nested types

    // maximize and minimize move and resize the window before WindowState catches up, so the capture waits at Background (v2)
    private sealed class Tracker(Window window, string key, IWindowPlacementStore store)
    {
        private bool _queued;
        private WindowPlacementState? _normal;
        private WindowState _beforeMinimized = WindowState.Normal;


        public void Start()
        {
            window.Opened += (_, _) => Queue();
            window.PositionChanged += (_, _) => Queue();
            window.Resized += (_, _) => Queue();
            window.Closing += (_, _) => Commit();
            window.PropertyChanged += (_, e) =>
            {
                if (e.Property == Window.WindowStateProperty && e.GetNewValue<WindowState>() == WindowState.Minimized) _beforeMinimized = e.GetOldValue<WindowState>();
            };
        }


        private void Queue()
        {
            if (_queued || !window.IsVisible || window.WindowState != WindowState.Normal) return;

            _queued = true;
            Dispatcher.UIThread.Post(() =>
            {
                _queued = false;
                Capture();
            }, DispatcherPriority.Background);
        }


        private void Capture()
        {
            if (!window.IsVisible || window.WindowState != WindowState.Normal) return;

            var size = window.ClientSize;
            if (size.Width <= 0 || size.Height <= 0) return;

            var screen = window.Screens.ScreenFromWindow(window);
            var frame = window.FrameSize is { } outer ? outer - size : default;
            _normal = new WindowPlacementState
            {
                X = window.Position.X,
                Y = window.Position.Y,
                Width = size.Width,
                Height = size.Height,
                ScreenX = screen?.Bounds.X ?? 0,
                ScreenY = screen?.Bounds.Y ?? 0,
                ScreenWidth = screen?.Bounds.Width ?? 0,
                ScreenHeight = screen?.Bounds.Height ?? 0,
                Scaling = screen?.Scaling ?? 1,
                FrameWidth = Math.Max(0, frame.Width),
                FrameHeight = Math.Max(0, frame.Height),
            };
        }


        private void Commit()
        {
            if (window.WindowState == WindowState.Normal) Capture();
            if (_normal is null) return;

            // a minimized window is never restored minimized; it keeps the state it had before
            var state = window.WindowState == WindowState.Minimized ? _beforeMinimized : window.WindowState;
            _normal.Maximized = state is WindowState.Maximized or WindowState.FullScreen;
            store.Set(key, _normal);
        }
    }

    #endregion // Nested types
}
