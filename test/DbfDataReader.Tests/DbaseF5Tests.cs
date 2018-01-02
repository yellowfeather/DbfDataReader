using Shouldly;
using Xunit;

namespace DbfDataReader.Tests
{
    [Collection("dbase_f5")]
    public class DbaseF5Tests : DbaseTests
    {
        private const string DbaseF5FixturePath = "../../../../fixtures/dbase_f5.dbf";

        public DbaseF5Tests() : base(DbaseF5FixturePath)
        {
        }

        [Fact]
        public void Should_report_correct_record_count()
        {
            DbfHeader.RecordCount.ShouldBe(975);
        }

        [Fact]
        public void Should_report_correct_version_number()
        {
            DbfHeader.Version.ShouldBe(0xf5);
        }

        [Fact]
        public void Should_report_that_the_file_is_foxpro()
        {
            DbfHeader.IsFoxPro.ShouldBeTrue();
        }

        [Fact]
        public void Should_have_the_correct_number_of_columns()
        {
            DbfTable.Columns.Count.ShouldBe(59);
        }

        [Fact]
        public void Should_have_the_correct_column_schema()
        {
            ValidateColumnSchema("../../../../fixtures/dbase_f5_summary.txt");
        }

        [Fact]
        [UseCulture("en-GB")]
        public void Should_have_correct_row_values()
        {
            ValidateRowValues("../../../../fixtures/dbase_f5.csv");
        }
    }
}