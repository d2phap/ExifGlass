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
using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Channels;

namespace ExifGlass.Core.ExifTool;

/// <summary>
/// One command's output: stdout bytes still in the pipe (valid only inside the parser), stderr text and the exit status.
/// </summary>
public readonly record struct ExifToolOutput(ReadOnlySequence<byte> Stdout, string Stderr, int? Status)
{
    #region Properties

    public string StdoutText => Stdout.IsSingleSegment ? Encoding.UTF8.GetString(Stdout.FirstSpan) : Encoding.UTF8.GetString(Stdout.ToArray());

    #endregion // Properties
}


/// <summary>
/// Reads one command's output while its bytes are still in the pipe.
/// </summary>
public delegate T ExifToolParser<out T>(in ExifToolOutput output, CancellationToken cancellationToken);


/// <summary>
/// How the daemon starts.
/// </summary>
public sealed class ExifToolDaemonOptions
{
    #region Properties

    public required string ExePath { get; init; }

    /// <summary>
    /// ExifGlass's ExifTool config; it only works as the very first argument of the process (S5).
    /// </summary>
    public string? ConfigPath { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Small files read once after -ver so the first real read finds the Perl modules loaded (S5: 52 to 38 ms).
    /// </summary>
    public IReadOnlyList<string> PrimingFiles { get; init; } = [];

    #endregion // Properties
}


/// <summary>
/// ExifTool couldn't start, stopped answering or failed in a way the app reports.
/// </summary>
public sealed class ExifToolException(string message, Exception? inner = null) : Exception(message, inner);


/// <summary>
/// One exiftool -stay_open process behind a serialized queue: framing, timeout, restart, job object, clean exit.
/// </summary>
public sealed class ExifToolDaemon : IAsyncDisposable
{
    #region Fields and constants

    private static readonly Encoding UTF8_NO_BOM = new UTF8Encoding(false);

    // four 64 KB segments cover a typical reply; a 2 MB workflow's other segments go back to the GC
    private static readonly BoundedMemoryPool PIPE_POOL = new(64 * 1024, 4);

    // ImageGlass kills its tools 3 s after SHUTDOWN (R21): the running command gets this long, then -stay_open False gets EXIT_GRACE
    private static readonly TimeSpan READ_GRACE = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan WRITE_GRACE = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan EXIT_GRACE = TimeSpan.FromMilliseconds(500);

    private readonly ExifToolDaemonOptions _options;
    private readonly Channel<WorkItem> _queue = Channel.CreateUnbounded<WorkItem>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Task _worker;

    private Process? _process;
    private Stream? _stdin;
    private PipeReader? _stdout;
    private Channel<StderrBlock>? _stderrBlocks;
    private string _nonce = string.Empty;
    private int _sequence;
    private volatile WorkItem? _running;
    private volatile bool _disposed;

    #endregion // Fields and constants


    #region Constructors

    public ExifToolDaemon(ExifToolDaemonOptions options)
    {
        _options = options;
        _worker = Task.Run(WorkerLoopAsync);
    }

    #endregion // Constructors


    #region Properties

    public ExifToolDaemonOptions Options => _options;

    public int ProcessId { get; private set; }

    /// <summary>
    /// How many times the process was restarted after a timeout or a crash.
    /// </summary>
    public int Restarts { get; private set; }

    /// <summary>
    /// The version -ver printed, once StartAsync has run.
    /// </summary>
    public string? Version { get; private set; }

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Queues one command; the parser runs on the worker thread while the bytes are still in the pipe. A write gets longer to finish when the daemon stops.
    /// </summary>
    public Task<T> ExecuteAsync<T>(IReadOnlyList<string> args, ExifToolParser<T> parser, CancellationToken cancellationToken = default, bool writes = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        foreach (var arg in args)
        {
            if (!ArgFile.CanEncode(arg)) throw new ArgumentException($"Argument can't be sent to ExifTool: {arg}", nameof(args));
        }

        var item = new WorkItem<T>(args, parser, cancellationToken, writes);
        if (!_queue.Writer.TryWrite(item)) throw new ObjectDisposedException(nameof(ExifToolDaemon));

        return item.Task;
    }


    /// <summary>
    /// Runs a command and returns stdout as text with stderr and the status.
    /// </summary>
    public Task<(string Stdout, string Stderr, int? Status)> RunTextAsync(IReadOnlyList<string> args, CancellationToken cancellationToken = default)
        => ExecuteAsync(args, static (in ExifToolOutput o, CancellationToken _) => (o.StdoutText, o.Stderr, o.Status), cancellationToken);


    /// <summary>
    /// Starts the process off the caller's thread, asks for the version, then primes the common modules.
    /// </summary>
    public async Task<string> StartAsync(CancellationToken cancellationToken = default)
    {
        var (stdout, _, _) = await RunTextAsync(["-ver"], cancellationToken).ConfigureAwait(false);
        Version = stdout.Trim();

        var priming = _options.PrimingFiles.Where(File.Exists).ToList();
        if (priming.Count > 0)
        {
            await ExecuteAsync<bool>(["-j", "-l", "-G0:1:4", "-H", "-fast", .. priming], static (in ExifToolOutput _, CancellationToken _) => true, cancellationToken)
                .ConfigureAwait(false);
        }

        return Version;
    }


    /// <summary>
    /// Stops within ImageGlass's budget (S6): queued commands are dropped, the running one may finish, then -stay_open False, and a kill when either takes too long.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _queue.Writer.TryComplete();

        var finished = true;
        try
        {
            await _worker.WaitAsync(_running?.Writes == true ? WRITE_GRACE : READ_GRACE).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            finished = false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // the worker reports its own errors through each command's task
        }

        var process = _process;
        if (process is null) return;

        // the stream belongs to the running command until it ends, so only an idle ExifTool is asked to exit
        try
        {
            if (finished && !process.HasExited && _stdin is not null)
            {
                await _stdin.WriteAsync("-stay_open\nFalse\n"u8.ToArray()).ConfigureAwait(false);
                await _stdin.FlushAsync().ConfigureAwait(false);

                using var grace = new CancellationTokenSource(EXIT_GRACE);
                await process.WaitForExitAsync(grace.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or InvalidOperationException or ObjectDisposedException)
        {
            // ExifTool is gone or didn't answer in time: Kill below
        }

        Kill();
    }

    #endregion // Public methods


    #region Private methods

    // "await foreach" would keep the last work item, and its multi-MB result, until the next command arrives
    private async Task WorkerLoopAsync()
    {
        while (await _queue.Reader.WaitToReadAsync().ConfigureAwait(false))
        {
            Task? running;
            while ((running = RunNext()) is not null)
            {
                await running.ConfigureAwait(false);
                running = null;
            }
        }
    }


    private Task? RunNext()
    {
        _running = null;
        if (!_queue.Reader.TryRead(out var item)) return null;
        if (_disposed || item.Token.IsCancellationRequested)
        {
            item.Cancel();
            return Task.CompletedTask;
        }

        _running = item;
        return item.RunAsync(this);
    }


    private async Task<T> RunOnceAsync<T>(IReadOnlyList<string> args, ExifToolParser<T> parser, CancellationToken cancellationToken)
    {
        EnsureStarted();

        var id = _nonce + (++_sequence).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var sb = new StringBuilder((args.Count * 48) + 64);
        foreach (var arg in args) ArgFile.AppendLine(sb, arg);
        sb.Append("-echo4\n{err").Append(id).Append(":${status}}\n");
        sb.Append("-execute").Append(id).Append('\n');

        var sentinel = Encoding.ASCII.GetBytes("{ready" + id + "}");
        var bytes = UTF8_NO_BOM.GetBytes(sb.ToString());

        using var timeout = new CancellationTokenSource(_options.Timeout);
        try
        {
            await _stdin!.WriteAsync(bytes, timeout.Token).ConfigureAwait(false);
            await _stdin.FlushAsync(timeout.Token).ConfigureAwait(false);

            while (true)
            {
                var read = await _stdout!.ReadAsync(timeout.Token).ConfigureAwait(false);
                var buffer = read.Buffer;

                if (EndsWithSentinel(buffer, sentinel, out var payloadLength))
                {
                    var err = await ReadStderrAsync(id, timeout.Token).ConfigureAwait(false);
                    try
                    {
                        return parser(new ExifToolOutput(buffer.Slice(0, payloadLength), err.Text, err.Status), cancellationToken);
                    }
                    finally
                    {
                        _stdout.AdvanceTo(buffer.End);
                    }
                }

                if (read.IsCompleted) throw new IOException("ExifTool closed its output");
                _stdout.AdvanceTo(buffer.Start, buffer.End);
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw new TimeoutException($"ExifTool didn't answer within {_options.Timeout.TotalSeconds:0.#} s");
        }
    }


    // ExifTool prints nothing after {ready<id>} until the next command, so the sentinel is always the tail
    private static bool EndsWithSentinel(in ReadOnlySequence<byte> buffer, byte[] sentinel, out long payloadLength)
    {
        payloadLength = 0;
        var tailLength = (int)Math.Min(buffer.Length, sentinel.Length + 3);
        if (tailLength < sentinel.Length + 1) return false;

        Span<byte> tail = stackalloc byte[tailLength];
        buffer.Slice(buffer.Length - tailLength).CopyTo(tail);

        if (tail[^1] != (byte)'\n') return false;
        var end = tail.Length - 1;
        if (end > 0 && tail[end - 1] == (byte)'\r') end--;

        var start = end - sentinel.Length;
        if (start < 0 || !tail[start..end].SequenceEqual(sentinel)) return false;

        var absoluteStart = buffer.Length - tailLength + start;
        if (absoluteStart > 0 && (start == 0 || tail[start - 1] != (byte)'\n')) return false;

        payloadLength = absoluteStart;
        return true;
    }


    private async Task<StderrBlock> ReadStderrAsync(string id, CancellationToken cancellationToken)
    {
        while (true)
        {
            var block = await _stderrBlocks!.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (block.Id == id) return block;
        }
    }


    private static async Task StderrLoopAsync(Process process, Channel<StderrBlock> blocks)
    {
        var text = new StringBuilder();
        try
        {
            while (await process.StandardError.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (line.StartsWith("{err", StringComparison.Ordinal) && line.EndsWith('}'))
                {
                    var body = line[4..^1];
                    var colon = body.IndexOf(':');
                    var id = colon < 0 ? body : body[..colon];
                    int? status = colon >= 0 && int.TryParse(body.AsSpan(colon + 1), out var s) ? s : null;

                    if (id.Length > 0 && id.AsSpan().IndexOfAnyExceptInRange('0', '9') < 0)
                    {
                        blocks.Writer.TryWrite(new StderrBlock(id, status, text.ToString()));
                        text.Clear();
                        continue;
                    }
                }

                text.Append(line).Append('\n');
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // the process went away; the command waiting on this block times out or sees the closed stdout
        }

        blocks.Writer.TryComplete();
    }


    private void EnsureStarted()
    {
        if (_process is { HasExited: false }) return;
        Kill();

        // a command cut short by DisposeAsync must not start a new ExifTool that nothing would stop (R11)
        ObjectDisposedException.ThrowIf(_disposed, this);

        var psi = new ProcessStartInfo
        {
            FileName = _options.ExePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardErrorEncoding = UTF8_NO_BOM,
        };

        if (_options.ConfigPath is { } config && File.Exists(config))
        {
            psi.ArgumentList.Add("-config");
            psi.ArgumentList.Add(config);
        }

        psi.ArgumentList.Add("-charset");
        psi.ArgumentList.Add("filename=utf8");
        psi.ArgumentList.Add("-stay_open");
        psi.ArgumentList.Add("True");
        psi.ArgumentList.Add("-@");
        psi.ArgumentList.Add("-");

        Process process;
        try
        {
            process = Process.Start(psi) ?? throw new ExifToolException($"ExifTool didn't start: {_options.ExePath}");
        }
        catch (Win32Exception ex)
        {
            throw new ExifToolException($"ExifTool couldn't start: {_options.ExePath} ({ex.Message})", ex);
        }

        if (OperatingSystem.IsWindows())
        {
            try
            {
                ProcessJob.Bind(process);
            }
            catch (Win32Exception)
            {
                // a parent job that forbids nesting: ExifTool still runs, -stay_open False ends it on a clean exit
            }
        }

        try
        {
            process.PriorityClass = ProcessPriorityClass.BelowNormal;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            // normal priority is fine
        }

        _process = process;
        ProcessId = process.Id;
        _stdin = process.StandardInput.BaseStream;
        _stdout = PipeReader.Create(process.StandardOutput.BaseStream, new StreamPipeReaderOptions(PIPE_POOL, bufferSize: 64 * 1024, minimumReadSize: 16 * 1024));
        _stderrBlocks = Channel.CreateUnbounded<StderrBlock>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        _ = Task.Run(() => StderrLoopAsync(process, _stderrBlocks));

        // a late reply from a killed process can't be taken for the new one's (S5)
        _nonce = Random.Shared.Next(100_000, 999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        _sequence = 0;
    }


    private void Kill()
    {
        // DisposeAsync and a command cut short by it may both get here
        var process = Interlocked.Exchange(ref _process, null);
        if (process is null) return;

        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            process.WaitForExit(2000);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // already gone
        }

        try
        {
            _stdout?.Complete();
        }
        catch (InvalidOperationException)
        {
            // a reader still in use completes on its own
        }

        process.Dispose();
        _stdin = null;
        _stdout = null;
    }


    private static bool IsRestartable(Exception ex) => ex is IOException or TimeoutException or ObjectDisposedException;

    #endregion // Private methods


    #region Nested types

    private readonly record struct StderrBlock(string Id, int? Status, string Text);


    private abstract class WorkItem(CancellationToken token, bool writes)
    {
        public CancellationToken Token { get; } = token;

        public bool Writes { get; } = writes;

        public abstract Task RunAsync(ExifToolDaemon daemon);

        public abstract void Cancel();
    }


    private sealed class WorkItem<T> : WorkItem
    {
        private readonly IReadOnlyList<string> _args;
        private readonly ExifToolParser<T> _parser;
        private readonly TaskCompletionSource<T> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenRegistration _registration;

        public WorkItem(IReadOnlyList<string> args, ExifToolParser<T> parser, CancellationToken token, bool writes) : base(token, writes)
        {
            _args = args;
            _parser = parser;

            // the caller moves on at once; a command already written is still read to the end so the stream stays in sync
            _registration = token.Register(static s => ((WorkItem<T>)s!).Cancel(), this);
        }

        public Task<T> Task => _tcs.Task;

        public override void Cancel() => _tcs.TrySetCanceled(Token);

        public override async Task RunAsync(ExifToolDaemon daemon)
        {
            try
            {
                for (var attempt = 1; ; attempt++)
                {
                    try
                    {
                        _tcs.TrySetResult(await daemon.RunOnceAsync(_args, _parser, Token).ConfigureAwait(false));
                        return;
                    }
                    catch (OperationCanceledException) when (Token.IsCancellationRequested)
                    {
                        Cancel();
                        return;
                    }
                    catch (Exception ex) when (IsRestartable(ex))
                    {
                        daemon.Kill();
                        if (attempt >= 2 || daemon._disposed)
                        {
                            _tcs.TrySetException(ex);
                            return;
                        }

                        daemon.Restarts++;
                    }
                    catch (Exception ex)
                    {
                        _tcs.TrySetException(ex);
                        return;
                    }
                }
            }
            finally
            {
                await _registration.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    #endregion // Nested types
}
