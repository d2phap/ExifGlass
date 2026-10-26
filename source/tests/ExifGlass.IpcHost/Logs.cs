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
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ExifGlass.IpcHost;

/// <summary>
/// The host's own JSON-lines log, in the app's EG_IPC_LOG format: t is Stopwatch.GetTimestamp, so both logs subtract.
/// </summary>
public sealed class HostLog : IDisposable
{
    #region Fields and constants

    private readonly Lock _lock = new();
    private readonly FileStream _file;

    #endregion // Fields and constants


    #region Constructors

    public HostLog(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        _file = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
    }

    #endregion // Constructors


    #region Public methods

    public void Write(string name, params ReadOnlySpan<(string Key, object? Value)> fields)
    {
        var ticks = Stopwatch.GetTimestamp();
        var sb = new StringBuilder("{\"t\":").Append(ticks).Append(",\"e\":").Append(JsonSerializer.Serialize(name, HostJsonContext.Default.String));
        foreach (var (key, value) in fields)
        {
            sb.Append(",\"").Append(key).Append("\":").Append(value switch
            {
                null => "null",
                bool b => b ? "true" : "false",
                int or long => value.ToString(),
                double d => d.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                _ => JsonSerializer.Serialize(value.ToString() ?? string.Empty, HostJsonContext.Default.String),
            });
        }

        sb.Append("}\n");
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        lock (_lock)
        {
            _file.Write(bytes);
            _file.Flush();
        }
    }


    public static double Ms(long fromTicks, long toTicks) => (toTicks - fromTicks) * 1000.0 / Stopwatch.Frequency;


    public void Dispose() => _file.Dispose();

    #endregion // Public methods
}


/// <summary>
/// One line of the app's EG_IPC_LOG.
/// </summary>
public sealed record LogEvent(long T, string E, JsonElement Data)
{
    #region Public methods

    public string? Str(string key) => Data.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public long Long(string key) => Data.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;

    public double Dbl(string key) => Data.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;

    public bool Bool(string key) => Data.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.True;

    #endregion // Public methods
}


/// <summary>
/// Tails the app's log while it is being written.
/// </summary>
public sealed class AppLog(string path)
{
    #region Fields and constants

    private readonly List<LogEvent> _events = [];
    private long _offset;

    #endregion // Fields and constants


    #region Properties

    public string Path { get; } = path;

    public IReadOnlyList<LogEvent> Events
    {
        get
        {
            Refresh();
            return _events;
        }
    }

    #endregion // Properties


    #region Public methods

    public LogEvent? Last(string name, Func<LogEvent, bool>? where = null)
    {
        var list = Events;
        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (list[i].E == name && (where is null || where(list[i]))) return list[i];
        }

        return null;
    }


    public LogEvent? First(string name, Func<LogEvent, bool>? where = null) => Events.FirstOrDefault(e => e.E == name && (where is null || where(e)));


    public int Count(string name, long afterTicks = 0) => Events.Count(e => e.E == name && e.T >= afterTicks);


    public async Task<LogEvent?> WaitForAsync(string name, Func<LogEvent, bool>? where, int timeoutMs, long afterTicks = 0)
    {
        var start = Stopwatch.GetTimestamp();
        while (true)
        {
            var hit = Events.LastOrDefault(e => e.E == name && e.T >= afterTicks && (where is null || where(e)));
            if (hit is not null) return hit;
            if (HostLog.Ms(start, Stopwatch.GetTimestamp()) > timeoutMs) return null;
            await Task.Delay(10);
        }
    }

    #endregion // Public methods


    #region Private methods

    private void Refresh()
    {
        if (!File.Exists(Path)) return;

        using var fs = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var length = fs.Length;
        if (length <= _offset) return;

        fs.Seek(_offset, SeekOrigin.Begin);
        var bytes = new byte[length - _offset];
        fs.ReadExactly(bytes);

        // whole lines only: the app may be mid-line, even mid-character
        var end = Array.LastIndexOf(bytes, (byte)'\n');
        if (end < 0) return;

        _offset += end + 1;
        foreach (var line in Encoding.UTF8.GetString(bytes, 0, end).Split('\n'))
        {
            if (line.Length == 0) continue;

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement.Clone();
            _events.Add(new LogEvent(root.GetProperty("t").GetInt64(), root.GetProperty("e").GetString()!, root));
        }
    }

    #endregion // Private methods
}


[System.Text.Json.Serialization.JsonSerializable(typeof(string))]
internal sealed partial class HostJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
