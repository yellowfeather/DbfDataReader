using System;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests
{
    [Collection("dbase_03")]
    public class DbfDataReaderTests : IDisposable
    {
        private const string FixturePath = "./test/fixtures/dbase_03.dbf";
        
        public DbfDataReaderTests()
        {
            DbfDataReader = new DbfDataReader(FixturePath);
        }

        public void Dispose()
        {
            DbfDataReader.Dispose();
            DbfDataReader = null;
        }

        public DbfDataReader DbfDataReader { get; set; }

        [Fact]
        public void Should_have_valid_first_row_values()
        {
            DbfDataReader.Read().ShouldBeTrue();

            DbfDataReader.GetString(0).ShouldBe("0507121");
            DbfDataReader.GetString(1).ShouldBe("CMP");
            DbfDataReader.GetString(2).ShouldBe("circular");
            DbfDataReader.GetString(3).ShouldBe("12");
            DbfDataReader.GetString(4).ShouldBe(string.Empty);
            DbfDataReader.GetString(5).ShouldBe("no");
            DbfDataReader.GetString(6).ShouldBe("Good");
            DbfDataReader.GetString(7).ShouldBe(string.Empty);
            DbfDataReader.GetDateTime(8).ShouldBe(new DateTime(2005,7,12));
            DbfDataReader.GetString(9).ShouldBe("10:56:30am");
            DbfDataReader.GetDecimal(10).ShouldBe(5.2m);
            DbfDataReader.GetDecimal(11).ShouldBe(2.0m);
            DbfDataReader.GetString(12).ShouldBe("Postprocessed Code");
            DbfDataReader.GetString(13).ShouldBe("GeoXT");
            DbfDataReader.GetDateTime(14).ShouldBe(new DateTime(2005,7,12));
            DbfDataReader.GetString(15).ShouldBe("10:56:52am");
            DbfDataReader.GetString(16).ShouldBe("New");
            DbfDataReader.GetString(17).ShouldBe("Driveway");
            DbfDataReader.GetString(18).ShouldBe("050712TR2819.cor");
            DbfDataReader.GetInt32(19).ShouldBe(2);
            DbfDataReader.GetInt32(20).ShouldBe(2);
            DbfDataReader.GetString(21).ShouldBe("MS4");
            DbfDataReader.GetInt32(22).ShouldBe(1331);
            DbfDataReader.GetDecimal(23).ShouldBe(226625.000m);
            DbfDataReader.GetDecimal(24).ShouldBe(1131.323m);
            DbfDataReader.GetDecimal(25).ShouldBe(3.1m);
            DbfDataReader.GetDecimal(26).ShouldBe(1.3m);
            DbfDataReader.GetDecimal(27).ShouldBe(0.897088m);
            DbfDataReader.GetDecimal(28).ShouldBe(557904.898m);
            DbfDataReader.GetDecimal(29).ShouldBe(2212577.192m);
            DbfDataReader.GetInt32(30).ShouldBe(401);
        }        
    }
}