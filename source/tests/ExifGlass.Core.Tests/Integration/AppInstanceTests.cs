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
using ExifGlass.Core.Integration;
using System.Collections.Concurrent;
using Xunit;

namespace ExifGlass.Core.Tests.Integration;

/// <summary>
/// Single instance: one slot per user and settings folder, a later launch's arguments over the pipe, and nothing lost before the window attaches.
/// </summary>
public sealed class AppInstanceTests : IDisposable
{
    #region Fields and constants

    // a folder of its own, so a running ExifGlass never takes part
    private readonly string _dir = Directory.CreateTempSubdirectory("eg-instance-").FullName;

    #endregion // Fields and constants


    #region Tests

    [Fact]
    public void TheFirstLaunchHoldsTheSlotUntilItLetsGo()
    {
        using var first = new AppInstance(_dir);
        using var later = new AppInstance(_dir);

        Assert.True(first.TryClaim());
        Assert.False(later.TryClaim());

        first.Dispose();
        Assert.True(later.TryClaim());
        Assert.False(first.TryClaim());
    }


    [Fact]
    public void AnotherSettingsFolderIsAnotherInstance()
    {
        var other = Directory.CreateTempSubdirectory("eg-instance-").FullName;
        try
        {
            using var first = new AppInstance(_dir);
            using var portable = new AppInstance(other);

            Assert.True(first.TryClaim());
            Assert.True(portable.TryClaim());
            Assert.NotEqual(AppInstance.IdFor(_dir), AppInstance.IdFor(other));
            Assert.Equal(AppInstance.IdFor(_dir), AppInstance.IdFor(_dir + Path.DirectorySeparatorChar));
            if (!OperatingSystem.IsLinux()) Assert.Equal(AppInstance.IdFor(_dir), AppInstance.IdFor(_dir.ToUpperInvariant()));
        }
        finally
        {
            Directory.Delete(other);
        }
    }


    [Fact]
    public async Task ALaterLaunchHandsItsArgumentsToTheFirst()
    {
        using var first = new AppInstance(_dir);
        Assert.True(first.TryClaim());
        first.Listen();
        var received = new BlockingCollection<string[]>();
        first.Attach(received.Add);

        using var later = new AppInstance(_dir);
        Assert.False(later.TryClaim());
        Assert.True(await Task.Run(() => later.Send([@"C:\photos\a b ảnh.jpg"])));
        Assert.True(await Task.Run(() => later.Send([])));

        Assert.True(received.TryTake(out var one, TimeSpan.FromSeconds(5)));
        Assert.Equal([@"C:\photos\a b ảnh.jpg"], one);
        Assert.True(received.TryTake(out var two, TimeSpan.FromSeconds(5)));
        Assert.Empty(two);
    }


    [Fact]
    public async Task WhatArrivesBeforeTheWindowAttachesWaitsForIt()
    {
        using var first = new AppInstance(_dir);
        Assert.True(first.TryClaim());
        first.Listen();

        using var later = new AppInstance(_dir);
        Assert.True(await Task.Run(() => later.Send(["early.jpg"])));

        // the pipe reads on a pool thread: give it a moment to queue
        var received = new BlockingCollection<string[]>();
        for (var i = 0; i < 100 && received.Count == 0; i++)
        {
            await Task.Delay(20);
            first.Attach(received.Add);
        }

        Assert.True(received.TryTake(out var args, TimeSpan.FromSeconds(5)));
        Assert.Equal(["early.jpg"], args);
    }


    [Fact]
    public async Task SendingFailsWhenNoWindowListens()
    {
        using var first = new AppInstance(_dir);
        Assert.True(first.TryClaim());

        using var later = new AppInstance(_dir);
        Assert.False(await Task.Run(() => later.Send(["a.jpg"])));
    }


    [Fact]
    public void ArgumentsRoundTrip()
    {
        string[] args = ["a.jpg", "", "with space", "ảnh", "-p:Theme=dark"];

        Assert.Equal(args, AppInstance.Decode(AppInstance.Encode(args)));
        Assert.Empty(AppInstance.Decode(AppInstance.Encode([])));
    }

    #endregion // Tests


    #region Public methods

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    #endregion // Public methods
}
