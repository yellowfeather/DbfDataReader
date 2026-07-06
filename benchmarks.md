# v0.5.6

|  Method |     Mean |     Error |    StdDev |   Median | Ratio | RatioSD |    Gen 0 |  Gen 1 | Gen 2 |  Allocated |
|-------- |---------:|----------:|----------:|---------:|------:|--------:|---------:|-------:|------:|-----------:|
|  Sylvan | 1.587 ms | 0.0310 ms | 0.0290 ms | 1.585 ms |  0.47 |    0.02 | 121.8750 |      - |     - |  750.47 KB |
|    NDbf | 3.310 ms | 0.0661 ms | 0.1558 ms | 3.363 ms |  1.00 |    0.00 | 213.2353 |      - |     - | 1309.15 KB |
| DbfData | 5.914 ms | 0.1176 ms | 0.1997 ms | 5.975 ms |  1.79 |    0.11 | 710.2273 | 5.6818 |     - | 4355.29 KB |

// * Hints *
Outliers
  DbfDataReaderBenchmarks.Sylvan: IterationTime=1.0000 s -> 2 outliers were removed, 3 outliers were detected (1.50 ms, 1.67 ms, 1.67 ms)
  DbfDataReaderBenchmarks.NDbf: IterationTime=1.0000 s   -> 1 outlier  was  removed (3.97 ms)


# v0.5.7

|  Method |     Mean |     Error |    StdDev | Ratio | RatioSD |    Gen 0 |  Gen 1 | Gen 2 |  Allocated |
|-------- |---------:|----------:|----------:|------:|--------:|---------:|-------:|------:|-----------:|
|  Sylvan | 1.505 ms | 0.0299 ms | 0.0674 ms |  0.46 |    0.03 | 121.9512 | 1.5244 |     - |  750.47 KB |
|    NDbf | 3.300 ms | 0.0651 ms | 0.1301 ms |  1.00 |    0.00 | 211.8056 |      - |     - | 1309.15 KB |
| DbfData | 6.362 ms | 0.1271 ms | 0.1463 ms |  1.92 |    0.08 | 906.2500 | 6.2500 |     - | 5563.33 KB |

// * Hints *
Outliers
  DbfDataReaderBenchmarks.Sylvan: IterationTime=1.0000 s -> 3 outliers were removed (2.12 ms..2.18 ms)
  DbfDataReaderBenchmarks.NDbf: IterationTime=1.0000 s   -> 2 outliers were removed (4.37 ms, 4.44 ms)


# v2.1.0 — automatic index use

Generated 50,000-row table (65-byte records) with a compound index on `ID` (unique
integers) and `CODE` (character keys, ~100 rows per value); see
`BenchmarkTableGenerator` in `test/DbfDataReader.Benchmarks`. Each pair runs the same
SQL through `DbfDbConnection` with `UseIndexes` on and off; the generated files are
verified for index/scan equivalence before anything is measured.

Reproduce with:
`dotnet run -c Release --project test/DbfDataReader.Benchmarks -- --filter "*IndexQueryBenchmarks*"`

BenchmarkDotNet v0.15.8, macOS (Apple M3 Max), .NET 10.0.2, Job=ShortRun

| Method          | Categories                   | Mean         | Ratio | Allocated   | Alloc Ratio |
|---------------- |----------------------------- |-------------:|------:|------------:|------------:|
| 'full scan'     | character seek (100 rows)    | 22,174.38 us | 1.000 | 16813.84 KB |       1.000 |
| index           | character seek (100 rows)    |    156.94 us | 0.007 |    81.27 KB |       0.005 |
|                 |                              |              |       |             |             |
| 'read all rows' | count(*) all rows            | 21,752.63 us |  1.00 | 16804.55 KB |       1.000 |
| 'status scan'   | count(*) all rows            | 15,131.75 us |  0.70 |    10.42 KB |       0.001 |
|                 |                              |              |       |             |             |
| 'full scan'     | count(*) filtered (10k rows) | 23,582.86 us |  1.00 | 17980.82 KB |        1.00 |
| 'index only'    | count(*) filtered (10k rows) |    972.50 us |  0.04 |  1201.05 KB |        0.07 |
|                 |                              |              |       |             |             |
| 'full scan'     | equality seek (1 row)        | 22,998.16 us | 1.000 | 17980.99 KB |       1.000 |
| index           | equality seek (1 row)        |     50.12 us | 0.002 |    31.98 KB |       0.002 |
|                 |                              |              |       |             |             |
| 'full scan'     | range scan (100 rows)        | 23,776.99 us | 1.000 | 17983.64 KB |       1.000 |
| index           | range scan (100 rows)        |    102.24 us | 0.004 |    81.12 KB |       0.005 |
|                 |                              |              |       |             |             |
| 'scan + sort'   | top 10 order by desc         | 60,556.31 us |  1.00 | 66357.99 KB |        1.00 |
| index           | top 10 order by desc         |  2,986.39 us |  0.05 |  5661.96 KB |        0.09 |
