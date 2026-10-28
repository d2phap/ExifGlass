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
using CommunityToolkit.Mvvm.Input;
using ExifGlass.ViewModels;

namespace ExifGlass.Windows;

/// <summary>
/// The macOS menu bar (Appendix B item 5): ExifGlass (About, Settings…), File, Edit, View, Window; macOS adds Services, Hide and Quit to the app menu itself.
/// </summary>
public static class MacMenuBar
{
    #region Public methods

    /// <summary>
    /// Gives the app its menu; call from Application.Initialize on macOS only.
    /// Avalonia.Native reads the app menu once, right after Initialize, and puts "About Avalonia" in when there is none.
    /// </summary>
    public static void AttachAppMenu(Application app, Func<MainViewModel?> viewModel) =>
        NativeMenu.SetMenu(app, BuildAppMenu(viewModel));


    /// <summary>
    /// Gives the main window its menus; call on macOS only.
    /// </summary>
    public static void Attach(MainWindow window) => NativeMenu.SetMenu(window, BuildWindowMenu(window));


    /// <summary>
    /// The app menu: About, then Settings… (Cmd+,); the view model is looked up when an item is chosen, since the menu is made before it exists.
    /// </summary>
    public static NativeMenu BuildAppMenu(Func<MainViewModel?> viewModel) =>
    [
        new NativeMenuItem(AppText.ABOUT) { Command = new RelayCommand(() => viewModel()?.OpenAboutCommand.Execute(null)) },
        new NativeMenuItemSeparator(),
        new NativeMenuItem(AppText.MENU_SETTINGS) { Command = new RelayCommand(() => viewModel()?.OpenSettingsCommand.Execute(null)), Gesture = Gesture(Key.OemComma) },
    ];


    /// <summary>
    /// File, Edit, View and Window for the main window.
    /// </summary>
    public static NativeMenu BuildWindowMenu(MainWindow window)
    {
        var viewModel = window.ViewModel;

        var groups = Toggle(AppText.MENU_GROUPS, viewModel.ToggleRailCommand);
        var inspector = Toggle(AppText.MENU_INSPECTOR, viewModel.ToggleInspectorCommand);
        var commandBar = Toggle(AppText.MENU_COMMAND_BAR, new RelayCommand(() => viewModel.IsCommandExpanded = !viewModel.IsCommandExpanded));

        void Sync()
        {
            groups.IsChecked = viewModel.IsRailShown;
            inspector.IsChecked = viewModel.IsInspectorShown;
            commandBar.IsChecked = viewModel.IsCommandExpanded;
        }

        Sync();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.IsRailShown) or nameof(MainViewModel.IsInspectorShown) or nameof(MainViewModel.IsCommandExpanded)) Sync();
        };

        return
        [
            Submenu(AppText.MENU_FILE,
                new NativeMenuItem(AppText.MENU_OPEN) { Command = viewModel.OpenCommand, Gesture = Gesture(Key.O) }),
            Submenu(AppText.MENU_EDIT,
                new NativeMenuItem(AppText.MENU_COPY) { Command = new RelayCommand(window.CopyFromMenu), Gesture = Gesture(Key.C) },
                new NativeMenuItem(AppText.MENU_SELECT_ALL) { Command = new RelayCommand(window.SelectAllFromMenu), Gesture = Gesture(Key.A) },
                new NativeMenuItemSeparator(),
                new NativeMenuItem(AppText.MENU_FIND) { Command = viewModel.FocusSearchCommand, Gesture = Gesture(Key.K) }),
            Submenu(AppText.MENU_VIEW, groups, inspector, commandBar),
            Submenu(AppText.MENU_WINDOW,
                new NativeMenuItem(AppText.MENU_MINIMIZE) { Command = new RelayCommand(() => window.WindowState = WindowState.Minimized), Gesture = Gesture(Key.M) },
                new NativeMenuItem(AppText.MENU_ZOOM) { Command = new RelayCommand(() => window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized) }),
        ];
    }

    #endregion // Public methods


    #region Private methods

    private static KeyGesture Gesture(Key key) => new(key, KeyModifiers.Meta);


    private static NativeMenuItem Toggle(string header, System.Windows.Input.ICommand command) => new(header) { Command = command, ToggleType = MenuItemToggleType.CheckBox };


    private static NativeMenuItem Submenu(string header, params NativeMenuItemBase[] items)
    {
        var menu = new NativeMenu();
        foreach (var item in items) menu.Add(item);
        return new NativeMenuItem(header) { Menu = menu };
    }

    #endregion // Private methods
}
