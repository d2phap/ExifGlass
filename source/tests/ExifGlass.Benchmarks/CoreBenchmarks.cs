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
using ExifGlass.Core.Export;
using ExifGlass.Core.Metadata;
using ExifGlass.Core.Query;
using System.Buffers;

namespace ExifGlass.Benchmarks;

/// <summary>
/// §8: parse 5k tags, query 1k and 5k tags (budget 8 ms for 1k), export the rows in view.
/// </summary>
[MemoryDiagnoser]
public class CoreBenchmarks
{
    #region Fields and constants

    private readonly StringPool _pool = new();
    private ReadOnlySequence<byte> _json5k;
    private MetadataDocument _doc1k = null!;
    private MetadataDocument _doc5k = null!;
    private ViewQuery _busy = null!;
    private IReadOnlyList<ViewRow> _rows5k = [];

    #endregion // Fields and constants


    #region Setup

    [GlobalSetup]
    public void Setup()
    {
        _json5k = new ReadOnlySequence<byte>(BenchData.TableJson(5000));
        _doc1k = BenchData.Document(1000);
        _doc5k = BenchData.Document(5000);
        _busy = ViewQuery.Default with
        {
            Search = "units",
            Filters = new HashSet<QuickFilter> { QuickFilter.HasTagId },
            SortColumn = SortColumn.Value,
            SortDirection = SortDirection.Descending,
        };
        _rows5k = QueryEngine.Run(_doc5k, ViewQuery.Default).RowsInView;
    }

    #endregion // Setup


    #region Benchmarks

    [Benchmark]
    public int ParseTable5k() => TableReader.Read(_json5k, _pool).Tags.Count;


    [Benchmark]
    public int BuildDocument5k() => MetadataDocument.Create("C:/bench.cr2", TableReader.Read(_json5k, _pool)).Groups.Count;


    [Benchmark]
    public int Query1k() => QueryEngine.Run(_doc1k, ViewQuery.Default).Items.Count;


    [Benchmark]
    public int QuerySearchFilterSort1k() => QueryEngine.Run(_doc1k, _busy).Items.Count;


    [Benchmark]
    public int QuerySearchFilterSort5k() => QueryEngine.Run(_doc5k, _busy).Items.Count;


    [Benchmark]
    public int ExportCsv5k() => Exporter.ToCsv(_rows5k, TableColumns.All).Length;


    [Benchmark]
    public int ExportJson5k() => Exporter.ToExifToolJson(_rows5k, "C:/bench.cr2").Length;

    #endregion // Benchmarks
}
