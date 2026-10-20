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
using ExifGlass.Core.ExifTool;
using ExifGlass.Core.Tests;
using Xunit;

[assembly: AssemblyFixture(typeof(ExifToolFixture))]

namespace ExifGlass.Core.Tests;

/// <summary>
/// One warm ExifTool daemon shared by the integration tests, started once per test run.
/// </summary>
public sealed class ExifToolFixture : IAsyncLifetime
{
    #region Properties

    /// <summary>
    /// Null when the bundled ExifTool isn't there; tests then skip.
    /// </summary>
    public ExifToolService? Service { get; private set; }

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// The shared service; skips the calling test when there is no ExifTool.
    /// </summary>
    public ExifToolService Require()
    {
        TestEnvironment.RequireExifTool();
        return Service ?? throw new InvalidOperationException("ExifTool didn't start");
    }


    public async ValueTask InitializeAsync()
    {
        if (TestEnvironment.Location is not { } location) return;

        if (!OperatingSystem.IsWindows()) BundledExifTool.EnsureExecutable(Path.GetDirectoryName(location.Path));
        Service = new ExifToolService(location);
        await Service.StartAsync();
    }


    public async ValueTask DisposeAsync()
    {
        if (Service is not null) await Service.DisposeAsync();
    }

    #endregion // Public methods
}
