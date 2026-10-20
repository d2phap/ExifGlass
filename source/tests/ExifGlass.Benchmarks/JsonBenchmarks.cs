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
using BenchmarkDotNet.Attributes;
using ExifGlass.Core.Diagnostics;
using System.Text.Json;

namespace ExifGlass.Benchmarks;

/// <summary>
/// Baseline for source-generated JSON, the path every metadata read goes through.
/// </summary>
[MemoryDiagnoser]
public class JsonBenchmarks
{
    #region Fields

    private readonly SelfTestProbe _probe = new("ExifGlass", 3, ["EXIF", "XMP", "AI"]);
    private string _json = string.Empty;

    #endregion // Fields


    #region Setup

    [GlobalSetup]
    public void Setup() => _json = JsonSerializer.Serialize(_probe, CoreJsonContext.Default.SelfTestProbe);

    #endregion // Setup


    #region Benchmarks

    [Benchmark]
    public string Serialize() => JsonSerializer.Serialize(_probe, CoreJsonContext.Default.SelfTestProbe);


    [Benchmark]
    public SelfTestProbe? Deserialize() => JsonSerializer.Deserialize(_json, CoreJsonContext.Default.SelfTestProbe);

    #endregion // Benchmarks
}
