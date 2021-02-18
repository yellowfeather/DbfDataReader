using System.Linq;
using BenchmarkDotNet.Attributes;

namespace DbfDataReader.Benchmarks
{
    public class DbfDataReaderBenchmarks
    {
        private const string FixturePath = "./fixtures/dbase_03.dbf";

        [Benchmark]
        public void DbfDataReader()
        {
            using var dbfDataReader = new DbfDataReader(FixturePath);

            var cols = dbfDataReader.GetColumnSchema();
            var allowDbNull = cols.Select(c => c.AllowDBNull != false).ToArray();
            while (dbfDataReader.Read())
            {
                for (var ordinal = 0; ordinal < dbfDataReader.FieldCount; ordinal++)
                {
                    if (allowDbNull[ordinal] && dbfDataReader.IsDBNull(ordinal))
                        continue;
                    dbfDataReader.ReadField(ordinal);
                }
            }
        }
    }
}