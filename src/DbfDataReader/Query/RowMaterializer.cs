using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace DbfDataReader.Query
{
    // Builds cached, compiled delegates that turn a row of values into a T. Class types
    // are populated through their public settable properties, matched to source columns
    // by name (exact first, then case-insensitively); every settable property must have
    // a matching column. Scalar types (string, primitives, decimal, DateTime and their
    // nullable forms) map the first column.
    internal static class RowMaterializer
    {
        private static readonly ConcurrentDictionary<(Type Type, string Signature), Delegate> Cache =
            new ConcurrentDictionary<(Type, string), Delegate>();

        private static readonly MethodInfo ConvertValueMethod =
            ((Func<object, Type, string, object>)ConvertValue).Method;

        public static Func<object[], T> Create<T>(IReadOnlyList<string> sourceNames)
        {
            var key = (typeof(T), string.Join("\u001f", sourceNames));
            return (Func<object[], T>)Cache.GetOrAdd(key, _ => Build<T>(sourceNames));
        }

        public static bool IsScalar(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            return type == typeof(string) || underlying.IsPrimitive || underlying == typeof(decimal) ||
                   underlying == typeof(DateTime);
        }

        private static Func<object[], T> Build<T>(IReadOnlyList<string> sourceNames)
        {
            if (IsScalar(typeof(T)))
            {
                var name = sourceNames.Count > 0 ? sourceNames[0] : "0";
                return row => (T)ConvertValue(row[0], typeof(T), name);
            }

            if (typeof(T).GetConstructor(Type.EmptyTypes) == null)
                throw new InvalidOperationException(
                    $"Type '{typeof(T).Name}' must have a public parameterless constructor to be materialized.");

            var row = Expression.Parameter(typeof(object[]), "row");
            var bindings = new List<MemberBinding>();

            foreach (var property in GetSettableProperties(typeof(T)))
            {
                var ordinal = FindSourceOrdinal(property.Name, sourceNames);
                if (ordinal < 0)
                    throw new InvalidOperationException(
                        $"No column matches property '{typeof(T).Name}.{property.Name}'.");

                var value = Expression.Call(ConvertValueMethod,
                    Expression.ArrayIndex(row, Expression.Constant(ordinal)),
                    Expression.Constant(property.PropertyType),
                    Expression.Constant(sourceNames[ordinal]));

                bindings.Add(Expression.Bind(property, Expression.Convert(value, property.PropertyType)));
            }

            if (bindings.Count == 0)
                throw new InvalidOperationException(
                    $"Type '{typeof(T).Name}' has no public settable properties to map.");

            var body = Expression.MemberInit(Expression.New(typeof(T)), bindings);
            return Expression.Lambda<Func<object[], T>>(body, row).Compile();
        }

        internal static IEnumerable<PropertyInfo> GetSettableProperties(Type type)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.CanWrite && property.GetSetMethod() != null) yield return property;
            }
        }

        internal static int FindSourceOrdinal(string propertyName, IReadOnlyList<string> sourceNames)
        {
            for (var ordinal = 0; ordinal < sourceNames.Count; ordinal++)
            {
                if (sourceNames[ordinal] == propertyName) return ordinal;
            }

            for (var ordinal = 0; ordinal < sourceNames.Count; ordinal++)
            {
                if (string.Equals(sourceNames[ordinal], propertyName, StringComparison.OrdinalIgnoreCase))
                    return ordinal;
            }

            return -1;
        }

        private static object ConvertValue(object value, Type targetType, string sourceName)
        {
            if (value == null)
            {
                if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null) return null;

                throw new InvalidOperationException(
                    $"Column '{sourceName}' contains a null value that cannot be assigned to " +
                    $"'{targetType.Name}'; use a nullable type.");
            }

            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (underlying.IsInstanceOfType(value)) return value;

            try
            {
                return Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
            }
            catch (Exception exception) when (exception is InvalidCastException ||
                                              exception is FormatException || exception is OverflowException)
            {
                throw new InvalidCastException(
                    $"Unable to convert column '{sourceName}' value of type '{value.GetType().Name}' " +
                    $"to '{targetType.Name}'.");
            }
        }
    }
}
