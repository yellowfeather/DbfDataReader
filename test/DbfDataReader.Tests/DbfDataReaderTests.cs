using System;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests
{
    [Collection("dbase_03")]
    public class DbfDataReaderTests
    {
        private const string FixturePath = "../../../../fixtures/dbase_03.dbf";
        
        [Fact]
        public void Should_have_valid_first_row_values()
        {
            using (var dbfDataReader = new DbfDataReader(FixturePath))
            {
                dbfDataReader.Read().ShouldBeTrue();

                dbfDataReader.GetString(0).ShouldBe("0507121");
                dbfDataReader.GetString(1).ShouldBe("CMP");
                dbfDataReader.GetString(2).ShouldBe("circular");
                dbfDataReader.GetString(3).ShouldBe("12");
                dbfDataReader.GetString(4).ShouldBe(string.Empty);
                dbfDataReader.GetString(5).ShouldBe("no");
                dbfDataReader.GetString(6).ShouldBe("Good");
                dbfDataReader.GetString(7).ShouldBe(string.Empty);
                dbfDataReader.GetDateTime(8).ShouldBe(new DateTime(2005, 7, 12));
                dbfDataReader.GetString(9).ShouldBe("10:56:30am");
                dbfDataReader.GetDecimal(10).ShouldBe(5.2m);
                dbfDataReader.GetDecimal(11).ShouldBe(2.0m);
                dbfDataReader.GetString(12).ShouldBe("Postprocessed Code");
                dbfDataReader.GetString(13).ShouldBe("GeoXT");
                dbfDataReader.GetDateTime(14).ShouldBe(new DateTime(2005, 7, 12));
                dbfDataReader.GetString(15).ShouldBe("10:56:52am");
                dbfDataReader.GetString(16).ShouldBe("New");
                dbfDataReader.GetString(17).ShouldBe("Driveway");
                dbfDataReader.GetString(18).ShouldBe("050712TR2819.cor");
                dbfDataReader.GetInt32(19).ShouldBe(2);
                dbfDataReader.GetInt32(20).ShouldBe(2);
                dbfDataReader.GetString(21).ShouldBe("MS4");
                dbfDataReader.GetInt32(22).ShouldBe(1331);
                dbfDataReader.GetDecimal(23).ShouldBe(226625.000m);
                dbfDataReader.GetDecimal(24).ShouldBe(1131.323m);
                dbfDataReader.GetDecimal(25).ShouldBe(3.1m);
                dbfDataReader.GetDecimal(26).ShouldBe(1.3m);
                dbfDataReader.GetDecimal(27).ShouldBe(0.897088m);
                dbfDataReader.GetDecimal(28).ShouldBe(557904.898m);
                dbfDataReader.GetDecimal(29).ShouldBe(2212577.192m);
                dbfDataReader.GetInt32(30).ShouldBe(401);
            }
        }

        [Fact]
        public void Should_be_able_to_read_all_the_rows()
        {
            using (var dbfDataReader = new DbfDataReader(FixturePath))
            {
                var rowCount = 0;
                while (dbfDataReader.Read())
                {
                    rowCount++;

                    var valueCol1 = dbfDataReader.GetString(0);
                    var valueCol2 = dbfDataReader.GetDecimal(10);
                }

                rowCount.ShouldBe(14);
            }
        }

        [Fact]
        public void Should_skip_deleted_rows()
        {
            var options = new DbfDataReaderOptions
            {
                SkipDeletedRecords = true
            };
            using (var dbfDataReader = new DbfDataReader(FixturePath, options))
            {
                var rowCount = 0;
                while (dbfDataReader.Read())
                {
                    rowCount++;

                    var valueCol1 = dbfDataReader.GetString(0);
                    var valueCol2 = dbfDataReader.GetDecimal(10);
                }

                rowCount.ShouldBe(12);
            }
        }

        [Fact]
        public void Should_throw_exception_when_casting_to_wrong_type()
        {
            using (var dbfDataReader = new DbfDataReader(FixturePath))
            {
                dbfDataReader.Read();

                var exception = Should.Throw<InvalidCastException>(() => dbfDataReader.GetInt32(0));
                exception.Message.ShouldBe(
                    "Unable to cast object of type 'System.String' to type 'System.Int32' at ordinal '0'.");
            }
        }
    }
}