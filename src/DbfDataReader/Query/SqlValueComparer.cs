using System;
using System.Globalization;

namespace DbfDataReader.Query
{
    // The dialect's comparison semantics, shared by the WHERE evaluator and ORDER BY:
    // ordinal case-sensitive string comparison ignoring trailing spaces, exact decimal
    // comparison unless a float/double is involved, and date columns coerced from ISO
    // strings. Callers handle nulls; both values must be non-null.
    internal static class SqlValueComparer
    {
        private static readonly string[] DateTimeFormats =
        {
            "yyyy-MM-dd",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss"
        };

        public static int CompareValues(object left, object right, int position)
        {
            if (left is string leftText && right is string rightText)
                return string.CompareOrdinal(TrimTrailingSpaces(leftText), TrimTrailingSpaces(rightText));

            if (IsNumber(left) && IsNumber(right)) return CompareNumbers(left, right);

            if (left is DateTime || right is DateTime)
                return ToDateTime(left, position).CompareTo(ToDateTime(right, position));

            if (left is bool leftBool && right is bool rightBool) return leftBool.CompareTo(rightBool);

            throw new InvalidOperationException(
                $"Cannot compare values of type {left.GetType().Name} and {right.GetType().Name} " +
                $"at position {position}.");
        }

        public static string TrimTrailingSpaces(string value)
        {
            return value.TrimEnd(' ');
        }

        private static int CompareNumbers(object left, object right)
        {
            if (left is double || left is float || right is double || right is float)
                return Convert.ToDouble(left, CultureInfo.InvariantCulture)
                    .CompareTo(Convert.ToDouble(right, CultureInfo.InvariantCulture));

            return Convert.ToDecimal(left, CultureInfo.InvariantCulture)
                .CompareTo(Convert.ToDecimal(right, CultureInfo.InvariantCulture));
        }

        private static bool IsNumber(object value)
        {
            switch (value)
            {
                case byte _:
                case sbyte _:
                case short _:
                case ushort _:
                case int _:
                case uint _:
                case long _:
                case ulong _:
                case float _:
                case double _:
                case decimal _:
                    return true;
                default:
                    return false;
            }
        }

        internal static bool TryParseDateTime(string text, out DateTime value)
        {
            return DateTime.TryParseExact(text, DateTimeFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out value);
        }

        private static DateTime ToDateTime(object value, int position)
        {
            switch (value)
            {
                case DateTime dateTime:
                    return dateTime;
                case string text:
                    if (TryParseDateTime(text, out var parsed)) return parsed;

                    throw new InvalidOperationException(
                        $"Cannot convert '{text}' to a date at position {position}; " +
                        "use 'yyyy-MM-dd' or 'yyyy-MM-dd HH:mm:ss'.");
                default:
                    throw new InvalidOperationException(
                        $"Cannot compare a date with a value of type {value.GetType().Name} " +
                        $"at position {position}.");
            }
        }
    }
}
