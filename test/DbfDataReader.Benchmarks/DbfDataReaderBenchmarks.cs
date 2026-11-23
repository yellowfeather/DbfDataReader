using System.IO;
using BenchmarkDotNet.Attributes;
using NDbfReader;
using Sylvan.Data.XBase;

namespace DbfDataReader.Benchmarks
{
    // Cross-library comparison in the style of
    // https://github.com/MarkPflug/Benchmarks/blob/main/source/Benchmarks/DbfDataReaderBenchmarks.cs:
    // read every field of every record of a US Census TIGER place file. Run against a
    // specific released DbfDataReader with -p:DbfDataReaderVersion=x.y.z (see README).
    public class DbfDataReaderBenchmarks
    {
        private const string FixturePath = "./fixtures/tl_2019_01_place.dbf";

        [GlobalSetup]
        public void Setup() => BenchmarkVersion.Print();

        [Benchmark]
        public void Sylvan()
        {
            using var stream = File.OpenRead(FixturePath);
            using var reader = XBaseDataReader.Create(stream);
            while (reader.Read())
            {
                for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
                {
                    if (reader.IsDBNull(ordinal)) continue;
                    reader.GetValue(ordinal);
                }
            }
        }

        [Benchmark(Baseline = true)]
        public void NDbf()
        {
            using var table = Table.Open(FixturePath);
            var reader = table.OpenReader();
            while (reader.Read())
            {
                foreach (var column in table.Columns)
                {
                    reader.GetValue(column);
                }
            }
        }

        [Benchmark]
        public void DbfData()
        {
            using var dbfDataReader = new DbfDataReader(FixturePath);
            while (dbfDataReader.Read())
            {
                for (var ordinal = 0; ordinal < dbfDataReader.FieldCount; ordinal++)
                {
                    if (dbfDataReader.IsDBNull(ordinal)) continue;
                    dbfDataReader.ReadField(ordinal);
                }
            }
        }
    }
}
