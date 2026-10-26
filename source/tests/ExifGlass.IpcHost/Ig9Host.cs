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
#if WINDOWS
using ImageGlass.Tools;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;

namespace ExifGlass.IpcHost;

/// <summary>
/// ImageGlass 9's side (Local.OpenPipedToolAsync / ClosePipedToolAsync) on the same ImageGlass.Tools PipeServer it uses.
/// </summary>
public sealed class Ig9Host : IDisposable
{
    #region Fields and constants

    private readonly HostLog _log;

    #endregion // Fields and constants


    #region Constructors

    public Ig9Host(HostLog log)
    {
        _log = log;
        Server = new PipeServer(ImageGlassTool.CreateServerName(Code), PipeDirection.InOut);
        Server.ClientDisconnected += (_, _) => _log.Write("IG9_CLIENT_DISCONNECTED");
    }

    #endregion // Constructors


    #region Properties

    public string Code { get; } = Guid.NewGuid().ToString("N");

    public PipeServer Server { get; }

    public Process? Client { get; private set; }

    public DateTime ClientStarted { get; private set; }

    public long LastSentTicks { get; private set; }

    #endregion // Properties


    #region Public methods

    public async Task<bool> LaunchAsync(string exe, string file, IReadOnlyDictionary<string, string> env)
    {
        Server.Start();

        // ImageGlass 9 shell-executes "<args> --ig-tool-pipe-code=<code>"
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"\"{file}\" {ImageGlassTool.PIPE_CODE_CMD_LINE}{Code}",
            UseShellExecute = false,
        };
        foreach (var (k, v) in env) psi.Environment[k] = v;
        Client = Process.Start(psi)!;
        ClientStarted = DateTime.Now;
        _log.Write("LAUNCH", ("pid", Client.Id), ("args", psi.Arguments));

        try
        {
            await Server.WaitForConnectionAsync().WaitAsync(TimeSpan.FromSeconds(10));
            _log.Write("CONNECTED");
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or InvalidOperationException)
        {
            // Start() already began a wait on the same stream, which may have connected
            for (var i = 0; i < 100 && !Server.ServerStream.IsConnected; i++) await Task.Delay(50);
            _log.Write("CONNECTED_STATE", ("connected", Server.ServerStream.IsConnected));
            return Server.ServerStream.IsConnected;
        }
    }


    // Local.RaiseImageLoadingEvent: fire and forget, as ImageGlass 9 does
    public void ImageLoading(int index, string path)
    {
        var json = JsonSerializer.Serialize(new IgImageLoadingEventArgs { Index = index - 1, NewIndex = index, FilePath = path },
            IgImageLoadingEventArgsJsonContext.Default.IgImageLoadingEventArgs);
        _ = Server.SendAsync(ImageGlassEvents.IMAGE_LOADING, json);
        LastSentTicks = Stopwatch.GetTimestamp();
        _log.Write("SENT", ("type", "IMAGE_LOADING"), ("path", path));
    }


    // Local.ClosePipedToolAsync: TOOL_TERMINATE, then stop and dispose the server
    public async Task<(double Ms, bool Killed)> TerminateAsync(int waitMs = 5000)
    {
        var start = Stopwatch.GetTimestamp();
        if (Server.ServerStream.IsConnected)
        {
            await Server.SendAsync(ImageGlassEvents.TOOL_TERMINATE, JsonSerializer.Serialize(new IgToolTernimatingEventArgs(),
                IgToolTernimatingEventArgsJsonContext.Default.IgToolTernimatingEventArgs));
        }

        Server.Stop();
        Server.Dispose();
        return await WaitExitAsync(start, waitMs);
    }


    public async Task<(double Ms, bool Killed)> WaitExitAsync(long start, int waitMs)
    {
        var killed = false;
        using var cts = new CancellationTokenSource(waitMs);
        try
        {
            await Client!.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Procs.TryKill(Client!.Id);
            killed = true;
        }

        return (HostLog.Ms(start, Stopwatch.GetTimestamp()), killed);
    }


    public void Dispose()
    {
        try
        {
            Server.Dispose();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // disposed already
        }
    }

    #endregion // Public methods
}
#endif
