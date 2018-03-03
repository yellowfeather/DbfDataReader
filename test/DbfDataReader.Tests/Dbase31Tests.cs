using Shouldly;
using Xunit;

namespace DbfDataReader.Tests
{
    [Collection("dbase_31")]
    public class Dbase31Tests : DbaseTests
    {
        private const string Dbase31FixturePath = "../../../../fixtures/dbase_31.dbf";

        public Dbase31Tests() : base(Dbase31FixturePath)
        {
        }

        [Fact]
        public void Should_report_correct_record_count()
        {
            DbfHeader.RecordCount.ShouldBe(77);
        }

        [Fact]
        public void Should_report_correct_version_number()
        {
            DbfHeader.Version.ShouldBe(0x31);
        }

        [Fact]
        public void Should_report_that_the_file_is_not_foxpro()
        {
            DbfHeader.IsFoxPro.ShouldBeTrue();
        }

        [Fact]
        public void Should_have_the_correct_number_of_columns()
        {
            DbfTable.Columns.Count.ShouldBe(13);
        }

        [Fact]
        public void Should_have_the_correct_column_schema()
        {
            ValidateColumnSchema("../../../../fixtures/dbase_31_summary.txt");
        }

        [Fact]
        [UseCulture("en-GB")]
        public void Should_have_correct_row_values()
        {
            ValidateRowValues("../../../../fixtures/dbase_31.csv");
        }
    }
}