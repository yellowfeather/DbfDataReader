using BenchmarkDotNet.Attributes;

namespace DbfDataReader.Benchmarks
{
    // Reads every field of every record of dbase_03 through the typed getters
    // (GetString / GetDecimal / GetDateTime / ...), the way application code
    // typically consumes a row. Kept apart from the cross-library comparison in
    // DbfDataReaderBenchmarks, which uses a different fixture and an NDbf baseline.
    public class TypedGetterBenchmarks
    {
        private const string FixturePath = "./fixtures/dbase_03.dbf";

        [GlobalSetup]
        public void Setup() => BenchmarkVersion.Print();

        [Benchmark]
        public void ReadRecord()
        {
            using (var dbfDataReader = new DbfDataReader(FixturePath))
            {
                while (dbfDataReader.Read())
                {
                    _ = dbfDataReader.GetString(0);
                    _ = dbfDataReader.GetString(1);
                    _ = dbfDataReader.GetString(2);
                    _ = dbfDataReader.GetString(3);
                    _ = dbfDataReader.GetString(4);
                    _ = dbfDataReader.GetString(5);
                    _ = dbfDataReader.GetString(6);
                    _ = dbfDataReader.GetString(7);
                    _ = dbfDataReader.GetDateTime(8);
                    _ = dbfDataReader.GetString(9);
                    _ = dbfDataReader.GetDecimal(10);
                    _ = dbfDataReader.GetDecimal(11);
                    _ = dbfDataReader.GetString(12);
                    _ = dbfDataReader.GetString(13);
                    _ = dbfDataReader.GetDateTime(14);
                    _ = dbfDataReader.GetString(15);
                    _ = dbfDataReader.GetString(16);
                    _ = dbfDataReader.GetString(17);
                    _ = dbfDataReader.GetString(18);
                    _ = dbfDataReader.GetInt64(19);
                    _ = dbfDataReader.GetInt64(20);
                    _ = dbfDataReader.GetString(21);
                    _ = dbfDataReader.GetInt32(22);
                    _ = dbfDataReader.GetDecimal(23);
                    _ = dbfDataReader.GetDecimal(24);
                    _ = dbfDataReader.GetDecimal(25);
                    _ = dbfDataReader.GetDecimal(26);

                    if (!dbfDataReader.IsDBNull(27))
                    {
                        _ = dbfDataReader.GetDecimal(27);
                    }

                    _ = dbfDataReader.GetDecimal(28);
                    _ = dbfDataReader.GetDecimal(29);
                    _ = dbfDataReader.GetInt32(30);
                }
            }
        }
    }
}
