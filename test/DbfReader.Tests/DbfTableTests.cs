using System;
using Shouldly;
using Xunit;

namespace DbfReader.Tests
{
    [Collection("dbase_30")]
    public class DbfTableTests
    {
        [Fact]
        public void Should_dispose_binary_reader()
        {
            const string fixturePath = "./test/fixtures/dbase_30.dbf";

            var dbfTable = new DbfTable(fixturePath);

            dbfTable.BinaryReader.ShouldNotBeNull();

            dbfTable.Dispose();

            dbfTable.BinaryReader.ShouldBeNull();
        }

        [Fact]
        public void Should_dispose_memo()
        {
            const string fixturePath = "./test/fixtures/dbase_30.dbf";

            var dbfTable = new DbfTable(fixturePath);

            dbfTable.Memo.ShouldNotBeNull();

            dbfTable.Dispose();

            dbfTable.Memo.ShouldBeNull();
        }
    }
}