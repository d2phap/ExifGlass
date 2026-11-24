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
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using ExifGlass.Core.Localization;
using ExifGlass.UI;
using ExifGlass.ViewModels;
using ExifGlass.Windows;
using Xunit;

namespace ExifGlass.UI.Tests;

/// <summary>
/// The Language setting: OK switches every open window at once, no restart (Lang.Current is shared, so each test puts English back).
/// </summary>
public sealed class LanguageTests
{
    #region Fields and constants

    private const string PACK = "Test.eglang.json";

    private const string PACK_JSON = """
        {
          "_Metadata": { "Code": "vi-VN", "EnglishName": "Vietnamese", "LocalName": "Tiếng Việt", "Contributors": ["An", "Bình"], "MinVersion": 3 },
          "Items": {
            "_About": "Giới thiệu",
            "_Settings": "Cài đặt",
            "_Tags": "{0:N0} thẻ",
            "Toolbar_Search": "Tìm kiếm",
            "Toolbar_OpenFileTip": "Mở tệp ({0})",
            "Table_HeaderTagName": "TÊN THẺ",
            "Rail_Groups": "NHÓM",
            "Toast_SettingsSaved": "Đã lưu cài đặt",
            "Settings_Language": "Ngôn ngữ"
          }
        }
        """;

    #endregion // Fields and constants


    #region Tests

    [AvaloniaFact]
    public async Task OkSwitchesEveryOpenWindowWithoutARestart()
    {
        var english = Lang.Current;
        var userDirectory = Lang.UserDirectory;
        try
        {
            using var shell = await TestShell.OpenWithFileAsync(1400);
            InstallPack(shell);
            var window = shell.Window;
            window.ShowAbout();
            shell.Settle(200);
            var about = Assert.Single(window.OwnedWindows.OfType<AboutWindow>());

            var (settings, vm) = await OpenSettingsAsync(shell);
            vm.SelectedLanguage = vm.Languages.Single(l => !l.Pack.IsBuiltIn);
            shell.Settle();

            // chosen, not applied: the row credits the translators, every window still speaks English
            Assert.Equal("Contributors: An, Bình", vm.LanguageContributors);
            Assert.Equal("Search", window.Toolbar.Search.PlaceholderText);
            Assert.Equal("About", about.Title);

            vm.OkCommand.Execute(null);
            shell.Settle(300);

            Assert.False(settings.IsVisible);
            Assert.Equal(PACK, shell.Settings.Current.Language);
            Assert.Equal("Tiếng Việt", Lang.Current.Metadata.LocalName);
            Assert.Equal("Tìm kiếm", window.Toolbar.Search.PlaceholderText);
            Assert.Equal($"Mở tệp ({UiText.Shortcut("O")})", ToolTip.GetTip(window.Toolbar.OpenButton));
            Assert.Equal("Giới thiệu", about.Title);
            Assert.True(about.IsVisible);
            Assert.EndsWith(" thẻ", shell.ViewModel.Summary, StringComparison.Ordinal);
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "NHÓM");
            Assert.Contains(window.GetVisualDescendants().OfType<DataGridColumnHeader>(), h => Equals(h.Content, "TÊN THẺ"));
            Assert.Equal("Đã lưu cài đặt", window.Toasts.Title);

            // a key the pack lacks keeps its English
            Assert.Equal("Hide all", shell.ViewModel.AllGroupsLabel);

            Lang.Current = new Lang();
            shell.Settle();

            Assert.Equal("Search", window.Toolbar.Search.PlaceholderText);
            Assert.Equal("About", about.Title);
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "GROUPS");
            about.Close();
        }
        finally
        {
            Lang.Current = english;
            Lang.UserDirectory = userDirectory;
        }
    }


    [AvaloniaFact]
    public async Task CancelKeepsTheLanguage()
    {
        var english = Lang.Current;
        var userDirectory = Lang.UserDirectory;
        try
        {
            using var shell = await TestShell.OpenWithFileAsync(1400);
            InstallPack(shell);
            var (_, vm) = await OpenSettingsAsync(shell);

            vm.SelectedLanguage = vm.Languages.Single(l => !l.Pack.IsBuiltIn);
            vm.CancelCommand.Execute(null);
            shell.Settle();

            Assert.True(Lang.Current.IsBuiltIn);
            Assert.Equal(Lang.BUILT_IN, shell.Settings.Current.Language);
            Assert.Equal("Search", shell.Window.Toolbar.Search.PlaceholderText);
        }
        finally
        {
            Lang.Current = english;
            Lang.UserDirectory = userDirectory;
        }
    }


    [AvaloniaFact]
    public async Task TheBuiltInEnglishComesFirstAndCreditsItsAuthor()
    {
        var userDirectory = Lang.UserDirectory;
        try
        {
            using var shell = await TestShell.OpenWithFileAsync(1400);
            InstallPack(shell);
            var (settings, vm) = await OpenSettingsAsync(shell);

            Assert.True(vm.Languages[0].Pack.IsBuiltIn);
            Assert.Same(vm.Languages[0], vm.SelectedLanguage);
            Assert.Null(vm.Languages[0].EnglishName);
            Assert.Equal("Vietnamese", vm.Languages[1].EnglishName);
            Assert.Equal("Contributors: Duong Dieu Phap", vm.LanguageContributors);
            Assert.Contains(settings.GetVisualDescendants().OfType<TextBlock>(), t => t.Name == "ContributorsText" && t.IsEffectivelyVisible && t.Text == vm.LanguageContributors);
            settings.Close();
        }
        finally
        {
            Lang.UserDirectory = userDirectory;
        }
    }

    #endregion // Tests


    #region Private methods

    private static void InstallPack(TestShell shell)
    {
        var dir = Directory.CreateDirectory(Path.Combine(shell.Folder, Lang.FOLDER)).FullName;
        File.WriteAllText(Path.Combine(dir, PACK), PACK_JSON);
        Lang.UserDirectory = dir;
    }


    private static async Task<(SettingsWindow Window, SettingsViewModel ViewModel)> OpenSettingsAsync(TestShell shell)
    {
        shell.Window.ShowSettings();
        shell.Settle(200);

        var window = shell.Window.OwnedWindows.OfType<SettingsWindow>().Single(w => w.IsVisible);
        await shell.WaitForAsync(() => window.ViewModel.Languages.Count == 2);
        return (window, window.ViewModel);
    }

    #endregion // Private methods
}
