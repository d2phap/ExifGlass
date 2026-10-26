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
using ImageGlass.SDK.Tools;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace ExifGlass.IpcHost;

/// <summary>
/// An ImageGlass tool entry: the executable and its argument template with &lt;file&gt;.
/// </summary>
public sealed record ToolSpec(string Exe, string Args);


/// <summary>
/// ImageGlass 10's side of the tool pipe as S6 read it from ToolProcessManager, ToolPipeServer and ExternalToolProxy, without the viewer.
/// </summary>
public sealed class Ig10Host : IAsyncDisposable
{
    #region Fields and constants

    private readonly HostLog _log;
    private readonly Channel<(string Json, string Type, string? Path)> _out = Channel.CreateUnbounded<(string, string, string?)>(new UnboundedChannelOptions { SingleReader = true });
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private Task? _writerTask;
    private Task? _readerTask;
    private string _toolDir = string.Empty;

    #endregion // Fields and constants


    #region Constructors

    public Ig10Host(HostLog log, string? pipeDir = null, bool barePipe = false)
    {
        _log = log;
        PipeName = CreatePipeName(pipeDir, barePipe);
    }

    #endregion // Constructors


    #region Properties

    public string PipeName { get; }

    public NamedPipeServerStream? Server { get; private set; }

    public Process? Client { get; private set; }

    public DateTime ClientStarted { get; private set; }

    public long LaunchTicks { get; private set; }

    public long ConnectedTicks { get; private set; }

    public bool? ClientPidVerified { get; private set; }

    public string? StderrPath { get; init; }

    /// <summary>
    /// The photo ImageGlass shows: what GET_PHOTO_METADATA answers.
    /// </summary>
    public string? CurrentPath { get; set; }

    public int MetaDelayMs { get; init; } = 5;

    public long LastSentTicks { get; private set; }

    #endregion // Properties


    #region Public methods

    // ToolProcessManager.CreatePipeName: bare on Windows and macOS, an absolute socket path under ~/.cache on Linux
    public static string CreatePipeName(string? pipeDir, bool barePipe)
    {
        var id = $"ig_{Guid.NewGuid():N}";
        if (barePipe || (!OperatingSystem.IsLinux() && pipeDir is null)) return id;

        var dir = pipeDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "ImageGlass", "tools");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, id);
    }


    public async Task<bool> LaunchAsync(ToolSpec tool, string currentFile, IReadOnlyDictionary<string, string> env, int connectTimeoutMs = 10_000)
    {
        CurrentPath = currentFile;
        _toolDir = Path.GetDirectoryName(tool.Exe) ?? string.Empty;
        Server = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

        var psi = new ProcessStartInfo
        {
            FileName = tool.Exe,
            UseShellExecute = false,
            WorkingDirectory = _toolDir,
            RedirectStandardError = true,
        };
        foreach (var arg in BuildArgumentList(tool.Args, currentFile)) psi.ArgumentList.Add(arg);
        psi.ArgumentList.Add("--pipe");
        psi.ArgumentList.Add(PipeName);
        foreach (var (k, v) in env) psi.Environment[k] = v;

        LaunchTicks = Stopwatch.GetTimestamp();
        Client = Process.Start(psi)!;
        ClientStarted = DateTime.Now;
        _ = CaptureStderrAsync(Client, StderrPath);
        _log.Write("LAUNCH", ("pid", Client.Id), ("pipe", PipeName), ("args", string.Join(" | ", psi.ArgumentList)));

        using var cts = new CancellationTokenSource(connectTimeoutMs);
        try
        {
            await Server.WaitForConnectionAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            _log.Write("CONNECT_TIMEOUT");
            KillStarted();
            return false;
        }

        ConnectedTicks = Stopwatch.GetTimestamp();
        if (Procs.PipeClientPid(Server) is { } clientPid) ClientPidVerified = clientPid == Client.Id;
        _log.Write("CONNECTED", ("ms", HostLog.Ms(LaunchTicks, ConnectedTicks)), ("pidVerified", ClientPidVerified));

        // ImageGlass 10 on Windows kills a client that isn't the process it started
        if (ClientPidVerified == false)
        {
            _log.Write("PID_REJECTED");
            KillStarted();
            await Server.DisposeAsync();
            return false;
        }

        _reader = new StreamReader(Server, leaveOpen: true);
        _writer = new StreamWriter(Server, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        _writerTask = Task.Run(WriteLoopAsync);
        _readerTask = Task.Run(ReadLoopAsync);
        return true;
    }


    /// <summary>
    /// What ExternalToolProxy sends once the tool is connected.
    /// </summary>
    public void SendInitAndExecute()
    {
        Send(MessageTypes.INIT, new ToolInitPayload
        {
            ToolId = "Tool_ExifGlass",
            DataDirectory = _toolDir,
            PipeName = PipeName,
            ThemeInfo = new ThemeInfo { IsDarkMode = true, AccentColor = "#FF0078D4", BackgroundColor = "#1E1E1E" },
        });
        Send(MessageTypes.EXECUTE);
    }


    public void PhotoChanged(string path)
    {
        CurrentPath = path;
        Send(MessageTypes.PHOTO_CHANGED, new PhotoChangedEventArgs { FilePath = path, Width = 1, Height = 1, Format = Path.GetExtension(path) });
    }


    public void Send(string type, object? payload = null, int? requestId = null)
    {
        var element = payload is null ? (JsonElement?)null : JsonSerializer.SerializeToElement(payload, payload.GetType(), ToolJsonContext.Default);
        var json = JsonSerializer.Serialize(new ToolMessage { Type = type, RequestId = requestId, Payload = element }, ToolJsonContext.Default.ToolMessage);
        _out.Writer.TryWrite((json, type, (payload as PhotoChangedEventArgs)?.FilePath));
    }


    /// <summary>
    /// ToolProcessManager.StopToolAsync: SHUTDOWN, 3 s grace, then Kill.
    /// </summary>
    public async Task<(double Ms, bool Killed)> StopToolAsync(int graceMs = 3000)
    {
        var start = Stopwatch.GetTimestamp();
        var killed = false;
        if (Client is { HasExited: false })
        {
            Send(MessageTypes.SHUTDOWN);
            using var cts = new CancellationTokenSource(graceMs);
            try
            {
                await Client.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                KillStarted();
                killed = true;
                await Client.WaitForExitAsync();
            }
        }

        var ms = HostLog.Ms(start, Stopwatch.GetTimestamp());
        _log.Write("STOPPED", ("ms", ms), ("killed", killed));
        await ClosePipeAsync();
        return (ms, killed);
    }


    /// <summary>
    /// Drops the pipe without SHUTDOWN, as a host that closes its end cleanly.
    /// </summary>
    public async Task ClosePipeAsync()
    {
        _out.Writer.TryComplete();
        if (_writerTask is not null) await _writerTask.WaitAsync(TimeSpan.FromSeconds(2));
        if (Server is not null) await Server.DisposeAsync();
        _log.Write("PIPE_CLOSED");
    }


    public async ValueTask DisposeAsync()
    {
        try
        {
            await ClosePipeAsync();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or TimeoutException)
        {
            // closed already
        }

        if (_readerTask is not null) await Task.WhenAny(_readerTask, Task.Delay(1000));
    }


    // ImageGlass BHelper.BuildArgumentList: tokenize, then <file> per token
    public static List<string> BuildArgumentList(string? template, string filePath)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(template)) return result;

        var token = new StringBuilder();
        var inQuotes = false;
        var hasToken = false;
        foreach (var ch in template)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                hasToken = true;
                continue;
            }

            if (!inQuotes && char.IsWhiteSpace(ch))
            {
                if (hasToken) result.Add(token.ToString().Replace("<file>", filePath, StringComparison.Ordinal));
                token.Clear();
                hasToken = false;
                continue;
            }

            token.Append(ch);
            hasToken = true;
        }

        if (hasToken) result.Add(token.ToString().Replace("<file>", filePath, StringComparison.Ordinal));
        return result;
    }

    #endregion // Public methods


    #region Private methods

    private async Task WriteLoopAsync()
    {
        try
        {
            while (await _out.Reader.WaitToReadAsync())
            {
                while (_out.Reader.TryRead(out var m))
                {
                    await _writer!.WriteLineAsync(m.Json);
                    if (m.Type == MessageTypes.PHOTO_CHANGED) LastSentTicks = Stopwatch.GetTimestamp();
                    _log.Write("SENT", ("type", m.Type), ("path", m.Path));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            _log.Write("WRITE_FAILED", ("error", ex.GetType().Name));
        }
    }


    // ToolPipeServer answers one request at a time, the current photo read on ImageGlass's UI thread after a hop
    private async Task ReadLoopAsync()
    {
        try
        {
            while (await _reader!.ReadLineAsync() is { } line)
            {
                var msg = JsonSerializer.Deserialize(line, ToolJsonContext.Default.ToolMessage);
                if (msg?.Type != MessageTypes.GET_PHOTO_METADATA) continue;

                if (MetaDelayMs > 0) await Task.Delay(MetaDelayMs);
                var path = CurrentPath;
                _log.Write("META_REQUEST", ("requestId", msg.RequestId), ("reply", path));
                Send("RESPONSE", path is null ? null : new ToolPhotoMetadata
                {
                    FilePath = path,
                    FileName = Path.GetFileName(path),
                    FileExtension = Path.GetExtension(path),
                    FolderPath = Path.GetDirectoryName(path) ?? string.Empty,
                }, msg.RequestId);
            }

            _log.Write("PIPE_EOF");
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
        {
            _log.Write("PIPE_BROKEN", ("error", ex.GetType().Name));
        }
    }


    // ImageGlass kills the process it started, not its tree
    private void KillStarted()
    {
        try
        {
            Client?.Kill(entireProcessTree: false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // gone already
        }
    }


    private static async Task CaptureStderrAsync(Process process, string? path)
    {
        try
        {
            var text = await process.StandardError.ReadToEndAsync();
            if (path is not null && text.Length > 0) await File.WriteAllTextAsync(path, text);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            // the process went away
        }
    }

    #endregion // Private methods
}
