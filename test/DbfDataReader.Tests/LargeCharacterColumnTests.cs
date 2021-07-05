using Shouldly;
using Xunit;

namespace DbfDataReader.Tests
{
    [Collection("FolderRoot")]
    public class LargeCharacterColumnTests : DbaseTests
    {
        private const string FolderRootFixturePath = "../../../../fixtures/FolderRoot.dbf";

        public LargeCharacterColumnTests() : base(FolderRootFixturePath)
        {
        }

        [Fact]
        public void Should_report_correct_record_count()
        {
            DbfHeader.RecordCount.ShouldBe(9);
        }

        [Fact]
        public void Should_report_correct_version_number()
        {
            DbfHeader.Version.ShouldBe(0x31);
        }

        [Fact]
        public void Should_report_that_the_file_is_foxpro()
        {
            DbfHeader.IsFoxPro.ShouldBeTrue();
        }

        [Fact]
        public void Should_have_the_correct_number_of_columns()
        {
            DbfTable.Columns.Count.ShouldBe(10);
        }

        [Fact]
        public void Should_have_the_correct_column_schema()
        {
            ValidateColumnSchema("../../../../fixtures/FolderRoot_summary.txt");
        }

        [Fact]
        [UseCulture("en-GB")]
        public void Should_have_correct_row_values()
        {
            ValidateRowValues("../../../../fixtures/FolderRoot.csv");
        }
    }
}