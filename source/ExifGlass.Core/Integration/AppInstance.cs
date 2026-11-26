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
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace ExifGlass.Core.Integration;

/// <summary>
/// Single instance, after ImageGlass's AppInstance: the first launch holds a named mutex (Windows) or a file lock (macOS, Linux) and listens on a pipe; a later launch sends it its arguments.
/// </summary>
public sealed class AppInstance : IDisposable
{
    #region Fields and constants

    public static readonly TimeSpan CONNECT_TIMEOUT = TimeSpan.FromSeconds(1);

    // a launch writes its arguments and closes; one that stalls or sends too much is dropped
    private static readonly TimeSpan READ_TIMEOUT = TimeSpan.FromSeconds(2);
    private const int MAX_MESSAGE_BYTES = 64 * 1024;

    // paths never hold a NUL on any OS
    private const char SEPARATOR = '\0';

    private readonly string _mutexName;
    private readonly string _pipeName;
    private readonly string? _lockPath;
    private readonly Lock _lock = new();
    private readonly List<string[]> _queued = [];
    private Action<string[]>? _handler;
    private CancellationTokenSource? _listening;
    private Mutex? _mutex;
    private FileStream? _lockFile;
    private bool _disposed;

    #endregion // Fields and constants


    #region Constructors

    public AppInstance(string settingsDirectory)
    {
        var id = IdFor(settingsDirectory);
        _mutexName = $@"Local\ExifGlass-{id}";

        if (OperatingSystem.IsWindows())
        {
            _pipeName = $"ExifGlass-{id}";
        }
        else
        {
            // a rooted name is the socket path itself; short, as macOS caps it at 104 bytes
            var dir = UnixDirectory();
            _pipeName = Path.Combine(dir, $"eg-{id}.sock");
            _lockPath = Path.Combine(dir, $"eg-{id}.lock");
        }
    }

    #endregion // Constructors


    #region Properties

    /// <summary>
    /// True once this process holds the slot that later launches send to.
    /// </summary>
    public bool IsFirst { get; private set; }

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// One instance per user and settings folder: a portable copy or an EG_SETTINGS_DIR run is its own.
    /// </summary>
    public static string IdFor(string settingsDirectory)
    {
        var dir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(settingsDirectory));
        if (!OperatingSystem.IsLinux()) dir = dir.ToUpperInvariant();

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserName + SEPARATOR + dir));
        return Convert.ToHexStringLower(hash.AsSpan(0, 6));
    }


    /// <summary>
    /// Takes the slot; false while another launch holds it, or when the OS won't give it.
    /// </summary>
    public bool TryClaim()
    {
        if (IsFirst) return true;
        if (_disposed) return false;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                // never owned, only created: the name lives while the first instance keeps its handle
                var mutex = new Mutex(false, _mutexName, out var created);
                if (!created)
                {
                    mutex.Dispose();
                    return false;
                }

                _mutex = mutex;
            }
            else
            {
                // the OS drops the lock with the process, so a crash never blocks the next launch
                _lockFile = new FileStream(_lockPath!, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or WaitHandleCannotBeOpenedException)
        {
            return false;
        }

        IsFirst = true;
        return true;
    }


    /// <summary>
    /// Opens the pipe for later launches; what they send waits for <see cref="Attach"/>.
    /// </summary>
    public void Listen()
    {
        if (!IsFirst || _listening is not null) return;

        // a crashed first instance leaves its socket file, and binding to it fails
        if (!OperatingSystem.IsWindows()) TryDelete(_pipeName);

        var listening = _listening = new CancellationTokenSource();
        _ = Task.Run(() => ListenAsync(listening.Token));
    }


    /// <summary>
    /// Sets who gets later launches' arguments, on a pool thread; what came before is handed over now.
    /// </summary>
    public void Attach(Action<string[]> handler)
    {
        lock (_lock)
        {
            _handler = handler;
            foreach (var args in _queued) handler(args);
            _queued.Clear();
        }
    }


    /// <summary>
    /// Hands this launch's arguments to the first instance; false when none took them in time.
    /// </summary>
    public bool Send(IReadOnlyList<string> args)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect((int)CONNECT_TIMEOUT.TotalMilliseconds);
            client.Write(Encode(args));
            client.Flush();

            // returns once the first instance has read it all, or fails when it dropped the read
            if (OperatingSystem.IsWindows()) client.WaitForPipeDrain();
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }


    /// <summary>
    /// Closes the pipe and frees the slot, so the next launch opens its own window.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _listening?.Cancel();
        _mutex?.Dispose();
        _lockFile?.Dispose();
        _mutex = null;
        _lockFile = null;
        IsFirst = false;
    }

    #endregion // Public methods


    #region Internal methods

    internal static byte[] Encode(IReadOnlyList<string> args)
    {
        var text = new StringBuilder();
        foreach (var arg in args) text.Append(arg).Append(SEPARATOR);
        return Encoding.UTF8.GetBytes(text.ToString());
    }


    internal static string[] Decode(ReadOnlySpan<byte> bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        return text.Length == 0 ? [] : text.TrimEnd(SEPARATOR).Split(SEPARATOR);
    }

    #endregion // Internal methods


    #region Private methods

    private async Task ListenAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            NamedPipeServerStream server;
            try
            {
                server = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // the name is taken or the folder unusable: later launches find no pipe and open their own window
                return;
            }

            await using (server.ConfigureAwait(false))
            {
                try
                {
                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                    if (await ReadAsync(server, token).ConfigureAwait(false) is { } args) Deliver(args);
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (IOException)
                {
                }
            }
        }
    }


    private static async Task<string[]?> ReadAsync(Stream stream, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(READ_TIMEOUT);

        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int read;
        while ((read = await stream.ReadAsync(chunk, timeout.Token).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > MAX_MESSAGE_BYTES) return null;
            buffer.Write(chunk, 0, read);
        }

        return Decode(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
    }


    private void Deliver(string[] args)
    {
        lock (_lock)
        {
            if (_handler is { } handler) handler(args);
            else _queued.Add(args);
        }
    }


    // a Flatpak sandbox may get its own /tmp; this folder is shared by every instance of the app
    private static string UnixDirectory()
    {
        if (Environment.GetEnvironmentVariable("FLATPAK_ID") is { Length: > 0 } app
            && Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") is { Length: > 0 } runtime
            && Path.Combine(runtime, "app", app) is var dir
            && Directory.Exists(dir)) return dir;

        return Path.GetTempPath();
    }


    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    #endregion // Private methods
}
