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
                    var pointId = dbfDataReader.GetString(0);
                    var type = dbfDataReader.GetString(1);
                    var shape = dbfDataReader.GetString(2);
                    var circularD = dbfDataReader.GetString(3);
                    var nonCircul = dbfDataReader.GetString(4);
                    var flowPrese = dbfDataReader.GetString(5);
                    var condition = dbfDataReader.GetString(6);
                    var comments = dbfDataReader.GetString(7);
                    var dateVisit = dbfDataReader.GetDateTime(8);
                    var time = dbfDataReader.GetString(9);
                    var maxPdop = dbfDataReader.GetDecimal(10);
                    var maxHdop = dbfDataReader.GetDecimal(11);
                    var corrType = dbfDataReader.GetString(12);
                    var rcvrType = dbfDataReader.GetString(13);
                    var gpsDate = dbfDataReader.GetDateTime(14);
                    var gpsTime = dbfDataReader.GetString(15);
                    var updateSta = dbfDataReader.GetString(16);
                    var FeatName = dbfDataReader.GetString(17);
                    var datafile = dbfDataReader.GetString(18);
                    var unfiltPos = dbfDataReader.GetInt64(19);
                    var filtPos = dbfDataReader.GetInt64(20);
                    var dataDicti = dbfDataReader.GetString(21);
                    var gpsWeek = dbfDataReader.GetInt32(22);
                    var gpsSecond = dbfDataReader.GetDecimal(23);
                    var gpsHeight = dbfDataReader.GetDecimal(24);
                    var vertPrec = dbfDataReader.GetDecimal(25);
                    var horzPrec = dbfDataReader.GetDecimal(26);

                    if (!dbfDataReader.IsDBNull(27))
                    {
                        var stdDev = dbfDataReader.GetDecimal(27);
                    }

                    var northing = dbfDataReader.GetDecimal(28);
                    var easting = dbfDataReader.GetDecimal(29);
                    var pointId2 = dbfDataReader.GetInt32(30);
                }
            }
        }
    }
}
