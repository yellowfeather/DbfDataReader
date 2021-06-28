using System.Text;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests
{
    [Collection("dbase_30")]
    public class DbfTableTests
    {
        [Fact]
        public void Should_dispose_stream()
        {
            const string fixturePath = "../../../../fixtures/dbase_30.dbf";

            var dbfTable = new DbfTable(fixturePath, Encoding.GetEncoding(1252));

            dbfTable.Stream.ShouldNotBeNull();

            dbfTable.Dispose();

            dbfTable.Stream.ShouldBeNull();
        }

        [Fact]
        public void Should_dispose_memo()
        {
            const string fixturePath = "../../../../fixtures/dbase_30.dbf";

            var dbfTable = new DbfTable(fixturePath, Encoding.GetEncoding(1252));

            dbfTable.Memo.ShouldNotBeNull();

            dbfTable.Dispose();

            dbfTable.Memo.ShouldBeNull();
        }
    }
}