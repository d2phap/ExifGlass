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
using ExifGlass.Core.Diagnostics;
using Xunit;

namespace ExifGlass.Core.Tests.Diagnostics;

public class SelfTestTests
{
    #region Tests

    [Fact]
    public async Task AllPassingChecksExitWithZero()
    {
        var output = new StringWriter();
        var test = new SelfTest()
            .Add("one", _ => Task.FromResult("fine"))
            .Add("two", _ => Task.FromResult("also fine"));

        var exitCode = await test.RunAsync(output, TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("PASS one: fine", output.ToString());
        Assert.Contains("PASS two: also fine", output.ToString());
        Assert.Contains("selftest: 2/2 passed", output.ToString());
    }


    [Fact]
    public async Task AFailingCheckIsReportedAndTheRestStillRun()
    {
        var output = new StringWriter();
        var test = new SelfTest()
            .Add("broken", _ => throw new InvalidOperationException("no luck"))
            .Add("after", _ => Task.FromResult("ran"));

        var exitCode = await test.RunAsync(output, TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains("FAIL broken: InvalidOperationException: no luck", output.ToString());
        Assert.Contains("PASS after: ran", output.ToString());
        Assert.Contains("selftest: 1/2 passed", output.ToString());
    }


    [Fact]
    public async Task TheJsonCheckRoundTripsWithReflectionDisabled()
    {
        var output = new StringWriter();
        var test = new SelfTest().AddCoreChecks();

        await test.RunAsync(output, TestContext.Current.CancellationToken);

        Assert.Contains("PASS json: source-generated round trip", output.ToString());
    }

    #endregion // Tests
}
