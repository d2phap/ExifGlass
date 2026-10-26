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
using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace ExifGlass.IpcHost;

/// <summary>
/// Processes: alive checks, a polite quit, ImageGlass 10's client PID check.
/// </summary>
public static partial class Procs
{
    #region Public methods

    /// <summary>
    /// Whether the PID is a live process that started no earlier than <paramref name="notBefore"/> (guards PID reuse).
    /// </summary>
    public static bool IsAlive(int pid, DateTime notBefore)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return !p.HasExited && p.StartTime >= notBefore.AddSeconds(-1);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }


    public static void TryKill(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            p.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // gone already
        }
    }


    /// <summary>
    /// Closes the app as a user would: WM_CLOSE on Windows, SIGTERM elsewhere; true when it ended within the wait.
    /// </summary>
    public static async Task<bool> QuitAsync(int pid, int waitMs = 5000)
    {
        Process p;
        try
        {
            p = Process.GetProcessById(pid);
        }
        catch (ArgumentException)
        {
            return true;
        }

        using (p)
        {
            if (OperatingSystem.IsWindows()) p.CloseMainWindow();
            else Process.Start("kill", ["-TERM", pid.ToString(System.Globalization.CultureInfo.InvariantCulture)])?.WaitForExit(2000);

            using var cts = new CancellationTokenSource(waitMs);
            try
            {
                await p.WaitForExitAsync(cts.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                TryKill(pid);
                return false;
            }
        }
    }


    /// <summary>
    /// ImageGlass 10's Win32PipeSecurityProvider check: the connected client must be the process it started.
    /// </summary>
    public static int? PipeClientPid(NamedPipeServerStream server)
    {
        if (!OperatingSystem.IsWindows()) return null;

        var handle = server.SafePipeHandle;
        var added = false;
        try
        {
            handle.DangerousAddRef(ref added);
            return GetNamedPipeClientProcessId(handle.DangerousGetHandle(), out var pid) ? (int)pid : null;
        }
        finally
        {
            if (added) handle.DangerousRelease();
        }
    }

    #endregion // Public methods


    #region Native

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetNamedPipeClientProcessId(nint pipe, out uint clientProcessId);

    #endregion // Native
}


/// <summary>
/// Medians for the report.
/// </summary>
public static class Stats
{
    #region Public methods

    public static double Median(IEnumerable<double> values)
    {
        var a = values.Order().ToArray();
        if (a.Length == 0) return double.NaN;
        return a.Length % 2 == 1 ? a[a.Length / 2] : (a[(a.Length / 2) - 1] + a[a.Length / 2]) / 2;
    }


    public static string Range(IEnumerable<double> values)
    {
        var a = values.ToArray();
        return a.Length == 0 ? "-" : $"{Median(a):0.#} ({a.Min():0.#}-{a.Max():0.#})";
    }

    #endregion // Public methods
}
