using System.Collections.Generic;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace DbfDataReader.Benchmarks
{
    // Measures what automatic index use buys on a generated 50,000-row table with a
    // compound index on ID (unique integers) and CODE (character keys, ~100 rows per
    // value). Each pair runs the same SQL with UseIndexes on and off; the generated
    // files are verified for index/scan equivalence before anything is measured.
    [CategoriesColumn]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class IndexQueryBenchmarks
    {
        private const int RowCount = 50_000;

        private string _directory = null!;

        [GlobalSetup]
        public void Setup()
        {
            BenchmarkVersion.Print();
            _directory = Path.Combine(Path.GetTempPath(), "DbfDataReader.Benchmarks",
                $"index-{RowCount}");
            BenchmarkTableGenerator.Generate(_directory, RowCount);
            BenchmarkTableGenerator.Verify(_directory, RowCount);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }

        [BenchmarkCategory("equality seek (1 row)")]
        [Benchmark(Baseline = true, Description = "full scan")]
        public int EqualitySeekFullScan() => Drain("select * from bench.dbf where ID = 25000", false);

        [BenchmarkCategory("equality seek (1 row)")]
        [Benchmark(Description = "index")]
        public int EqualitySeekWithIndex() => Drain("select * from bench.dbf where ID = 25000", true);

        [BenchmarkCategory("character seek (100 rows)")]
        [Benchmark(Baseline = true, Description = "full scan")]
        public int CharacterSeekFullScan() => Drain("select * from bench.dbf where CODE = 'C000123'", false);

        [BenchmarkCategory("character seek (100 rows)")]
        [Benchmark(Description = "index")]
        public int CharacterSeekWithIndex() => Drain("select * from bench.dbf where CODE = 'C000123'", true);

        [BenchmarkCategory("range scan (100 rows)")]
        [Benchmark(Baseline = true, Description = "full scan")]
        public int RangeFullScan() =>
            Drain("select ID, NAME from bench.dbf where ID between 25000 and 25099", false);

        [BenchmarkCategory("range scan (100 rows)")]
        [Benchmark(Description = "index")]
        public int RangeWithIndex() =>
            Drain("select ID, NAME from bench.dbf where ID between 25000 and 25099", true);

        [BenchmarkCategory("top 10 order by desc")]
        [Benchmark(Baseline = true, Description = "scan + sort")]
        public int TopTenDescendingScan() =>
            Drain("select top 10 ID, NAME from bench.dbf order by ID desc", false);

        [BenchmarkCategory("top 10 order by desc")]
        [Benchmark(Description = "index")]
        public int TopTenDescendingWithIndex() =>
            Drain("select top 10 ID, NAME from bench.dbf order by ID desc", true);

        [BenchmarkCategory("count(*) filtered (10k rows)")]
        [Benchmark(Baseline = true, Description = "full scan")]
        public int CountFilteredFullScan() =>
            Scalar("select count(*) from bench.dbf where ID between 10000 and 19999", false);

        [BenchmarkCategory("count(*) filtered (10k rows)")]
        [Benchmark(Description = "index only")]
        public int CountFilteredWithIndex() =>
            Scalar("select count(*) from bench.dbf where ID between 10000 and 19999", true);

        [BenchmarkCategory("count(*) all rows")]
        [Benchmark(Baseline = true, Description = "read all rows")]
        public int CountAllByReadingRows()
        {
            using var reader = new DbfDataReader(BenchmarkTableGenerator.DbfPath(_directory));
            var count = 0;
            while (reader.Read()) count++;
            return count;
        }

        [BenchmarkCategory("count(*) all rows")]
        [Benchmark(Description = "status scan")]
        public int CountAllByStatusScan() => Scalar("select count(*) from bench.dbf", true);

        private int Drain(string sql, bool useIndexes)
        {
            using var connection = OpenConnection(useIndexes);
            var command = connection.CreateCommand();
            command.CommandText = sql;

            var rows = 0;
            var values = default(object[]);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                values ??= new object[reader.FieldCount];
                reader.GetValues(values);
                rows++;
            }

            return rows;
        }

        private int Scalar(string sql, bool useIndexes)
        {
            using var connection = OpenConnection(useIndexes);
            var command = connection.CreateCommand();
            command.CommandText = sql;

            return (int)command.ExecuteScalar()!;
        }

        private DbfDbConnection OpenConnection(bool useIndexes)
        {
            var connection = new DbfDbConnection();
            connection.ConnectionString =
                $"Folder={_directory};SkipDeletedRecords=false;UseIndexes={useIndexes}";
            connection.Open();
            return connection;
        }
    }
}
