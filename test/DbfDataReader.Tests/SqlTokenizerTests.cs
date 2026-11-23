using System.Linq;
using DbfDataReader.Query;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests;

public class SqlTokenizerTests
{
    [Fact]
    public void Should_tokenize_a_full_statement()
    {
        var tokens = SqlTokenizer.Tokenize("select id, name from contacts.dbf where a >= 1.5;");

        tokens.Select(t => t.Type).ShouldBe(new[]
        {
            SqlTokenType.SelectKeyword, SqlTokenType.Identifier, SqlTokenType.Comma, SqlTokenType.Identifier,
            SqlTokenType.FromKeyword, SqlTokenType.Identifier, SqlTokenType.WhereKeyword, SqlTokenType.Identifier,
            SqlTokenType.GreaterThanOrEqual, SqlTokenType.NumberLiteral, SqlTokenType.Semicolon,
            SqlTokenType.EndOfText
        });
    }

    [Theory]
    [InlineData("dbase_03.dbf", (int)SqlTokenType.Identifier)]
    [InlineData("2023_data.dbf", (int)SqlTokenType.Identifier)]
    [InlineData("file.name.dbf", (int)SqlTokenType.Identifier)]
    [InlineData("123", (int)SqlTokenType.NumberLiteral)]
    [InlineData("123.45", (int)SqlTokenType.NumberLiteral)]
    [InlineData("select", (int)SqlTokenType.SelectKeyword)]
    [InlineData("SeLeCt", (int)SqlTokenType.SelectKeyword)]
    public void Should_classify_words(string text, int expectedType)
    {
        var tokens = SqlTokenizer.Tokenize(text);

        tokens[0].Type.ShouldBe((SqlTokenType)expectedType);
        tokens[0].Value.ShouldBe(text);
    }

    [Fact]
    public void Should_unescape_string_literals()
    {
        var tokens = SqlTokenizer.Tokenize("'it''s a test'");

        tokens[0].Type.ShouldBe(SqlTokenType.StringLiteral);
        tokens[0].Value.ShouldBe("it's a test");
    }

    [Fact]
    public void Should_read_delimited_identifiers()
    {
        var tokens = SqlTokenizer.Tokenize("\"file with spaces.dbf\" [another file.dbf]");

        tokens[0].Type.ShouldBe(SqlTokenType.QuotedIdentifier);
        tokens[0].Value.ShouldBe("file with spaces.dbf");
        tokens[1].Type.ShouldBe(SqlTokenType.BracketedIdentifier);
        tokens[1].Value.ShouldBe("another file.dbf");
    }

    [Fact]
    public void Should_read_parameters()
    {
        var tokens = SqlTokenizer.Tokenize("@name ?");

        tokens[0].Type.ShouldBe(SqlTokenType.NamedParameter);
        tokens[0].Value.ShouldBe("name");
        tokens[1].Type.ShouldBe(SqlTokenType.PositionalParameter);
    }

    [Fact]
    public void Should_tokenize_operators_without_whitespace()
    {
        var tokens = SqlTokenizer.Tokenize("a<=1 and b<>2 and c!=3 and SELECT*FROM");

        tokens.Select(t => t.Type).ShouldContain(SqlTokenType.LessThanOrEqual);
        tokens.Select(t => t.Type).ShouldContain(SqlTokenType.Star);
        tokens.Count(t => t.Type == SqlTokenType.NotEqual).ShouldBe(2);
    }

    [Fact]
    public void Should_record_token_positions()
    {
        var tokens = SqlTokenizer.Tokenize("select *");

        tokens[0].Position.ShouldBe(0);
        tokens[1].Position.ShouldBe(7);
        tokens[2].Position.ShouldBe(8); // end of text
    }

    [Theory]
    [InlineData("'unterminated", "unterminated string")]
    [InlineData("[unterminated", "unterminated identifier")]
    [InlineData("\"unterminated", "unterminated identifier")]
    [InlineData("\"\"", "empty identifier")]
    [InlineData("@ name", "parameter name")]
    [InlineData("a ! b", "unexpected character '!'")]
    [InlineData("a ~ b", "unexpected character '~'")]
    public void Should_report_lexical_errors(string text, string expectedFragment)
    {
        var exception = Should.Throw<SqlParseException>(() => SqlTokenizer.Tokenize(text));

        exception.Message.ShouldContain(expectedFragment);
    }
}
