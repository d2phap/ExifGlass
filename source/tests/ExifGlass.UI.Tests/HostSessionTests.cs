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
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ExifGlass.Core.Editing;
using ExifGlass.Core.Export;
using ExifGlass.Core.Settings;
using ExifGlass.Integration;
using ExifGlass.UI.Controls;
using ExifGlass.ViewModels;
using Xunit;

namespace ExifGlass.UI.Tests;

/// <summary>
/// P10: a host's requests through S6's policy into the window, D8 once per burst, the SHUTDOWN exit path and D29's recovery file.
/// </summary>
public sealed class HostSessionTests : IDisposable
{
    #region Fields and constants

    private readonly string _folder = Directory.CreateTempSubdirectory("eg-host-").FullName;
    private readonly FakeReader _reader = new();
    private readonly FakeHost _host = new();
    private readonly FakeWindow _window = new();
    private readonly AskingShell _shell = new();
    private readonly SettingsStore _settings;
    private readonly MainViewModel _vm;
    private int _exifToolStops;

    #endregion // Fields and constants


    #region Constructors

    public HostSessionTests()
    {
        _settings = new SettingsStore(_folder);
        _vm = new MainViewModel(_settings, _reader) { Shell = _shell };
    }

    #endregion // Constructors


    #region Tests

    [AvaloniaFact]
    public async Task ReadsALoneChangeAtOnceAndOnlyTheLastOfABurst()
    {
        using var session = Start();

        _host.Navigate("a.jpg");
        Assert.Equal(1, _reader.Reads);

        foreach (var path in new[] { "b.jpg", "c.jpg", "d.jpg" }) _host.Navigate(path);
        await WaitForAsync(() => _vm.Document?.SourcePath == "d.jpg");

        Assert.Equal(2, _reader.Reads);
    }


    [AvaloniaFact]
    public async Task AdoptsTheReadInFlightAndSkipsTheUnchangedFileOnScreen()
    {
        using var session = Start();
        var file = Touch("photo.jpg");

        _reader.Hold(file);
        _host.Request(file);
        _host.Request(file);
        Assert.Equal(1, _reader.Reads);

        _reader.Release(file);
        await WaitForAsync(() => _vm.Document?.SourcePath == file);
        _host.Request(file);
        Assert.Equal(1, _reader.Reads);

        // a file saved since is read again
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(1));
        _host.Request(file);
        Assert.Equal(2, _reader.Reads);
    }


    [AvaloniaFact]
    public async Task AsksOnceForABurstWithEditsStagedThenOpensTheNewest()
    {
        using var session = Start();
        await OpenWithEditAsync("a.jpg");

        _shell.Hold();
        _host.Navigate("b.jpg");
        _host.Navigate("c.jpg");
        _host.Request("d.jpg");
        Pump();
        Assert.Single(_shell.Asked);

        _shell.Answer(DialogAnswer.Secondary);
        await WaitForAsync(() => _vm.Document?.SourcePath == "d.jpg");
        Assert.Single(_shell.Asked);
        Assert.False(_vm.HasPending);
        Assert.Equal(2, _reader.Reads);
    }


    [AvaloniaFact]
    public async Task CancelKeepsTheFileAndItsEdits()
    {
        using var session = Start();
        await OpenWithEditAsync("a.jpg");

        _shell.Hold();
        _host.Navigate("b.jpg");
        Pump();
        _shell.Answer(DialogAnswer.Cancel);
        Pump(150);

        Assert.Equal("a.jpg", _vm.Document?.SourcePath);
        Assert.True(_vm.HasPending);
        Assert.Equal(1, _reader.Reads);
    }


    [AvaloniaFact]
    public async Task ShutdownKeepsTheStagedEditsAndClosesWithoutAsking()
    {
        using var session = Start();
        await OpenWithEditAsync(Touch("photo.jpg"));

        _host.Close(isShutdown: true);
        await WaitForAsync(() => _window.ClosedForHost);

        Assert.Empty(_shell.Asked);
        Assert.Equal(1, _exifToolStops);
        var kept = RecoveryFile.Load(_folder);
        Assert.NotNull(kept);
        Assert.Equal(_vm.Document!.SourcePath, kept.SourcePath);
        Assert.Equal(1, kept.PendingCount);
        Assert.True(kept.StripAll);

        // nothing more is read once ImageGlass said SHUTDOWN
        _host.Request("late.jpg");
        Pump();
        Assert.Equal(1, _reader.Reads);
    }


    [AvaloniaFact]
    public async Task ShutdownWithNothingStagedWritesNoRecoveryFile()
    {
        using var session = Start();
        _host.Request("a.jpg");
        await WaitForAsync(() => _vm.HasDocument);

        _host.Close(isShutdown: true);
        await WaitForAsync(() => _window.ClosedForHost);

        Assert.Null(RecoveryFile.Load(_folder));
    }


    [AvaloniaFact]
    public async Task RestoresTheKeptEditsAtTheNextStartAndKeepsThatFile()
    {
        var file = Touch("photo.jpg");
        var key = TestDocuments.Make(file).Tags[3].Key;
        var info = new FileInfo(file);
        var edits = EditSet.Empty.Delete(key, "EXIF:Tag3", "Value 3").WithRemoveGps(true);
        RecoveryFile.Save(_folder, RecoveryFile.Create(file, info.Length, info.LastWriteTimeUtc, edits, DateTimeOffset.UtcNow));

        using var session = new HostSession(_host, _vm, _window, StopExifTool);
        _shell.Hold();
        var offer = session.OfferRecoveryAsync();
        session.Start();
        _host.Request("other.jpg");
        Pump();

        var asked = Assert.Single(_shell.Asked);
        Assert.Equal("Restore changes to photo.jpg?", asked.Title);
        Assert.Equal("2 changes weren't saved when ImageGlass closed ExifGlass. Discard drops them.", asked.Body);
        Assert.Equal(("Restore", "Discard", "Not now"), (asked.PrimaryText, asked.SecondaryText, asked.CancelText));

        _shell.Answer(DialogAnswer.Primary);
        await offer;

        Assert.Equal(file, _vm.Document?.SourcePath);
        Assert.Equal(2, _vm.PendingCount);
        Assert.Equal(EditState.Deleted, _vm.Edits.StateOf(key));
        Assert.Null(RecoveryFile.Load(_folder));
        Assert.Equal(1, _reader.Reads);
        Assert.Contains(_shell.Toasts, t => t.Title == "Restored 2 changes");
    }


    [AvaloniaFact]
    public async Task DiscardDropsTheKeptEditsAndOpensWhatTheHostAskedFor()
    {
        var file = Touch("photo.jpg");
        RecoveryFile.Save(_folder, RecoveryFile.Create(file, 0, null, EditSet.Empty.WithStripAll(true), DateTimeOffset.UtcNow));

        using var session = new HostSession(_host, _vm, _window, StopExifTool);
        _shell.Hold();
        var offer = session.OfferRecoveryAsync();
        session.Start();
        _host.Request("other.jpg");

        // its size and time differ from the kept ones
        Assert.Contains("the file has changed since", Assert.Single(_shell.Asked).Body);
        _shell.Answer(DialogAnswer.Secondary);
        await offer;
        await WaitForAsync(() => _vm.Document?.SourcePath == "other.jpg");

        Assert.Null(RecoveryFile.Load(_folder));
        Assert.False(_vm.HasPending);
    }


    [AvaloniaFact]
    public async Task NotNowKeepsTheFileForTheNextStart()
    {
        RecoveryFile.Save(_folder, RecoveryFile.Create(Touch("photo.jpg"), 0, null, EditSet.Empty.WithStripAll(true), DateTimeOffset.UtcNow));

        using var session = new HostSession(_host, _vm, _window, StopExifTool);
        _shell.Hold();
        var offer = session.OfferRecoveryAsync();
        session.Start();
        _shell.Answer(DialogAnswer.Cancel);
        await offer;

        Assert.NotNull(RecoveryFile.Load(_folder));
        Assert.False(_vm.HasDocument);
    }


    [AvaloniaFact]
    public async Task SaysSoWhenTheKeptFileIsGone()
    {
        RecoveryFile.Save(_folder, RecoveryFile.Create(Path.Combine(_folder, "gone.jpg"), 0, null, EditSet.Empty.WithStripAll(true), DateTimeOffset.UtcNow));

        using var session = new HostSession(_host, _vm, _window, StopExifTool);
        await session.OfferRecoveryAsync();

        Assert.Empty(_shell.Asked);
        Assert.Contains(_shell.Toasts, t => t.Title == "Couldn't restore unsaved changes" && t.Subtitle == "gone.jpg was moved or deleted");
        Assert.Null(RecoveryFile.Load(_folder));
    }


    [AvaloniaFact]
    public void ActivatesForExecuteAndClosesAsTheUserWouldForImageGlass9()
    {
        using var session = Start();

        _host.Activate();
        _host.Close(isShutdown: false);

        Assert.Equal(1, _window.Activations);
        Assert.True(_window.Closed);
        Assert.False(_window.ClosedForHost);
        Assert.Equal(0, _exifToolStops);
    }

    #endregion // Tests


    #region Public methods

    public void Dispose()
    {
        _settings.FlushAsync().GetAwaiter().GetResult();
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // a save racing the delete leaves a temp folder behind, nothing more
        }
    }

    #endregion // Public methods


    #region Private methods

    private HostSession Start()
    {
        var session = new HostSession(_host, _vm, _window, StopExifTool);
        session.Start();
        return session;
    }


    private ValueTask StopExifTool()
    {
        _exifToolStops++;
        return ValueTask.CompletedTask;
    }


    private string Touch(string name)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, [1, 2, 3]);
        return path;
    }


    private async Task OpenWithEditAsync(string path)
    {
        _host.Request(path);
        await WaitForAsync(() => _vm.Document?.SourcePath == path);
        _vm.StripAllCommand.Execute(null);
        Assert.True(_vm.HasPending);
    }


    private static void Pump(int ms = 30)
    {
        var frame = new DispatcherFrame();
        DispatcherTimer.RunOnce(() => frame.Continue = false, TimeSpan.FromMilliseconds(ms));
        Dispatcher.UIThread.PushFrame(frame);
    }


    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            Pump(10);
            await Task.Delay(5);
        }

        Assert.True(condition());
    }

    #endregion // Private methods


    #region Nested types

    private sealed class FakeHost : IImageSourceHost
    {
        public event EventHandler<FileRequestedEventArgs>? FileRequested;

        public event EventHandler? ActivateRequested;

        public event EventHandler<CloseRequestedEventArgs>? CloseRequested;

        public void Navigate(string path) => FileRequested?.Invoke(this, new FileRequestedEventArgs(path, isNavigation: true));

        public void Request(string path) => FileRequested?.Invoke(this, new FileRequestedEventArgs(path, isNavigation: false));

        public void Activate() => ActivateRequested?.Invoke(this, EventArgs.Empty);

        public void Close(bool isShutdown) => CloseRequested?.Invoke(this, new CloseRequestedEventArgs(isShutdown));

        public void Start()
        {
        }

        public void Dispose()
        {
        }
    }


    private sealed class FakeWindow : IHostWindow
    {
        public int Activations { get; private set; }

        public bool Closed { get; private set; }

        public bool ClosedForHost { get; private set; }

        public void ActivateFromHost() => Activations++;

        public void Close() => Closed = true;

        public void CloseForHost() => ClosedForHost = true;
    }


    // a dialog stays up until the test answers it
    private sealed class AskingShell : IShellView
    {
        private TaskCompletionSource<DialogAnswer>? _pending;

        public List<DialogRequest> Asked { get; } = [];

        public List<(string Title, string? Subtitle)> Toasts { get; } = [];

        public void Hold() => _pending = new TaskCompletionSource<DialogAnswer>();

        public void Answer(DialogAnswer answer) => _pending!.TrySetResult(answer);

        public Task<DialogAnswer> AskAsync(DialogRequest request)
        {
            Asked.Add(request);
            return _pending?.Task ?? Task.FromResult(DialogAnswer.Cancel);
        }

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

        public Task CopyTextAsync(string text) => Task.CompletedTask;

        public Task<SaveTarget?> PickSaveFileAsync(string suggestedName, ExportFormat format) => Task.FromResult<SaveTarget?>(null);
    }

    #endregion // Nested types
}
