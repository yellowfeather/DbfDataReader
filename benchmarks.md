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


# v1.1.0

Cross-library comparison: read every field of every record of
`test/fixtures/tl_2019_01_place.dbf` (586 records), in the style of
[MarkPflug/Benchmarks](https://github.com/MarkPflug/Benchmarks/blob/main/source/Benchmarks/DbfDataReaderBenchmarks.cs).
Reproduce with:
`DbfDataReaderVersion=1.1.0 dotnet run -c Release --project test/DbfDataReader.Benchmarks -- --filter "*DbfDataReaderBenchmarks*" --job short`
(the version must be set through the environment variable — `-p:DbfDataReaderVersion`
does not reach BenchmarkDotNet's generated child build; the benchmark log's
`// DbfDataReader under benchmark:` line shows the version actually measured)

BenchmarkDotNet v0.15.8, macOS (Apple M3 Max), .NET 10.0.2, Job=ShortRun
(numbers are not comparable with the v0.5.x tables above: different file and hardware)

| Method  | Mean     | Error     | StdDev   | Ratio | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio |
|-------- |---------:|----------:|---------:|------:|--------:|--------:|--------:|----------:|------------:|
| Sylvan  | 284.6 us |  63.42 us |  3.48 us |  0.52 |    0.01 | 41.0156 |  0.9766 | 336.81 KB |        0.80 |
| NDbf    | 542.8 us | 144.89 us |  7.94 us |  1.00 |    0.02 | 51.7578 |  0.9766 | 423.52 KB |        1.00 |
| DbfData | 643.8 us |  19.39 us |  1.06 us |  1.19 |    0.02 | 85.9375 | 11.7188 |  704.4 KB |        1.66 |

# v2.1.0

Same comparison against DbfDataReader 2.1.0 — the sequential read path is unchanged
from 1.1.0 (all of the SQL, typed-query and index features are additive):

| Method  | Mean     | Error     | StdDev   | Ratio | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio |
|-------- |---------:|----------:|---------:|------:|--------:|--------:|--------:|----------:|------------:|
| Sylvan  | 286.5 us |  88.50 us |  4.85 us |  0.51 |    0.01 | 41.0156 |  0.9766 | 336.81 KB |        0.80 |
| NDbf    | 563.2 us | 207.53 us | 11.38 us |  1.00 |    0.02 | 51.7578 |  0.9766 | 423.52 KB |        1.00 |
| DbfData | 647.6 us | 384.16 us | 21.06 us |  1.15 |    0.04 | 85.9375 | 11.7188 | 704.41 KB |        1.66 |

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

# v2.2.0

Same comparison against DbfDataReader 2.2.0 — the read path allocates half of what
2.1.0 did: character fields trim their padding before the string is materialized,
numeric and date fields parse straight from the record buffer, and null checks no
longer box (#303).

| Method  | Mean     | Error     | StdDev   | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|-------- |---------:|----------:|---------:|------:|--------:|--------:|-------:|----------:|------------:|
| Sylvan  | 297.9 us |  82.40 us |  4.52 us |  0.55 |    0.01 | 41.0156 | 0.9766 | 336.81 KB |        0.80 |
| NDbf    | 537.0 us | 175.12 us |  9.60 us |  1.00 |    0.02 | 51.7578 | 0.9766 | 423.52 KB |        1.00 |
| DbfData | 610.0 us | 206.70 us | 11.33 us |  1.14 |    0.03 | 42.9688 | 7.8125 | 355.86 KB |        0.84 |

# v2.2.0 — automatic index use

Same generated 50,000-row table and query pairs as the v2.1.0 run above. The index
paths are unchanged, but the full-scan baselines are far cheaper than in v2.1.0:
queries parse only the columns they reference (#296), so for example the
`where ID = 25000` scan parses one binary column for the 49,999 rows that do not
match, and the halved string allocations (#303) shrink whatever must still be parsed.

Reproduce with:
`dotnet run -c Release --project test/DbfDataReader.Benchmarks -- --filter "*IndexQueryBenchmarks*"`

BenchmarkDotNet v0.15.8, macOS (Apple M3 Max), .NET 10.0.2, Job=ShortRun

| Method          | Categories                   | Mean         | Ratio | Allocated   | Alloc Ratio |
|---------------- |----------------------------- |-------------:|------:|------------:|------------:|
| 'full scan'     | character seek (100 rows)    | 17,791.35 us | 1.000 |  1976.55 KB |        1.00 |
| index           | character seek (100 rows)    |    150.55 us | 0.008 |    58.45 KB |        0.03 |
|                 |                              |              |       |             |             |
| 'read all rows' | count(*) all rows            | 20,808.41 us |  1.00 |  7038.84 KB |       1.000 |
| 'status scan'   | count(*) all rows            | 15,611.37 us |  0.75 |    10.43 KB |       0.001 |
|                 |                              |              |       |             |             |
| 'full scan'     | count(*) filtered (10k rows) | 17,806.75 us |  1.00 |  1183.69 KB |        1.00 |
| 'index only'    | count(*) filtered (10k rows) |  1,040.94 us |  0.06 |  1201.06 KB |        1.01 |
|                 |                              |              |       |             |             |
| 'full scan'     | equality seek (1 row)        | 17,344.77 us | 1.000 |  1184.37 KB |        1.00 |
| index           | equality seek (1 row)        |     51.62 us | 0.003 |    32.36 KB |        0.03 |
|                 |                              |              |       |             |             |
| 'full scan'     | range scan (100 rows)        | 17,666.93 us | 1.000 |  1193.09 KB |        1.00 |
| index           | range scan (100 rows)        |     98.77 us | 0.006 |    54.31 KB |        0.05 |
|                 |                              |              |       |             |             |
| 'scan + sort'   | top 10 order by desc         | 45,792.49 us |  1.00 | 50999.07 KB |        1.00 |
| index           | top 10 order by desc         |  3,234.12 us |  0.07 |  5659.65 KB |        0.11 |
