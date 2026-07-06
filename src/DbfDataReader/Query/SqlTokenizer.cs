using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DbfDataReader.Query
{
    internal static class SqlTokenizer
    {
        private static readonly Dictionary<string, SqlTokenType> Keywords =
            new Dictionary<string, SqlTokenType>(StringComparer.OrdinalIgnoreCase)
            {
                ["SELECT"] = SqlTokenType.SelectKeyword,
                ["TOP"] = SqlTokenType.TopKeyword,
                ["AS"] = SqlTokenType.AsKeyword,
                ["FROM"] = SqlTokenType.FromKeyword,
                ["WHERE"] = SqlTokenType.WhereKeyword,
                ["ORDER"] = SqlTokenType.OrderKeyword,
                ["BY"] = SqlTokenType.ByKeyword,
                ["ASC"] = SqlTokenType.AscKeyword,
                ["DESC"] = SqlTokenType.DescKeyword,
                ["LIMIT"] = SqlTokenType.LimitKeyword,
                ["AND"] = SqlTokenType.AndKeyword,
                ["OR"] = SqlTokenType.OrKeyword,
                ["NOT"] = SqlTokenType.NotKeyword,
                ["BETWEEN"] = SqlTokenType.BetweenKeyword,
                ["IN"] = SqlTokenType.InKeyword,
                ["LIKE"] = SqlTokenType.LikeKeyword,
                ["IS"] = SqlTokenType.IsKeyword,
                ["NULL"] = SqlTokenType.NullKeyword,
                ["TRUE"] = SqlTokenType.TrueKeyword,
                ["FALSE"] = SqlTokenType.FalseKeyword
            };

        public static List<SqlToken> Tokenize(string text)
        {
            var tokens = new List<SqlToken>();
            var position = 0;

            while (position < text.Length)
            {
                var c = text[position];

                if (char.IsWhiteSpace(c))
                {
                    position++;
                    continue;
                }

                if (c == '\'')
                {
                    tokens.Add(ReadStringLiteral(text, ref position));
                }
                else if (c == '"')
                {
                    tokens.Add(ReadQuotedIdentifier(text, ref position));
                }
                else if (c == '[')
                {
                    tokens.Add(ReadBracketedIdentifier(text, ref position));
                }
                else if (c == '@')
                {
                    tokens.Add(ReadNamedParameter(text, ref position));
                }
                else if (IsWordCharacter(c))
                {
                    tokens.Add(ReadWord(text, ref position));
                }
                else
                {
                    tokens.Add(ReadSymbol(text, ref position));
                }
            }

            tokens.Add(new SqlToken(SqlTokenType.EndOfText, string.Empty, text.Length));
            return tokens;
        }

        // words cover identifiers, keywords and numbers; a dot is a word character so that
        // file names such as dbase_03.dbf lex as a single identifier
        private static bool IsWordCharacter(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '.';
        }

        private static SqlToken ReadWord(string text, ref int position)
        {
            var start = position;
            while (position < text.Length && IsWordCharacter(text[position])) position++;

            var word = text.Substring(start, position - start);

            if (decimal.TryParse(word, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _))
                return new SqlToken(SqlTokenType.NumberLiteral, word, start);

            return Keywords.TryGetValue(word, out var keyword)
                ? new SqlToken(keyword, word, start)
                : new SqlToken(SqlTokenType.Identifier, word, start);
        }

        private static SqlToken ReadStringLiteral(string text, ref int position)
        {
            var start = position;
            position++; // opening quote

            var value = new StringBuilder();
            while (position < text.Length)
            {
                var c = text[position];
                if (c == '\'')
                {
                    // two consecutive quotes escape a single quote
                    if (position + 1 < text.Length && text[position + 1] == '\'')
                    {
                        value.Append('\'');
                        position += 2;
                        continue;
                    }

                    position++;
                    return new SqlToken(SqlTokenType.StringLiteral, value.ToString(), start);
                }

                value.Append(c);
                position++;
            }

            throw new SqlParseException($"Syntax error at position {start}: unterminated string literal.", start);
        }

        private static SqlToken ReadQuotedIdentifier(string text, ref int position)
        {
            return ReadDelimitedIdentifier(text, ref position, '"', SqlTokenType.QuotedIdentifier);
        }

        private static SqlToken ReadBracketedIdentifier(string text, ref int position)
        {
            return ReadDelimitedIdentifier(text, ref position, ']', SqlTokenType.BracketedIdentifier);
        }

        private static SqlToken ReadDelimitedIdentifier(string text, ref int position, char closing, SqlTokenType type)
        {
            var start = position;
            position++; // opening delimiter

            var end = text.IndexOf(closing, position);
            if (end < 0)
                throw new SqlParseException($"Syntax error at position {start}: unterminated identifier.", start);

            var value = text.Substring(position, end - position);
            if (value.Length == 0)
                throw new SqlParseException($"Syntax error at position {start}: empty identifier.", start);

            position = end + 1;
            return new SqlToken(type, value, start);
        }

        private static SqlToken ReadNamedParameter(string text, ref int position)
        {
            var start = position;
            position++; // @

            var nameStart = position;
            while (position < text.Length &&
                   (char.IsLetterOrDigit(text[position]) || text[position] == '_')) position++;

            if (position == nameStart)
                throw new SqlParseException($"Syntax error at position {start}: expected a parameter name after '@'.",
                    start);

            return new SqlToken(SqlTokenType.NamedParameter, text.Substring(nameStart, position - nameStart), start);
        }

        private static SqlToken ReadSymbol(string text, ref int position)
        {
            var start = position;
            var c = text[position];

            switch (c)
            {
                case ',':
                    position++;
                    return new SqlToken(SqlTokenType.Comma, ",", start);
                case '(':
                    position++;
                    return new SqlToken(SqlTokenType.LeftParen, "(", start);
                case ')':
                    position++;
                    return new SqlToken(SqlTokenType.RightParen, ")", start);
                case '*':
                    position++;
                    return new SqlToken(SqlTokenType.Star, "*", start);
                case ';':
                    position++;
                    return new SqlToken(SqlTokenType.Semicolon, ";", start);
                case '-':
                    position++;
                    return new SqlToken(SqlTokenType.Minus, "-", start);
                case '?':
                    position++;
                    return new SqlToken(SqlTokenType.PositionalParameter, "?", start);
                case '=':
                    position++;
                    return new SqlToken(SqlTokenType.Equal, "=", start);
                case '<':
                    position++;
                    if (position < text.Length && text[position] == '=')
                    {
                        position++;
                        return new SqlToken(SqlTokenType.LessThanOrEqual, "<=", start);
                    }

                    if (position < text.Length && text[position] == '>')
                    {
                        position++;
                        return new SqlToken(SqlTokenType.NotEqual, "<>", start);
                    }

                    return new SqlToken(SqlTokenType.LessThan, "<", start);
                case '>':
                    position++;
                    if (position < text.Length && text[position] == '=')
                    {
                        position++;
                        return new SqlToken(SqlTokenType.GreaterThanOrEqual, ">=", start);
                    }

                    return new SqlToken(SqlTokenType.GreaterThan, ">", start);
                case '!':
                    if (position + 1 < text.Length && text[position + 1] == '=')
                    {
                        position += 2;
                        return new SqlToken(SqlTokenType.NotEqual, "!=", start);
                    }

                    throw new SqlParseException($"Syntax error at position {start}: unexpected character '!'.", start);
                default:
                    throw new SqlParseException($"Syntax error at position {start}: unexpected character '{c}'.",
                        start);
            }
        }
    }
}
