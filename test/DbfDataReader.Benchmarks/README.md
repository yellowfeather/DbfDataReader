# DbfDataReader.Benchmarks

Benchmarks run against a **published DbfDataReader package** (default: the version in
the csproj). Results are recorded in [`../../benchmarks.md`](../../benchmarks.md).

## Cross-library comparison (Sylvan / NDbf / DbfDataReader)

Reads every field of every record of `fixtures/tl_2019_01_place.dbf`, in the style of
[MarkPflug/Benchmarks](https://github.com/MarkPflug/Benchmarks/blob/main/source/Benchmarks/DbfDataReaderBenchmarks.cs).
Run it against any released version to compare releases:

```
dotnet run -c Release --project test/DbfDataReader.Benchmarks -p:DbfDataReaderVersion=1.1.0 -- --filter "*DbfDataReaderBenchmarks*" --job short
dotnet run -c Release --project test/DbfDataReader.Benchmarks -p:DbfDataReaderVersion=2.1.0 -- --filter "*DbfDataReaderBenchmarks*" --job short
```

## Index benchmarks (2.x only)

Generates a 50,000-row table with a compound index and pairs the same SQL with
`UseIndexes` on and off. The generated files are verified for index/scan equivalence
before anything is measured.

```
dotnet run -c Release --project test/DbfDataReader.Benchmarks -- --filter "*IndexQueryBenchmarks*" --job short
```

Omit `--job short` for full-accuracy runs.
