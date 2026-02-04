using Shouldly;
using Xunit;

namespace DbfDataReader.Tests;

[Collection("dbase_8C")]
public class Dbase8CTests : DbaseTests
{
    private const string Dbase8CFixturePath = "../../../../fixtures/dbase_8c.dbf";

    public Dbase8CTests() : base(Dbase8CFixturePath)
    {
    }

    [Fact]
    public void Should_report_correct_record_count()
    {
        DbfHeader.RecordCount.ShouldBe(8);
    }

    [Fact]
    public void Should_report_correct_version_number()
    {
        DbfHeader.Version.ShouldBe(0x8B);
    }

    [Fact]
    public void Should_have_the_correct_column_schema()
    {
        ValidateColumnSchema("../../../../fixtures/dbase_8c_summary.txt");
    }

    [Fact]
    public void Should_have_correct_row_values()
    {
        ValidateRowValues("../../../../fixtures/dbase_8c.csv");
    }
}