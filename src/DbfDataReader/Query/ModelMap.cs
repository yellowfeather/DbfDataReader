using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace DbfDataReader.Query
{
    // The single source of truth for which properties of a model type are mapped and
    // which column each one reads. Follows the EF Core conventions: the column name is
    // [Column("name")] when present, otherwise the property name; [NotMapped] properties
    // (and indexers) are skipped. [Column] without a name, and its Order / TypeName, are
    // ignored.
    internal static class ModelMap
    {
        private static readonly ConcurrentDictionary<Type, IReadOnlyList<ModelMapEntry>> Cache =
            new ConcurrentDictionary<Type, IReadOnlyList<ModelMapEntry>>();

        public static IReadOnlyList<ModelMapEntry> For(Type type)
        {
            return Cache.GetOrAdd(type, Build);
        }

        private static IReadOnlyList<ModelMapEntry> Build(Type type)
        {
            var entries = new List<ModelMapEntry>();

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanWrite || property.GetSetMethod() == null) continue;
                if (property.GetIndexParameters().Length > 0) continue;
                if (property.GetCustomAttribute<NotMappedAttribute>(true) != null) continue;

                var columnName = property.GetCustomAttribute<ColumnAttribute>(true)?.Name;
                var isExplicit = !string.IsNullOrEmpty(columnName);

                entries.Add(new ModelMapEntry(property, isExplicit ? columnName : property.Name, isExplicit));
            }

            return entries;
        }
    }

    internal sealed class ModelMapEntry
    {
        public ModelMapEntry(PropertyInfo property, string columnName, bool isExplicit)
        {
            Property = property;
            ColumnName = columnName;
            IsExplicit = isExplicit;
        }

        public PropertyInfo Property { get; }

        public string ColumnName { get; }

        // true when the name came from [Column("name")] rather than the property name
        public bool IsExplicit { get; }
    }
}
