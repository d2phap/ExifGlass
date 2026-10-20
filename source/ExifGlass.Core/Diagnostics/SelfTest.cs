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

namespace ExifGlass.Core.Diagnostics;

/// <summary>
/// Runs named checks inside the published app (--selftest) and prints one line per check.
/// </summary>
public sealed class SelfTest
{
    #region Fields

    private readonly List<(string Name, Func<CancellationToken, Task<string>> Check)> _checks = [];

    #endregion // Fields


    #region Properties

    public int Count => _checks.Count;

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Adds a check that returns a short detail on success and throws on failure.
    /// </summary>
    public SelfTest Add(string name, Func<CancellationToken, Task<string>> check)
    {
        _checks.Add((name, check));
        return this;
    }


    /// <summary>
    /// Runs every check in order; returns 0 when all pass, 1 otherwise.
    /// </summary>
    public async Task<int> RunAsync(TextWriter output, CancellationToken cancellationToken = default)
    {
        var passed = 0;

        foreach (var (name, check) in _checks)
        {
            var watch = Stopwatch.StartNew();
            string line;

            try
            {
                var detail = await check(cancellationToken).ConfigureAwait(false);
                passed++;
                line = $"PASS {name}: {detail} ({watch.ElapsedMilliseconds} ms)";
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                line = $"FAIL {name}: {ex.GetType().Name}: {ex.Message} ({watch.ElapsedMilliseconds} ms)";
            }

            await output.WriteLineAsync(line).ConfigureAwait(false);
        }

        await output.WriteLineAsync($"selftest: {passed}/{_checks.Count} passed").ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);

        return passed == _checks.Count ? 0 : 1;
    }

    #endregion // Public methods
}
