using System.Collections.Generic;

namespace DbfDataReader.Query
{
    internal sealed class SelectStatement
    {
        public SelectStatement(bool isSelectAll, bool isCountAll, IReadOnlyList<SelectColumn> columns,
            string tableName, SqlExpression where, IReadOnlyList<OrderByItem> orderBy, int? top)
        {
            IsSelectAll = isSelectAll;
            IsCountAll = isCountAll;
            Columns = columns;
            TableName = tableName;
            Where = where;
            OrderBy = orderBy;
            Top = top;
        }

        public bool IsSelectAll { get; }

        // the statement is SELECT COUNT(*); Columns is empty
        public bool IsCountAll { get; }

        // empty when IsSelectAll
        public IReadOnlyList<SelectColumn> Columns { get; }

        public string TableName { get; }

        // null when there is no WHERE clause
        public SqlExpression Where { get; }

        // empty when there is no ORDER BY clause
        public IReadOnlyList<OrderByItem> OrderBy { get; }

        // row limit from TOP or LIMIT; null when unlimited
        public int? Top { get; }
    }

    internal sealed class SelectColumn
    {
        public SelectColumn(string columnName, string alias, int position)
        {
            ColumnName = columnName;
            Alias = alias;
            Position = position;
        }

        public string ColumnName { get; }

        public string Alias { get; }

        public string OutputName => Alias ?? ColumnName;

        public int Position { get; }

        // resolved by SqlBinder; -1 until bound
        public int Ordinal { get; set; } = -1;
    }

    internal sealed class OrderByItem
    {
        public OrderByItem(string columnName, bool descending, int position)
        {
            ColumnName = columnName;
            Descending = descending;
            Position = position;
        }

        public string ColumnName { get; }

        public bool Descending { get; }

        public int Position { get; }

        // resolved by SqlBinder; -1 until bound
        public int Ordinal { get; set; } = -1;
    }
}
