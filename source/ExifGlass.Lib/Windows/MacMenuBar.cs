/*
ExifGlass - AI & EXIF metadata viewer
Copyright (C) 2023 - 2027 DUONG DIEU PHAP
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
using ExifGlass.Core.Localization;
using ExifGlass.UI.Localization;
using ExifGlass.ViewModels;
using System.Runtime.CompilerServices;

namespace ExifGlass.Windows;

/// <summary>
/// The macOS menu bar (Appendix B item 5): ExifGlass (About, Settings…), File, Edit, View, Window; macOS adds Services, Hide and Quit to the app menu itself.
/// </summary>
public static class MacMenuBar
{
    #region Fields and constants

    // a native menu item is no StyledElement, so it can't hold a dynamic resource: each remembers its key instead
    private static readonly ConditionalWeakTable<NativeMenuItem, object> KEYS = [];

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Gives the app its menu; call from Application.Initialize on macOS only: Avalonia.Native reads it once, right after Initialize, and puts "About Avalonia" in when there is none.
    /// </summary>
    public static void AttachAppMenu(Application app, Func<MainViewModel?> viewModel)
    {
        var menu = BuildAppMenu(viewModel);
        NativeMenu.SetMenu(app, menu);
        Lang.CurrentChanged += (_, _) => Localize(menu);
    }


    /// <summary>
    /// Gives the main window its menus; call on macOS only.
    /// </summary>
    public static void Attach(MainWindow window)
    {
        var menu = BuildWindowMenu(window);
        NativeMenu.SetMenu(window, menu);
        LangResources.Track(window, () => Localize(menu));
    }


    /// <summary>
    /// The app menu: About, then Settings… (Cmd+,); the view model is looked up when an item is chosen, since the menu is made before it exists.
    /// </summary>
    public static NativeMenu BuildAppMenu(Func<MainViewModel?> viewModel) =>
    [
        Item(LangId._About, new RelayCommand(() => viewModel()?.OpenAboutCommand.Execute(null))),
        new NativeMenuItemSeparator(),
        Item(LangId.MacMenu_Settings, new RelayCommand(() => viewModel()?.OpenSettingsCommand.Execute(null)), Gesture(Key.OemComma)),
    ];


    /// <summary>
    /// File, Edit, View and Window for the main window.
    /// </summary>
    public static NativeMenu BuildWindowMenu(MainWindow window)
    {
        var viewModel = window.ViewModel;

        var groups = Toggle(LangId.MacMenu_Groups, viewModel.ToggleRailCommand);
        var inspector = Toggle(LangId.MacMenu_Inspector, viewModel.ToggleInspectorCommand);
        var commandBar = Toggle(LangId.MacMenu_CommandBar, new RelayCommand(() => viewModel.IsCommandExpanded = !viewModel.IsCommandExpanded));

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
            Submenu(LangId.MacMenu_File,
                Item(LangId.MacMenu_Open, viewModel.OpenCommand, Gesture(Key.O)),
                new NativeMenuItemSeparator(),
                // through the window's own close, so staged edits ask first (D8)
                Item(LangId.MacMenu_CloseWindow, new RelayCommand(window.Close), Gesture(Key.W))),
            Submenu(LangId.MacMenu_Edit,
                Item(LangId._Copy, new RelayCommand(window.CopyFromMenu), Gesture(Key.C)),
                Item(LangId.MacMenu_SelectAll, new RelayCommand(window.SelectAllFromMenu), Gesture(Key.A)),
                new NativeMenuItemSeparator(),
                Item(LangId.MacMenu_Find, viewModel.FocusSearchCommand, Gesture(Key.K))),
            Submenu(LangId.MacMenu_View, groups, inspector, commandBar),
            Submenu(LangId.MacMenu_Window,
                Item(LangId.MacMenu_Minimize, new RelayCommand(() => window.WindowState = WindowState.Minimized), Gesture(Key.M)),
                Item(LangId.MacMenu_Zoom, new RelayCommand(() => window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized))),
        ];
    }

    #endregion // Public methods


    #region Private methods

    private static KeyGesture Gesture(Key key) => new(key, KeyModifiers.Meta);


    private static NativeMenuItem Item(LangId key, System.Windows.Input.ICommand? command = null, KeyGesture? gesture = null)
    {
        var item = new NativeMenuItem(Lang.Current[key]) { Command = command, Gesture = gesture };
        KEYS.Add(item, key);
        return item;
    }


    private static NativeMenuItem Toggle(LangId key, System.Windows.Input.ICommand command)
    {
        var item = Item(key, command);
        item.ToggleType = MenuItemToggleType.CheckBox;
        return item;
    }


    private static NativeMenuItem Submenu(LangId key, params NativeMenuItemBase[] items)
    {
        var menu = new NativeMenu();
        foreach (var item in items) menu.Add(item);

        var submenu = Item(key);
        submenu.Menu = menu;
        return submenu;
    }


    private static void Localize(NativeMenu menu)
    {
        foreach (var item in menu.Items.OfType<NativeMenuItem>())
        {
            if (KEYS.TryGetValue(item, out var key)) item.Header = Lang.Current[(LangId)key];
            if (item.Menu is { } submenu) Localize(submenu);
        }
    }

    #endregion // Private methods
}
