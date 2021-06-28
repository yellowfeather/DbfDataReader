using Shouldly;
using Xunit;

namespace DbfDataReader.Tests
{
    [Collection("foxpro_currency")]
    public class FoxproCurrencyTests : DbaseTests
    {
        private const string FixturePath = "../../../../fixtures/foxpro_currency_01.dbf";

        public FoxproCurrencyTests() : base(FixturePath)
        {
        }

        [Fact]
        public void Should_report_correct_record_count()
        {
            DbfHeader.RecordCount.ShouldBe(3);
        }

        [Fact]
        public void Should_report_correct_version_number()
        {
            DbfHeader.Version.ShouldBe(0x30);
        }

        [Fact]
        public void Should_report_that_the_file_is_foxpro()
        {
            DbfHeader.IsFoxPro.ShouldBeTrue();
        }

        [Fact]
        public void Should_have_the_correct_number_of_columns()
        {
            DbfTable.Columns.Count.ShouldBe(2);
        }

        [Fact]
        public void Should_have_the_correct_column_schema()
        {
            ValidateColumnSchema("../../../../fixtures/foxpro_currency_01_summary.txt");
        }

        [Fact]
        [UseCulture("it-IT")]
        public void Should_have_correct_row_values()
        {
            ValidateRowValues("../../../../fixtures/foxpro_currency_01.csv");
        }
    }

}