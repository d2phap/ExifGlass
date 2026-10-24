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
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using ExifGlass.Core.Export;
using ExifGlass.Core.Settings;
using ExifGlass.UI.Controls;
using ExifGlass.UI.Tokens;
using ExifGlass.UI.Windows;
using ExifGlass.ViewModels;
using ExifGlass.Windows;
using Xunit;

namespace ExifGlass.UI.Tests;

/// <summary>
/// A main window on a fake reader and its own settings folder, with motion frozen.
/// </summary>
public sealed class TestShell : IDisposable
{
    #region Fields and constants

    public const double HEIGHT = 860;

    #endregion // Fields and constants


    #region Constructors

    private TestShell(double width, IWindowPlacementStore? placements, ThemeVariant? theme)
    {
        MotionTokens.IsFrozen = true;
        Folder = Directory.CreateTempSubdirectory("eg-shell-").FullName;
        Settings = new SettingsStore(Folder);
        ViewModel = new MainViewModel(Settings, Reader);
        Window = new MainWindow(ViewModel, placements ?? new MemoryPlacementStore()) { Width = width, Height = HEIGHT, RequestedThemeVariant = theme };
        Window.Show();
        Settle();
    }

    #endregion // Constructors


    #region Properties

    public FakeReader Reader { get; } = new();

    /// <summary>
    /// Gets the run's own folder: its settings and any file a test needs.
    /// </summary>
    public string Folder { get; }

    public SettingsStore Settings { get; }

    public MainViewModel ViewModel { get; }

    public MainWindow Window { get; }

    #endregion // Properties


    #region Public methods

    public static TestShell Open(double width, IWindowPlacementStore? placements = null, ThemeVariant? theme = null) => new(width, placements, theme);


    /// <summary>
    /// Opens a shell with a file loaded: the panels and their toggles wait for one.
    /// </summary>
    public static async Task<TestShell> OpenWithFileAsync(double width, ThemeVariant? theme = null, string file = "a.jpg")
    {
        var shell = Open(width, theme: theme);
        await shell.ViewModel.OpenFileAsync(file);
        await shell.WaitForAsync(() => shell.ViewModel.HasDocument);
        shell.Settle();

        return shell;
    }


    /// <summary>
    /// Runs the dispatcher for a while: a pushed frame runs its timers too (drag grace, scrim fade), which RunJobs alone doesn't.
    /// </summary>
    public void Settle(int ms = 120)
    {
        var frame = new DispatcherFrame();
        var ticker = new DispatcherTimer(TimeSpan.FromMilliseconds(15), DispatcherPriority.Background, (_, _) => AvaloniaHeadlessPlatform.ForceRenderTimerTick());
        ticker.Start();
        DispatcherTimer.RunOnce(() => frame.Continue = false, TimeSpan.FromMilliseconds(ms));
        Dispatcher.UIThread.PushFrame(frame);
        ticker.Stop();
    }


    public async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            Settle(10);
            await Task.Delay(5);
        }

        Assert.True(condition());
    }


    public void Dispose()
    {
        Window.Close();
        MotionTokens.IsFrozen = false;
        Settings.FlushAsync().GetAwaiter().GetResult();
        try
        {
            Directory.Delete(Folder, recursive: true);
        }
        catch (IOException)
        {
            // a save racing the delete leaves a temp folder behind, nothing more
        }
    }

    #endregion // Public methods
}


/// <summary>
/// A shell with no window: it records toasts and copies and saves to memory.
/// </summary>
internal sealed class RecordingShell : IShellView
{
    public List<(string Title, string? Subtitle)> Toasts { get; } = [];

    public List<string> Copied { get; } = [];

    public MemoryStream Saved { get; private set; } = new();

    public Task<string?> PickFileAsync() => Task.FromResult<string?>(null);

    public void ShowToast(string title, string? subtitle = null, ToastKind kind = ToastKind.Success) => Toasts.Add((title, subtitle));

    public void FocusSearch()
    {
    }

    public void ShowSettings()
    {
    }

    public void ShowAbout()
    {
    }

    public Task CopyTextAsync(string text)
    {
        Copied.Add(text);
        return Task.CompletedTask;
    }

    public Task<SaveTarget?> PickSaveFileAsync(string suggestedName, ExportFormat format)
    {
        Saved = new KeptStream();
        return Task.FromResult<SaveTarget?>(new SaveTarget(Saved, suggestedName));
    }

    public Task<SaveTarget?> PickSaveFileAsync(string suggestedName, string extension, string typeName)
    {
        Saved = new KeptStream();
        return Task.FromResult<SaveTarget?>(new SaveTarget(Saved, suggestedName));
    }

    /// <summary>
    /// Gets or sets the answer every dialog gets; the questions asked are kept.
    /// </summary>
    public DialogAnswer Answer { get; set; } = DialogAnswer.Cancel;

    public List<DialogRequest> Asked { get; } = [];

    public Task<DialogAnswer> AskAsync(DialogRequest request)
    {
        Asked.Add(request);
        return Task.FromResult(Answer);
    }

    // the export disposes its stream; the test still reads it
    private sealed class KeptStream : MemoryStream
    {
        protected override void Dispose(bool disposing)
        {
        }

        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
