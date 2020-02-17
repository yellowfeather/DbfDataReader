using Shouldly;
using Xunit;

namespace DbfDataReader.Tests
{
    [Collection("ms_khdm")]
    public class MsKhdmTests : DbaseTests
    {
        private const string MsKhdmFixturePath = "../../../../fixtures/MS__KHDM.DBF";

        public MsKhdmTests() : base(MsKhdmFixturePath)
        {
        }

        [Fact]
        public void Should_report_correct_record_count()
        {
            DbfHeader.RecordCount.ShouldBe(193);
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
            DbfTable.Columns.Count.ShouldBe(33);
        }

        [Fact]
        public void Should_have_the_correct_column_schema()
        {
            ValidateColumnSchema("../../../../fixtures/MS__KHDM_summary.txt");
        }

        [Fact(Skip = "WIP")]
        public void Should_have_correct_row_values()
        {
            ValidateRowValues("../../../../fixtures/MS__KHDM.csv");
        }
    }
}
