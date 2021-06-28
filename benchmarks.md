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

