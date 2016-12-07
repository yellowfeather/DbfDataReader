using System;
using Shouldly;
using Xunit;

namespace DbfReader.Tests
{
    public class Dbase3Tests : IDisposable
    {
        private const string FixturePath = "C:\\code\\yellowfeather\\DbfReader\\test\\fixtures\\dbase_03.dbf";

        public Dbase3Tests()
        {
            DbfTable = new DbfTable(FixturePath);
        }

        public void Dispose()
        {
            DbfTable.Dispose();
        }

        public DbfTable DbfTable { get; }

        public DbfHeader DbfHeader => DbfTable.Header;

        [Fact]
        public void Should_report_correct_record_count()
        {
            DbfHeader.RecordCount.ShouldBe(14);
        }

        [Fact]
        public void Should_report_correct_version_number()
        {
            DbfHeader.Version.ShouldBe(0x03);
        }

        [Fact]
        public void Should_report_that_the_file_is_not_foxpro()
        {
            DbfHeader.IsFoxPro.ShouldBeFalse();
        }

        [Fact]
        public void Should_have_the_correct_number_of_columns()
        {
            DbfTable.Columns.Count.ShouldBe(31);
        }
    }
}
