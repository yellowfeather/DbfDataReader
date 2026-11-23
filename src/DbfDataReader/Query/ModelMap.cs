using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DbfDataReader.Query
{
    // The single source of truth for which properties of a model type are mapped and
    // which column each one reads. Follows the EF Core conventions and precedence:
    // fluent configuration (DbfModelBuilder<T>) first, then [Column("name")] /
    // [NotMapped], then the property name. Indexers are never mapped. [Column] without
    // a name, and its Order / TypeName, are ignored.
    internal sealed class ModelMap
    {
        private static readonly ConcurrentDictionary<Type, ModelMap> Cache =
            new ConcurrentDictionary<Type, ModelMap>();

        private static readonly IReadOnlyDictionary<string, ModelMapOverride> NoOverrides =
            new Dictionary<string, ModelMapOverride>();

        private ModelMap(IReadOnlyList<ModelMapEntry> entries, string signature)
        {
            Entries = entries;
            Signature = signature;
        }

        public IReadOnlyList<ModelMapEntry> Entries { get; }

        // identifies the fluent configuration, so that compiled materializers can be
        // cached per mapping; empty for the attribute / convention mapping
        public string Signature { get; }

        // the mapping from attributes and conventions alone
        public static ModelMap For(Type type)
        {
            return Cache.GetOrAdd(type, t => new ModelMap(Build(t, NoOverrides), string.Empty));
        }

        // the mapping with fluent overrides, keyed by property name, applied on top
        public static ModelMap Create(Type type, IReadOnlyDictionary<string, ModelMapOverride> overrides)
        {
            if (overrides.Count == 0) return For(type);

            return new ModelMap(Build(type, overrides), BuildSignature(overrides));
        }

        internal static bool IsMappable(PropertyInfo property)
        {
            return property.CanWrite && property.GetSetMethod() != null &&
                   property.GetIndexParameters().Length == 0;
        }

        private static IReadOnlyList<ModelMapEntry> Build(Type type,
            IReadOnlyDictionary<string, ModelMapOverride> overrides)
        {
            var entries = new List<ModelMapEntry>();

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!IsMappable(property)) continue;

                // a fluent override, when present, decides whether the property is mapped
                var configured = overrides.TryGetValue(property.Name, out var fluent);
                var ignored = configured
                    ? fluent.Ignored
                    : property.GetCustomAttribute<NotMappedAttribute>(true) != null;
                if (ignored) continue;

                var columnName = fluent?.ColumnName ?? property.GetCustomAttribute<ColumnAttribute>(true)?.Name;
                var isExplicit = !string.IsNullOrEmpty(columnName);

                entries.Add(new ModelMapEntry(property, isExplicit ? columnName : property.Name, isExplicit));
            }

            return entries;
        }

        private static string BuildSignature(IReadOnlyDictionary<string, ModelMapOverride> overrides)
        {
            var builder = new StringBuilder();

            foreach (var pair in overrides.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                builder.Append(pair.Key)
                    .Append(pair.Value.Ignored ? '!' : '=')
                    .Append(pair.Value.ColumnName)
                    .Append('\u001e');
            }

            return builder.ToString();
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

        // true when the name came from configuration rather than the property name
        public bool IsExplicit { get; }
    }

    // A fluent decision about one property: ignored, or mapped - to ColumnName when set,
    // otherwise to the attribute / convention name.
    internal sealed class ModelMapOverride
    {
        public ModelMapOverride(bool ignored, string columnName)
        {
            Ignored = ignored;
            ColumnName = columnName;
        }

        public bool Ignored { get; }

        public string ColumnName { get; }
    }
}
