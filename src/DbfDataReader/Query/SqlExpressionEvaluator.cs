using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DbfDataReader.Query
{
    // Evaluates a bound WHERE expression against the current row of a reader, using SQL
    // three-valued logic: comparisons involving NULL are unknown, and a row matches only
    // when the whole expression is true. String comparisons are ordinal, case-sensitive
    // and ignore trailing spaces, matching DBF MACHINE collation and CHAR padding.
    internal sealed class SqlExpressionEvaluator
    {
        private readonly SqlExpression _expression;
        private readonly IReadOnlyDictionary<string, object> _namedParameters;
        private readonly IReadOnlyList<object> _positionalParameters;
        private readonly Dictionary<string, Regex> _likePatterns = new Dictionary<string, Regex>();

        public SqlExpressionEvaluator(SqlExpression expression,
            IReadOnlyDictionary<string, object> namedParameters, IReadOnlyList<object> positionalParameters)
        {
            _expression = expression;
            _namedParameters = namedParameters;
            _positionalParameters = positionalParameters;
        }

        // getValue returns the current row's value for an underlying column ordinal
        public bool Matches(Func<int, object> getValue)
        {
            return EvaluateCondition(_expression, getValue) == true;
        }

        private bool? EvaluateCondition(SqlExpression expression, Func<int, object> reader)
        {
            switch (expression)
            {
                case SqlBinaryExpression binary:
                    return EvaluateBinary(binary, reader);
                case SqlNotExpression not:
                    return Negate(EvaluateCondition(not.Operand, reader));
                case SqlBetweenExpression between:
                    return EvaluateBetween(between, reader);
                case SqlInExpression inExpression:
                    return EvaluateIn(inExpression, reader);
                case SqlLikeExpression like:
                    return EvaluateLike(like, reader);
                case SqlIsNullExpression isNull:
                    return EvaluateIsNull(isNull, reader);
                default:
                    throw new InvalidOperationException(
                        $"The expression at position {expression.Position} cannot be used as a condition.");
            }
        }

        private bool? EvaluateBinary(SqlBinaryExpression binary, Func<int, object> reader)
        {
            switch (binary.Operator)
            {
                case SqlBinaryOperator.And:
                    return EvaluateAnd(binary, reader);
                case SqlBinaryOperator.Or:
                    return EvaluateOr(binary, reader);
                default:
                    return EvaluateComparison(binary, reader);
            }
        }

        private bool? EvaluateAnd(SqlBinaryExpression binary, Func<int, object> reader)
        {
            var left = EvaluateCondition(binary.Left, reader);
            if (left == false) return false;

            var right = EvaluateCondition(binary.Right, reader);
            if (right == false) return false;

            return left == true && right == true ? true : (bool?)null;
        }

        private bool? EvaluateOr(SqlBinaryExpression binary, Func<int, object> reader)
        {
            var left = EvaluateCondition(binary.Left, reader);
            if (left == true) return true;

            var right = EvaluateCondition(binary.Right, reader);
            if (right == true) return true;

            return left == false && right == false ? false : (bool?)null;
        }

        private bool? EvaluateComparison(SqlBinaryExpression binary, Func<int, object> reader)
        {
            var left = EvaluateOperand(binary.Left, reader);
            var right = EvaluateOperand(binary.Right, reader);
            if (left == null || right == null) return null;

            var cmp = CompareValues(left, right, binary.Position);
            switch (binary.Operator)
            {
                case SqlBinaryOperator.Equal: return cmp == 0;
                case SqlBinaryOperator.NotEqual: return cmp != 0;
                case SqlBinaryOperator.LessThan: return cmp < 0;
                case SqlBinaryOperator.LessThanOrEqual: return cmp <= 0;
                case SqlBinaryOperator.GreaterThan: return cmp > 0;
                case SqlBinaryOperator.GreaterThanOrEqual: return cmp >= 0;
                default:
                    throw new InvalidOperationException(
                        $"Unexpected operator at position {binary.Position}.");
            }
        }

        private bool? EvaluateBetween(SqlBetweenExpression between, Func<int, object> reader)
        {
            var value = EvaluateOperand(between.Operand, reader);
            var low = EvaluateOperand(between.Low, reader);
            var high = EvaluateOperand(between.High, reader);

            var lowerBound = value == null || low == null
                ? (bool?)null
                : CompareValues(value, low, between.Position) >= 0;
            if (lowerBound == false) return ApplyNegation(false, between.Negated);

            var upperBound = value == null || high == null
                ? (bool?)null
                : CompareValues(value, high, between.Position) <= 0;

            bool? result;
            if (upperBound == false) result = false;
            else if (lowerBound == true && upperBound == true) result = true;
            else result = null;

            return ApplyNegation(result, between.Negated);
        }

        private bool? EvaluateIn(SqlInExpression inExpression, Func<int, object> reader)
        {
            var value = EvaluateOperand(inExpression.Operand, reader);

            var sawUnknown = value == null;
            var found = false;

            if (value != null)
            {
                foreach (var valueExpression in inExpression.Values)
                {
                    var element = EvaluateOperand(valueExpression, reader);
                    if (element == null)
                    {
                        sawUnknown = true;
                        continue;
                    }

                    if (CompareValues(value, element, inExpression.Position) == 0)
                    {
                        found = true;
                        break;
                    }
                }
            }

            bool? result;
            if (found) result = true;
            else if (sawUnknown) result = null;
            else result = false;

            return ApplyNegation(result, inExpression.Negated);
        }

        private bool? EvaluateLike(SqlLikeExpression like, Func<int, object> reader)
        {
            var value = EvaluateOperand(like.Operand, reader);
            var pattern = EvaluateOperand(like.Pattern, reader);
            if (value == null || pattern == null) return null;

            if (!(value is string text))
                throw new InvalidOperationException(
                    $"LIKE requires a character operand at position {like.Position}.");
            if (!(pattern is string patternText))
                throw new InvalidOperationException(
                    $"LIKE requires a character pattern at position {like.Position}.");

            var result = GetLikePattern(patternText).IsMatch(TrimTrailingSpaces(text));
            return ApplyNegation(result, like.Negated);
        }

        private bool? EvaluateIsNull(SqlIsNullExpression isNull, Func<int, object> reader)
        {
            var value = EvaluateOperand(isNull.Operand, reader);
            return isNull.Negated ? value != null : value == null;
        }

        private object EvaluateOperand(SqlExpression expression, Func<int, object> reader)
        {
            switch (expression)
            {
                case SqlLiteralExpression literal:
                    return literal.Value;
                case SqlColumnExpression column:
                    return reader(column.Ordinal);
                case SqlParameterExpression parameter:
                    return GetParameterValue(parameter);
                default:
                    throw new InvalidOperationException(
                        $"The expression at position {expression.Position} cannot be used as a value.");
            }
        }

        private object GetParameterValue(SqlParameterExpression parameter)
        {
            object value;
            if (parameter.Name != null)
            {
                if (_namedParameters == null || !_namedParameters.TryGetValue(parameter.Name, out value))
                    throw new InvalidOperationException($"Parameter '@{parameter.Name}' was not supplied.");
            }
            else
            {
                if (_positionalParameters == null || parameter.Index >= _positionalParameters.Count)
                    throw new InvalidOperationException(
                        $"Positional parameter {parameter.Index + 1} was not supplied.");

                value = _positionalParameters[parameter.Index];
            }

            if (value == null || value is DBNull) return null;
            if (value is char character) return character.ToString(CultureInfo.InvariantCulture);

            return value;
        }

        private static int CompareValues(object left, object right, int position)
        {
            return SqlValueComparer.CompareValues(left, right, position);
        }

        private Regex GetLikePattern(string pattern)
        {
            if (_likePatterns.TryGetValue(pattern, out var regex)) return regex;

            var translated = "^" + Regex.Escape(pattern).Replace("%", ".*").Replace("_", ".") + "$";
            regex = new Regex(translated, RegexOptions.Singleline, TimeSpan.FromMilliseconds(100));
            _likePatterns[pattern] = regex;

            return regex;
        }

        private static bool? Negate(bool? value)
        {
            return value == null ? (bool?)null : !value.Value;
        }

        private static bool? ApplyNegation(bool? value, bool negated)
        {
            return negated ? Negate(value) : value;
        }

        private static string TrimTrailingSpaces(string value)
        {
            return SqlValueComparer.TrimTrailingSpaces(value);
        }
    }
}
