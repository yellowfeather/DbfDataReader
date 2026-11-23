using DbfDataReader.Query;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests;

[Collection("dbase_03")]
public class SqlBinderTests
{
    private const string Dbase03FixturePath = "../../../../fixtures/dbase_03.dbf";

    [Fact]
    public void Should_resolve_select_where_and_order_by_columns()
    {
        using var dbfTable = new DbfTable(Dbase03FixturePath);
        var statement = SqlParser.Parse(
            "select Point_ID, Date_Visit from dbase_03.dbf where Point_ID = 'x' and Max_PDOP is null order by Date_Visit");

        SqlBinder.Bind(statement, dbfTable.Columns);

        statement.Columns[0].Ordinal.ShouldBe(0);
        statement.Columns[1].Ordinal.ShouldBeGreaterThan(0);

        var and = statement.Where.ShouldBeOfType<SqlBinaryExpression>();
        var equal = and.Left.ShouldBeOfType<SqlBinaryExpression>();
        equal.Left.ShouldBeOfType<SqlColumnExpression>().Ordinal.ShouldBe(0);
        var isNull = and.Right.ShouldBeOfType<SqlIsNullExpression>();
        isNull.Operand.ShouldBeOfType<SqlColumnExpression>().Ordinal.ShouldBeGreaterThan(0);

        statement.OrderBy[0].Ordinal.ShouldBe(statement.Columns[1].Ordinal);
    }

    [Fact]
    public void Should_resolve_columns_case_insensitively()
    {
        using var dbfTable = new DbfTable(Dbase03FixturePath);
        var statement = SqlParser.Parse("select point_id from dbase_03.dbf");

        SqlBinder.Bind(statement, dbfTable.Columns);

        statement.Columns[0].Ordinal.ShouldBe(0);
    }

    [Fact]
    public void Should_throw_for_unknown_columns()
    {
        using var dbfTable = new DbfTable(Dbase03FixturePath);
        var statement = SqlParser.Parse("select nope from dbase_03.dbf");

        var exception = Should.Throw<SqlParseException>(() => SqlBinder.Bind(statement, dbfTable.Columns));

        exception.Message.ShouldContain("Unknown column 'nope'");
        exception.Position.ShouldBe(7);
    }

    [Fact]
    public void Should_throw_for_unknown_columns_in_where_and_order_by()
    {
        using var dbfTable = new DbfTable(Dbase03FixturePath);

        var whereStatement = SqlParser.Parse("select * from dbase_03.dbf where nope = 1");
        Should.Throw<SqlParseException>(() => SqlBinder.Bind(whereStatement, dbfTable.Columns))
            .Message.ShouldContain("Unknown column 'nope'");

        var orderByStatement = SqlParser.Parse("select * from dbase_03.dbf order by nope");
        Should.Throw<SqlParseException>(() => SqlBinder.Bind(orderByStatement, dbfTable.Columns))
            .Message.ShouldContain("Unknown column 'nope'");
    }
}
