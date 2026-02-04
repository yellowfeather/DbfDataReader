using Shouldly;
using Xunit;

namespace DbfDataReader.Tests
{
    [Collection("dbase_02")]
    public class Dbase02Tests : DbaseTests
    {
        private const string Dbase02FixturePath = "../../../../fixtures/dbase_02.dbf";

        public Dbase02Tests() : base(Dbase02FixturePath)
        {
        }

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

        [Fact]
        public void Should_have_the_correct_column_schema()
        {
            ValidateColumnSchema("../../../../fixtures/dbase_02_summary.txt");
        }

        [Fact]
        [UseCulture("en-GB")]
        public void Should_have_correct_row_values()
        {
            ValidateRowValues("../../../../fixtures/dbase_02.csv");
        }
    }
}
