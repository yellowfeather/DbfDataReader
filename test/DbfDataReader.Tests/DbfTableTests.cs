using System;
using System.IO;
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

        [Fact]
        public void Should_reject_a_null_path()
        {
            Should.Throw<ArgumentNullException>(() => new DbfTable((string) null))
                .ParamName.ShouldBe("path");
        }

        [Theory]
        [InlineData("")]
        [InlineData("missing\0file.dbf")]
        public void Should_reject_a_malformed_path(string path)
        {
            var exception = Should.Throw<ArgumentException>(() => new DbfTable(path));
            exception.ShouldNotBeOfType<ArgumentNullException>();
        }

        [Fact]
        public void Should_report_a_missing_file()
        {
            Should.Throw<FileNotFoundException>(() => new DbfTable("../../../../fixtures/does_not_exist.dbf"));
        }

        [Fact]
        public void Should_dispose_caller_supplied_stream_by_default()
        {
            const string fixturePath = "../../../../fixtures/dbase_03.dbf";

            var stream = File.OpenRead(fixturePath);
            var dbfTable = new DbfTable(stream);

            dbfTable.Dispose();

            stream.CanRead.ShouldBeFalse();
        }

        [Fact]
        public void Should_leave_caller_supplied_stream_open_when_leave_open_is_set()
        {
            const string fixturePath = "../../../../fixtures/dbase_03.dbf";

            using var stream = File.OpenRead(fixturePath);
            var dbfTable = new DbfTable(stream, leaveOpen: true);

            dbfTable.Dispose();

            stream.CanRead.ShouldBeTrue();
        }
    }
}