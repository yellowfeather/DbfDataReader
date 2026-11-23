using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using DbfDataReader.Query;

namespace DbfDataReader
{
    /// <summary>
    /// Fluent configuration of how <typeparamref name="T"/>'s properties map to columns,
    /// in the style of EF Core.
    /// </summary>
    /// <remarks>
    /// Fluent configuration takes precedence over [Column] / [NotMapped], which take
    /// precedence over matching by property name. When a property is configured more than
    /// once, the last call wins.
    /// </remarks>
    /// <example>
    /// <code>
    /// map.Property(r => r.Coverage).HasColumnName("Coverage %");
    /// map.Ignore(r => r.Display);
    /// </code>
    /// </example>
    public sealed class DbfModelBuilder<T>
    {
        private readonly Dictionary<string, ModelMapOverride> _overrides =
            new Dictionary<string, ModelMapOverride>(StringComparer.Ordinal);

        internal DbfModelBuilder()
        {
        }

        // selects a property to configure; this also maps a property marked [NotMapped]
        public DbfPropertyBuilder<T> Property<TProperty>(Expression<Func<T, TProperty>> property)
        {
            var info = GetProperty(property);
            if (!ModelMap.IsMappable(info))
                throw new ArgumentException(
                    $"Property '{typeof(T).Name}.{info.Name}' cannot be mapped; it must have a public setter.",
                    nameof(property));

            // keep a column name given by an earlier Property(...).HasColumnName(...)
            if (!_overrides.TryGetValue(info.Name, out var existing) || existing.Ignored)
                _overrides[info.Name] = new ModelMapOverride(false, null);

            return new DbfPropertyBuilder<T>(this, info.Name);
        }

        // excludes a property, so that no column is required for it
        public DbfModelBuilder<T> Ignore<TProperty>(Expression<Func<T, TProperty>> property)
        {
            _overrides[GetProperty(property).Name] = new ModelMapOverride(true, null);
            return this;
        }

        internal void SetColumnName(string propertyName, string columnName)
        {
            _overrides[propertyName] = new ModelMapOverride(false, columnName);
        }

        internal ModelMap Build()
        {
            return ModelMap.Create(typeof(T), _overrides);
        }

        internal static ModelMap BuildMap(Action<DbfModelBuilder<T>> configure)
        {
            var builder = new DbfModelBuilder<T>();
            configure(builder);
            return builder.Build();
        }

        private static PropertyInfo GetProperty(LambdaExpression property)
        {
            if (property == null) throw new ArgumentNullException(nameof(property));

            var body = property.Body;
            while (body.NodeType == ExpressionType.Convert || body.NodeType == ExpressionType.ConvertChecked)
            {
                body = ((UnaryExpression)body).Operand;
            }

            if (body is MemberExpression member && member.Expression == property.Parameters[0] &&
                member.Member is PropertyInfo)
            {
                // resolve against T so that the mapping is keyed like ModelMap's reflection
                var info = typeof(T).GetProperty(member.Member.Name, BindingFlags.Public | BindingFlags.Instance);
                if (info != null) return info;
            }

            throw new ArgumentException(
                $"'{property}' is not a property of '{typeof(T).Name}'; use a lambda such as x => x.Property.",
                nameof(property));
        }
    }

    public sealed class DbfPropertyBuilder<T>
    {
        private readonly DbfModelBuilder<T> _model;
        private readonly string _propertyName;

        internal DbfPropertyBuilder(DbfModelBuilder<T> model, string propertyName)
        {
            _model = model;
            _propertyName = propertyName;
        }

        // the column the property reads, matched exactly first and then case-insensitively
        public DbfPropertyBuilder<T> HasColumnName(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("A column name is required.", nameof(name));

            _model.SetColumnName(_propertyName, name);
            return this;
        }
    }
}
