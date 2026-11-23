using Shouldly;
using Xunit;

namespace DbfDataReader.Tests;

[Collection("dbase_8B")]
public class Dbase8BTests : DbaseTests
{
    private const string Dbase8BFixturePath = "../../../../fixtures/client.dbf";

    public Dbase8BTests() : base(Dbase8BFixturePath)
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
        ValidateColumnSchema("../../../../fixtures/client_summary.txt");
    }

    [Fact]
    public void Should_have_correct_row_values()
    {
        ValidateRowValues("../../../../fixtures/client.csv");
    }
}