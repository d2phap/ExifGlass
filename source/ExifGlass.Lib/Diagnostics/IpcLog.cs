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
using System.Diagnostics;
using System.Text.Json;

namespace ExifGlass.Diagnostics;

/// <summary>
/// EG_IPC_LOG=&lt;file&gt;: the ImageGlass session as JSON lines for the IPC test host and the gates; off unless the variable is set.
/// </summary>
public static class IpcLog
{
    #region Fields and constants

    public const string VARIABLE = "EG_IPC_LOG";

    /// <summary>
    /// EG_IPC_GC=1: a full collection before each SETTLED line, so the test host compares live memory across a long session.
    /// </summary>
    public const string GC_VARIABLE = "EG_IPC_GC";

    private static readonly Lock _lock = new();
    private static readonly ArrayBufferWriter<byte> _buffer = new(512);
    private static readonly Stream? _file = Open();

    #endregion // Fields and constants


    #region Properties

    public static bool IsOn => _file is not null;

    public static bool CollectsOnSettle { get; } = Environment.GetEnvironmentVariable(GC_VARIABLE) == "1";

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// One line: t is Stopwatch.GetTimestamp, machine-wide, so the test host's times and these subtract.
    /// </summary>
    public static void Write(string name, params ReadOnlySpan<(string Key, object? Value)> fields)
    {
        if (_file is null) return;

        var ticks = Stopwatch.GetTimestamp();
        lock (_lock)
        {
            _buffer.ResetWrittenCount();
            using (var writer = new Utf8JsonWriter(_buffer))
            {
                writer.WriteStartObject();
                writer.WriteNumber("t", ticks);
                writer.WriteString("e", name);
                foreach (var (key, value) in fields)
                {
                    switch (value)
                    {
                        case null: writer.WriteNull(key); break;
                        case string s: writer.WriteString(key, s); break;
                        case bool b: writer.WriteBoolean(key, b); break;
                        case int i: writer.WriteNumber(key, i); break;
                        case long l: writer.WriteNumber(key, l); break;
                        case double d: writer.WriteNumber(key, Math.Round(d, 3)); break;
                        default: writer.WriteString(key, value.ToString()); break;
                    }
                }

                writer.WriteEndObject();
            }

            _buffer.Write("\n"u8);
            try
            {
                _file.Write(_buffer.WrittenSpan);
                _file.Flush();
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                // a log line is never worth a crash
            }
        }
    }


    public static double Ms(long fromTicks, long toTicks) => (toTicks - fromTicks) * 1000.0 / Stopwatch.Frequency;

    #endregion // Public methods


    #region Private methods

    private static FileStream? Open()
    {
        if (Environment.GetEnvironmentVariable(VARIABLE) is not { Length: > 0 } path) return null;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

            // the test host reads the file while the app writes it
            return new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    #endregion // Private methods
}
