# DbfDataReader.Benchmarks

Benchmarks run against a **published DbfDataReader package** (default: the version in
the csproj). Results are recorded in [`../../benchmarks.md`](../../benchmarks.md).

To benchmark a different version, set the **`DbfDataReaderVersion` environment
variable** — do *not* use `-p:DbfDataReaderVersion`. BenchmarkDotNet rebuilds this
project inside a generated child project which does not inherit `-p` MSBuild
properties, so the `-p` form silently benchmarks the csproj default; the environment
variable reaches every build. Each benchmark prints the version it actually loaded
(`// DbfDataReader under benchmark: ...`) — check it in the log.

## Cross-library comparison (Sylvan / NDbf / DbfDataReader)

Reads every field of every record of `fixtures/tl_2019_01_place.dbf`, in the style of
[MarkPflug/Benchmarks](https://github.com/MarkPflug/Benchmarks/blob/main/source/Benchmarks/DbfDataReaderBenchmarks.cs).
Run it against any released version to compare releases:

```
DbfDataReaderVersion=1.1.0 dotnet run -c Release --project test/DbfDataReader.Benchmarks -- --filter "*DbfDataReaderBenchmarks*" --job short
DbfDataReaderVersion=2.1.0 dotnet run -c Release --project test/DbfDataReader.Benchmarks -- --filter "*DbfDataReaderBenchmarks*" --job short
```

(On Windows PowerShell: `$env:DbfDataReaderVersion = "2.1.0"` before the command.)

## Index benchmarks (2.x only)

Generates a 50,000-row table with a compound index and pairs the same SQL with
`UseIndexes` on and off. The generated files are verified for index/scan equivalence
before anything is measured.

```
dotnet run -c Release --project test/DbfDataReader.Benchmarks -- --filter "*IndexQueryBenchmarks*" --job short
```

Omit `--job short` for full-accuracy runs.
