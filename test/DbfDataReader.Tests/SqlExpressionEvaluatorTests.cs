using System;
using System.Collections.Generic;
using DbfDataReader.Query;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests;

public class SqlExpressionEvaluatorTests
{
    // literal- and parameter-only expressions never touch the row, so the evaluator can
    // run without a reader; this exercises the coercion matrix and three-valued logic
    // in isolation
    private static bool Matches(string where, Dictionary<string, object> named = null,
        List<object> positional = null)
    {
        var statement = SqlParser.Parse($"select * from t.dbf where {where}");
        var evaluator = new SqlExpressionEvaluator(statement.Where, named, positional);
        return evaluator.Matches(null);
    }

    [Theory]
    [InlineData("1 = 1", true)]
    [InlineData("1 = 2", false)]
    [InlineData("1 = 1.0", true)]
    [InlineData("1.5 > 1", true)]
    [InlineData("-2 < -1", true)]
    [InlineData("2 <> 3", true)]
    [InlineData("2 <= 2", true)]
    [InlineData("3 >= 4", false)]
    public void Should_compare_numbers(string where, bool expected)
    {
        Matches(where).ShouldBe(expected);
    }

    [Theory]
    [InlineData("'ab' = 'ab'", true)]
    [InlineData("'ab' = 'ab   '", true)] // trailing spaces are ignored
    [InlineData("'ab' = 'AB'", false)] // ordinal, case-sensitive
    [InlineData("'abc' < 'abd'", true)]
    [InlineData("' ab' = 'ab'", false)] // leading spaces are significant
    public void Should_compare_strings(string where, bool expected)
    {
        Matches(where).ShouldBe(expected);
    }

    [Theory]
    [InlineData("true = true", true)]
    [InlineData("true <> false", true)]
    [InlineData("false = true", false)]
    public void Should_compare_booleans(string where, bool expected)
    {
        Matches(where).ShouldBe(expected);
    }

    [Theory]
    [InlineData("null = null", false)] // unknown, not true
    [InlineData("not (null = null)", false)] // NOT unknown stays unknown
    [InlineData("null is null", true)]
    [InlineData("null is not null", false)]
    [InlineData("1 is not null", true)]
    [InlineData("null = 1 or 1 = 1", true)] // unknown OR true = true
    [InlineData("null = 1 and 1 = 1", false)] // unknown AND true = unknown
    [InlineData("1 = 2 and null = 1", false)] // false AND unknown = false
    [InlineData("not (1 = 2 and null = 1)", true)] // NOT false = true
    public void Should_apply_three_valued_logic(string where, bool expected)
    {
        Matches(where).ShouldBe(expected);
    }

    [Theory]
    [InlineData("'abc' like 'a%'", true)]
    [InlineData("'abc' like '%c'", true)]
    [InlineData("'abc' like 'a_c'", true)]
    [InlineData("'abc' like 'a_d'", false)]
    [InlineData("'a.c' like 'a_c'", true)]
    [InlineData("'axc' like 'a.c'", false)] // regex characters in the pattern are literal
    [InlineData("'abc' like 'A%'", false)] // case-sensitive
    [InlineData("'abc' not like 'b%'", true)]
    [InlineData("'abc   ' like 'abc'", true)] // trailing spaces on the value are ignored
    [InlineData("'abc' like null", false)] // unknown
    public void Should_evaluate_like(string where, bool expected)
    {
        Matches(where).ShouldBe(expected);
    }

    [Theory]
    [InlineData("1 in (1, 2)", true)]
    [InlineData("3 in (1, 2)", false)]
    [InlineData("1 in (null, 1)", true)] // found despite the null element
    [InlineData("3 in (1, null)", false)] // unknown, not false
    [InlineData("3 not in (1, 2)", true)]
    [InlineData("3 not in (1, null)", false)] // NOT unknown stays unknown
    [InlineData("'b' in ('a', 'b')", true)]
    public void Should_evaluate_in(string where, bool expected)
    {
        Matches(where).ShouldBe(expected);
    }

    [Theory]
    [InlineData("2 between 1 and 3", true)]
    [InlineData("0 between 1 and 3", false)]
    [InlineData("4 between 1 and 3", false)]
    [InlineData("2 not between 1 and 3", false)]
    [InlineData("0 not between 1 and 3", true)]
    [InlineData("null between 1 and 3", false)] // unknown
    [InlineData("2 between null and 3", false)] // unknown
    [InlineData("0 between 1 and null", false)] // below the lower bound: definitively false
    [InlineData("0 not between 1 and null", true)]
    [InlineData("'b' between 'a' and 'c'", true)]
    public void Should_evaluate_between(string where, bool expected)
    {
        Matches(where).ShouldBe(expected);
    }

    [Fact]
    public void Should_coerce_date_strings_when_compared_with_dates()
    {
        var named = new Dictionary<string, object> { ["d"] = new DateTime(2020, 6, 15) };

        Matches("@d >= '2020-01-01'", named).ShouldBeTrue();
        Matches("@d = '2020-06-15'", named).ShouldBeTrue();
        Matches("@d < '2020-06-15 00:00:01'", named).ShouldBeTrue();
        Matches("@d between '2020-01-01' and '2020-12-31'", named).ShouldBeTrue();
    }

    [Fact]
    public void Should_throw_for_unparseable_date_strings()
    {
        var named = new Dictionary<string, object> { ["d"] = new DateTime(2020, 6, 15) };

        var exception = Should.Throw<InvalidOperationException>(() => Matches("@d = 'not-a-date'", named));

        exception.Message.ShouldContain("Cannot convert 'not-a-date' to a date");
    }

    [Fact]
    public void Should_resolve_named_and_positional_parameters()
    {
        var named = new Dictionary<string, object> { ["id"] = 5 };
        var positional = new List<object> { 7 };

        Matches("@id = 5", named).ShouldBeTrue();
        Matches("? = 7", positional: positional).ShouldBeTrue();
        Matches("@id = 5.0", named).ShouldBeTrue(); // int parameter vs decimal literal
    }

    [Fact]
    public void Should_treat_dbnull_parameters_as_null()
    {
        var named = new Dictionary<string, object> { ["id"] = DBNull.Value };

        Matches("@id = 1", named).ShouldBeFalse(); // unknown
        Matches("@id is null", named).ShouldBeTrue();
    }

    [Fact]
    public void Should_compare_char_parameters_as_strings()
    {
        var named = new Dictionary<string, object> { ["c"] = 'x' };

        Matches("@c = 'x'", named).ShouldBeTrue();
    }

    [Theory]
    [InlineData("@missing = 1", "Parameter '@missing' was not supplied")]
    [InlineData("? = 1", "Positional parameter 1 was not supplied")]
    public void Should_throw_for_missing_parameters(string where, string expectedFragment)
    {
        var exception = Should.Throw<InvalidOperationException>(() => Matches(where));

        exception.Message.ShouldContain(expectedFragment);
    }

    [Fact]
    public void Should_throw_for_type_mismatches_with_the_position()
    {
        var exception = Should.Throw<InvalidOperationException>(() => Matches("'a' > 1"));

        exception.Message.ShouldContain("Cannot compare");
        exception.Message.ShouldContain("position");
    }

    [Fact]
    public void Should_throw_for_like_on_non_string_operands()
    {
        Should.Throw<InvalidOperationException>(() => Matches("1 like 'a%'"))
            .Message.ShouldContain("LIKE requires a character operand");
    }
}
