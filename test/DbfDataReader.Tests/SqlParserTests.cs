using System;
using System.IO;
using System.Linq;
using Bogus;
using DbfDataReader.Query;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests;

public class SqlParserTests
{
    private readonly string _fileName;

    public SqlParserTests()
    {
        // bare (unquoted) table names consist of word characters and dots; Bogus can
        // generate names with apostrophes, ampersands or spaces, which would need
        // quoting and are covered by the delimited-name tests
        var faker = new Faker();
        var fileName = faker.System.FileName("dbf");
        _fileName = System.Text.RegularExpressions.Regex.Replace(fileName, @"[^\w.]", "_");
    }

    [Fact]
    public void Should_parse_filename_without_extension()
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(_fileName);

        var statement = SqlParser.Parse($"select * from {fileNameWithoutExtension}");

        statement.TableName.ShouldBe(fileNameWithoutExtension);
        statement.IsSelectAll.ShouldBeTrue();
    }

    [Theory]
    [InlineData("select * from {0}")]
    [InlineData("SELECT * FROM {0}")]
    [InlineData("SELECT * from {0}")]
    [InlineData("select * from {0};")]
    [InlineData("  select  *  from  {0}  ")]
    public void Should_parse_select_all(string format)
    {
        var statement = SqlParser.Parse(string.Format(format, _fileName));

        statement.IsSelectAll.ShouldBeTrue();
        statement.TableName.ShouldBe(_fileName);
        statement.Columns.ShouldBeEmpty();
        statement.Where.ShouldBeNull();
        statement.OrderBy.ShouldBeEmpty();
        statement.Top.ShouldBeNull();
    }

    [Theory]
    [InlineData("select * from \"file with spaces.dbf\"")]
    [InlineData("select * from [file with spaces.dbf]")]
    public void Should_parse_delimited_table_names(string commandText)
    {
        var statement = SqlParser.Parse(commandText);

        statement.TableName.ShouldBe("file with spaces.dbf");
    }

    [Fact]
    public void Should_parse_column_list()
    {
        var statement = SqlParser.Parse("select id, name from contacts.dbf");

        statement.IsSelectAll.ShouldBeFalse();
        statement.Columns.Count.ShouldBe(2);
        statement.Columns[0].ColumnName.ShouldBe("id");
        statement.Columns[0].OutputName.ShouldBe("id");
        statement.Columns[1].ColumnName.ShouldBe("name");
    }

    [Fact]
    public void Should_parse_column_aliases()
    {
        var statement = SqlParser.Parse("select id as contact_id, name full_name, [order] from contacts.dbf");

        statement.Columns[0].Alias.ShouldBe("contact_id");
        statement.Columns[0].OutputName.ShouldBe("contact_id");
        statement.Columns[1].Alias.ShouldBe("full_name");
        statement.Columns[2].ColumnName.ShouldBe("order");
        statement.Columns[2].Alias.ShouldBeNull();
    }

    [Theory]
    [InlineData("select top 5 * from t.dbf")]
    [InlineData("select * from t.dbf limit 5")]
    public void Should_parse_row_limits(string commandText)
    {
        var statement = SqlParser.Parse(commandText);

        statement.Top.ShouldBe(5);
    }

    [Fact]
    public void Should_reject_top_and_limit_together()
    {
        Should.Throw<SqlParseException>(() => SqlParser.Parse("select top 5 * from t.dbf limit 5"))
            .Message.ShouldContain("TOP and LIMIT");
    }

    [Theory]
    [InlineData("select top 1.5 * from t.dbf")]
    [InlineData("select top -1 * from t.dbf")]
    [InlineData("select * from t.dbf limit x")]
    public void Should_reject_non_integer_row_limits(string commandText)
    {
        Should.Throw<SqlParseException>(() => SqlParser.Parse(commandText));
    }

    [Fact]
    public void Should_parse_comparison_operators()
    {
        var statement = SqlParser.Parse(
            "select * from t.dbf where a = 1 and b <> 2 and c != 3 and d < 4 and e <= 5 and f > 6 and g >= 7");

        var operators = Flatten(statement.Where, SqlBinaryOperator.And)
            .Cast<SqlBinaryExpression>()
            .Select(b => b.Operator)
            .ToList();

        operators.ShouldBe(new[]
        {
            SqlBinaryOperator.Equal, SqlBinaryOperator.NotEqual, SqlBinaryOperator.NotEqual,
            SqlBinaryOperator.LessThan, SqlBinaryOperator.LessThanOrEqual,
            SqlBinaryOperator.GreaterThan, SqlBinaryOperator.GreaterThanOrEqual
        });
    }

    [Fact]
    public void Should_give_and_precedence_over_or()
    {
        var statement = SqlParser.Parse("select * from t.dbf where a = 1 or b = 2 and c = 3");

        var or = statement.Where.ShouldBeOfType<SqlBinaryExpression>();
        or.Operator.ShouldBe(SqlBinaryOperator.Or);
        or.Left.ShouldBeOfType<SqlBinaryExpression>().Operator.ShouldBe(SqlBinaryOperator.Equal);
        or.Right.ShouldBeOfType<SqlBinaryExpression>().Operator.ShouldBe(SqlBinaryOperator.And);
    }

    [Fact]
    public void Should_let_parentheses_override_precedence()
    {
        var statement = SqlParser.Parse("select * from t.dbf where (a = 1 or b = 2) and c = 3");

        var and = statement.Where.ShouldBeOfType<SqlBinaryExpression>();
        and.Operator.ShouldBe(SqlBinaryOperator.And);
        and.Left.ShouldBeOfType<SqlBinaryExpression>().Operator.ShouldBe(SqlBinaryOperator.Or);
    }

    [Fact]
    public void Should_bind_between_and_to_the_between_not_the_logical_and()
    {
        var statement = SqlParser.Parse("select * from t.dbf where a between 1 and 2 and b = 3");

        var and = statement.Where.ShouldBeOfType<SqlBinaryExpression>();
        and.Operator.ShouldBe(SqlBinaryOperator.And);

        var between = and.Left.ShouldBeOfType<SqlBetweenExpression>();
        between.Low.ShouldBeOfType<SqlLiteralExpression>().Value.ShouldBe(1m);
        between.High.ShouldBeOfType<SqlLiteralExpression>().Value.ShouldBe(2m);
        between.Negated.ShouldBeFalse();
    }

    [Fact]
    public void Should_parse_not_variants()
    {
        var statement = SqlParser.Parse(
            "select * from t.dbf where not a = 1 and b not between 1 and 2 and c not in (1) and d not like 'x%' and e is not null");

        var conjuncts = Flatten(statement.Where, SqlBinaryOperator.And).ToList();

        conjuncts[0].ShouldBeOfType<SqlNotExpression>();
        conjuncts[1].ShouldBeOfType<SqlBetweenExpression>().Negated.ShouldBeTrue();
        conjuncts[2].ShouldBeOfType<SqlInExpression>().Negated.ShouldBeTrue();
        conjuncts[3].ShouldBeOfType<SqlLikeExpression>().Negated.ShouldBeTrue();
        conjuncts[4].ShouldBeOfType<SqlIsNullExpression>().Negated.ShouldBeTrue();
    }

    [Fact]
    public void Should_parse_in_list()
    {
        var statement = SqlParser.Parse("select * from t.dbf where a in (1, 'two', @three, ?)");

        var inExpression = statement.Where.ShouldBeOfType<SqlInExpression>();
        inExpression.Values.Count.ShouldBe(4);
        inExpression.Values[0].ShouldBeOfType<SqlLiteralExpression>().Value.ShouldBe(1m);
        inExpression.Values[1].ShouldBeOfType<SqlLiteralExpression>().Value.ShouldBe("two");
        inExpression.Values[2].ShouldBeOfType<SqlParameterExpression>().Name.ShouldBe("three");
        inExpression.Values[3].ShouldBeOfType<SqlParameterExpression>().Index.ShouldBe(0);
    }

    [Fact]
    public void Should_reject_columns_in_in_lists()
    {
        Should.Throw<SqlParseException>(() => SqlParser.Parse("select * from t.dbf where a in (b)"))
            .Message.ShouldContain("IN lists");
    }

    [Fact]
    public void Should_parse_literals()
    {
        var statement = SqlParser.Parse(
            "select * from t.dbf where a = 'it''s' and b = -1.5 and c = null and d = true and e = false");

        var literals = Flatten(statement.Where, SqlBinaryOperator.And)
            .Cast<SqlBinaryExpression>()
            .Select(b => ((SqlLiteralExpression)b.Right).Value)
            .ToList();

        literals.ShouldBe(new object[] { "it's", -1.5m, null, true, false });
    }

    [Fact]
    public void Should_number_positional_parameters_in_order()
    {
        var statement = SqlParser.Parse("select * from t.dbf where a = ? and b = ? and c = @named");

        var parameters = Flatten(statement.Where, SqlBinaryOperator.And)
            .Cast<SqlBinaryExpression>()
            .Select(b => (SqlParameterExpression)b.Right)
            .ToList();

        parameters[0].Index.ShouldBe(0);
        parameters[1].Index.ShouldBe(1);
        parameters[2].Name.ShouldBe("named");
        parameters[2].Index.ShouldBe(-1);
    }

    [Fact]
    public void Should_parse_order_by()
    {
        var statement = SqlParser.Parse("select * from t.dbf order by a, b asc, c desc");

        statement.OrderBy.Count.ShouldBe(3);
        statement.OrderBy[0].Descending.ShouldBeFalse();
        statement.OrderBy[1].Descending.ShouldBeFalse();
        statement.OrderBy[2].Descending.ShouldBeTrue();
        statement.OrderBy[2].ColumnName.ShouldBe("c");
    }

    [Fact]
    public void Should_throw_with_invalid_query()
    {
        var faker = new Faker();
        var query = faker.Lorem.Sentence();

        Should.Throw<ArgumentException>(() => SqlParser.Parse(query));
    }

    [Theory]
    [InlineData("select * frm t.dbf", "expected FROM")]
    [InlineData("select from t.dbf", "expected a column name")]
    [InlineData("select * from", "expected a table name")]
    [InlineData("select * from t.dbf where", "expected a column, literal or parameter")]
    [InlineData("select * from t.dbf where a =", "expected a column, literal or parameter")]
    [InlineData("select * from t.dbf where a", "expected a comparison operator")]
    [InlineData("select * from t.dbf where a not = 1", "expected BETWEEN, IN or LIKE after NOT")]
    [InlineData("select * from t.dbf order a", "expected BY")]
    [InlineData("select * from t.dbf; extra", "expected end of statement")]
    [InlineData("select a, * from t.dbf", "expected a column name")]
    public void Should_report_helpful_syntax_errors(string commandText, string expectedFragment)
    {
        var exception = Should.Throw<SqlParseException>(() => SqlParser.Parse(commandText));

        exception.Message.ShouldContain(expectedFragment);
        exception.Message.ShouldContain("position");
    }

    [Fact]
    public void Should_report_error_positions()
    {
        var exception = Should.Throw<SqlParseException>(() => SqlParser.Parse("select * frm t.dbf"));

        exception.Position.ShouldBe(9);
    }

    private static System.Collections.Generic.IEnumerable<SqlExpression> Flatten(SqlExpression expression,
        SqlBinaryOperator op)
    {
        if (expression is SqlBinaryExpression binary && binary.Operator == op)
        {
            foreach (var child in Flatten(binary.Left, op)) yield return child;
            foreach (var child in Flatten(binary.Right, op)) yield return child;
        }
        else
        {
            yield return expression;
        }
    }
}
