using System;
using System.Collections.Generic;
using System.Globalization;

namespace DbfDataReader.Query
{
    internal static class SqlParser
    {
        public static SelectStatement Parse(string commandText)
        {
            if (commandText == null) throw new ArgumentNullException(nameof(commandText));

            var tokens = SqlTokenizer.Tokenize(commandText);
            return new Parser(tokens).ParseSelectStatement();
        }

        private sealed class Parser
        {
            private readonly List<SqlToken> _tokens;
            private int _index;
            private int _positionalParameterCount;

            public Parser(List<SqlToken> tokens)
            {
                _tokens = tokens;
            }

            private SqlToken Current => _tokens[_index];

            public SelectStatement ParseSelectStatement()
            {
                Expect(SqlTokenType.SelectKeyword, "SELECT");

                int? top = null;
                if (Match(SqlTokenType.TopKeyword)) top = ParseNonNegativeInteger("TOP");

                var isSelectAll = false;
                var isCountAll = false;
                var columns = new List<SelectColumn>();
                if (Match(SqlTokenType.Star))
                {
                    isSelectAll = true;
                }
                else if (IsCountFunction())
                {
                    ParseCountAll();
                    isCountAll = true;

                    if (Current.Type == SqlTokenType.Comma)
                        throw Error("COUNT(*) must be the only item in the select list", Current);
                }
                else
                {
                    do
                    {
                        columns.Add(ParseSelectColumn());
                    } while (Match(SqlTokenType.Comma));
                }

                Expect(SqlTokenType.FromKeyword, "FROM");
                var tableName = ParseName("a table name");

                SqlExpression where = null;
                if (Match(SqlTokenType.WhereKeyword)) where = ParseOrExpression();

                var orderBy = new List<OrderByItem>();
                if (Match(SqlTokenType.OrderKeyword))
                {
                    Expect(SqlTokenType.ByKeyword, "BY");
                    do
                    {
                        orderBy.Add(ParseOrderByItem());
                    } while (Match(SqlTokenType.Comma));
                }

                if (Current.Type == SqlTokenType.LimitKeyword)
                {
                    if (top != null)
                        throw Error("TOP and LIMIT cannot both be specified", Current);

                    _index++;
                    top = ParseNonNegativeInteger("LIMIT");
                }

                Match(SqlTokenType.Semicolon);
                Expect(SqlTokenType.EndOfText, "end of statement");

                if (isCountAll && orderBy.Count > 0)
                    throw Error("ORDER BY cannot be used with COUNT(*)",
                        new SqlToken(SqlTokenType.OrderKeyword, "ORDER", orderBy[0].Position));

                return new SelectStatement(isSelectAll, isCountAll, columns, tableName, where, orderBy, top);
            }

            // COUNT is not a reserved word: it only acts as a function when followed by
            // a parenthesis, so columns named "count" keep working
            private bool IsCountFunction()
            {
                return Current.Type == SqlTokenType.Identifier &&
                       string.Equals(Current.Value, "count", StringComparison.OrdinalIgnoreCase) &&
                       _tokens[_index + 1].Type == SqlTokenType.LeftParen;
            }

            private void ParseCountAll()
            {
                _index++; // count
                _index++; // (
                Expect(SqlTokenType.Star, "'*'");
                Expect(SqlTokenType.RightParen, "')'");
            }

            private SelectColumn ParseSelectColumn()
            {
                var position = Current.Position;
                var columnName = ParseName("a column name");

                string alias = null;
                if (Match(SqlTokenType.AsKeyword))
                {
                    alias = ParseName("an alias");
                }
                else if (IsName(Current.Type))
                {
                    alias = Current.Value;
                    _index++;
                }

                return new SelectColumn(columnName, alias, position);
            }

            private OrderByItem ParseOrderByItem()
            {
                var position = Current.Position;
                var columnName = ParseName("a column name");

                var descending = false;
                if (Match(SqlTokenType.DescKeyword)) descending = true;
                else Match(SqlTokenType.AscKeyword);

                return new OrderByItem(columnName, descending, position);
            }

            private SqlExpression ParseOrExpression()
            {
                var left = ParseAndExpression();
                while (Match(SqlTokenType.OrKeyword))
                {
                    left = new SqlBinaryExpression(SqlBinaryOperator.Or, left, ParseAndExpression());
                }

                return left;
            }

            private SqlExpression ParseAndExpression()
            {
                var left = ParseNotExpression();
                while (Match(SqlTokenType.AndKeyword))
                {
                    left = new SqlBinaryExpression(SqlBinaryOperator.And, left, ParseNotExpression());
                }

                return left;
            }

            private SqlExpression ParseNotExpression()
            {
                if (Current.Type == SqlTokenType.NotKeyword)
                {
                    var position = Current.Position;
                    _index++;
                    return new SqlNotExpression(ParseNotExpression(), position);
                }

                return ParsePredicate();
            }

            private SqlExpression ParsePredicate()
            {
                if (Match(SqlTokenType.LeftParen))
                {
                    var inner = ParseOrExpression();
                    Expect(SqlTokenType.RightParen, "')'");
                    return inner;
                }

                var operand = ParseOperand();

                var binaryOperator = TryGetBinaryOperator(Current.Type);
                if (binaryOperator != null)
                {
                    _index++;
                    return new SqlBinaryExpression(binaryOperator.Value, operand, ParseOperand());
                }

                if (Match(SqlTokenType.IsKeyword))
                {
                    var negated = Match(SqlTokenType.NotKeyword);
                    Expect(SqlTokenType.NullKeyword, "NULL");
                    return new SqlIsNullExpression(operand, negated);
                }

                var negatedOperator = Match(SqlTokenType.NotKeyword);

                if (Match(SqlTokenType.BetweenKeyword))
                {
                    var low = ParseOperand();
                    // this AND belongs to BETWEEN, not to the logical expression
                    Expect(SqlTokenType.AndKeyword, "AND");
                    var high = ParseOperand();
                    return new SqlBetweenExpression(operand, low, high, negatedOperator);
                }

                if (Match(SqlTokenType.InKeyword))
                {
                    Expect(SqlTokenType.LeftParen, "'('");
                    var values = new List<SqlExpression>();
                    do
                    {
                        values.Add(ParseInListValue());
                    } while (Match(SqlTokenType.Comma));

                    Expect(SqlTokenType.RightParen, "')'");
                    return new SqlInExpression(operand, values, negatedOperator);
                }

                if (Match(SqlTokenType.LikeKeyword))
                {
                    return new SqlLikeExpression(operand, ParseOperand(), negatedOperator);
                }

                throw Error(
                    negatedOperator ? "expected BETWEEN, IN or LIKE after NOT" : "expected a comparison operator",
                    Current);
            }

            private SqlExpression ParseInListValue()
            {
                var token = Current;
                var value = ParseOperand();
                if (value is SqlColumnExpression)
                    throw Error("IN lists may only contain literals or parameters", token);

                return value;
            }

            private SqlExpression ParseOperand()
            {
                var token = Current;
                switch (token.Type)
                {
                    case SqlTokenType.Identifier:
                    case SqlTokenType.QuotedIdentifier:
                    case SqlTokenType.BracketedIdentifier:
                        _index++;
                        return new SqlColumnExpression(token.Value, token.Position);
                    case SqlTokenType.StringLiteral:
                        _index++;
                        return new SqlLiteralExpression(token.Value, token.Position);
                    case SqlTokenType.NumberLiteral:
                        _index++;
                        return new SqlLiteralExpression(ParseNumber(token), token.Position);
                    case SqlTokenType.Minus:
                        _index++;
                        var number = Expect(SqlTokenType.NumberLiteral, "a number");
                        return new SqlLiteralExpression(-ParseNumber(number), token.Position);
                    case SqlTokenType.NullKeyword:
                        _index++;
                        return new SqlLiteralExpression(null, token.Position);
                    case SqlTokenType.TrueKeyword:
                        _index++;
                        return new SqlLiteralExpression(true, token.Position);
                    case SqlTokenType.FalseKeyword:
                        _index++;
                        return new SqlLiteralExpression(false, token.Position);
                    case SqlTokenType.NamedParameter:
                        _index++;
                        return new SqlParameterExpression(token.Value, -1, token.Position);
                    case SqlTokenType.PositionalParameter:
                        _index++;
                        return new SqlParameterExpression(null, _positionalParameterCount++, token.Position);
                    default:
                        throw Error("expected a column, literal or parameter", token);
                }
            }

            private static decimal ParseNumber(SqlToken token)
            {
                return decimal.Parse(token.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
            }

            private int ParseNonNegativeInteger(string clause)
            {
                var token = Expect(SqlTokenType.NumberLiteral, $"an integer after {clause}");
                if (token.Value.IndexOf('.') >= 0 || !int.TryParse(token.Value, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var value))
                    throw Error($"expected an integer after {clause}", token);

                return value;
            }

            private string ParseName(string what)
            {
                var token = Current;
                if (!IsName(token.Type)) throw Error($"expected {what}", token);

                _index++;
                return token.Value;
            }

            private static bool IsName(SqlTokenType type)
            {
                return type == SqlTokenType.Identifier ||
                       type == SqlTokenType.QuotedIdentifier ||
                       type == SqlTokenType.BracketedIdentifier;
            }

            private static SqlBinaryOperator? TryGetBinaryOperator(SqlTokenType type)
            {
                switch (type)
                {
                    case SqlTokenType.Equal: return SqlBinaryOperator.Equal;
                    case SqlTokenType.NotEqual: return SqlBinaryOperator.NotEqual;
                    case SqlTokenType.LessThan: return SqlBinaryOperator.LessThan;
                    case SqlTokenType.LessThanOrEqual: return SqlBinaryOperator.LessThanOrEqual;
                    case SqlTokenType.GreaterThan: return SqlBinaryOperator.GreaterThan;
                    case SqlTokenType.GreaterThanOrEqual: return SqlBinaryOperator.GreaterThanOrEqual;
                    default: return null;
                }
            }

            private bool Match(SqlTokenType type)
            {
                if (Current.Type != type) return false;

                _index++;
                return true;
            }

            private SqlToken Expect(SqlTokenType type, string expected)
            {
                var token = Current;
                if (token.Type != type) throw Error($"expected {expected}", token);

                _index++;
                return token;
            }

            private static SqlParseException Error(string message, SqlToken token)
            {
                var found = token.Type == SqlTokenType.EndOfText ? "end of text" : $"'{token.Value}'";
                return new SqlParseException(
                    $"Syntax error at position {token.Position}: {message}, found {found}.", token.Position);
            }
        }
    }
}
