using System;
using System.Collections.Generic;

namespace DbfDataReader.Query
{
    internal static class SqlBinder
    {
        // resolves every column reference in the statement to an ordinal in the table's
        // column list, using the same exact-then-case-insensitive rules as GetOrdinal
        public static void Bind(SelectStatement statement, IList<DbfColumn> columns)
        {
            foreach (var selectColumn in statement.Columns)
            {
                selectColumn.Ordinal = ResolveColumn(selectColumn.ColumnName, selectColumn.Position, columns);
            }

            if (statement.Where != null) BindExpression(statement.Where, columns);

            foreach (var orderByItem in statement.OrderBy)
            {
                orderByItem.Ordinal = ResolveOrderByColumn(orderByItem, statement, columns);
            }
        }

        // ORDER BY may reference a select-list alias as well as a table column
        private static int ResolveOrderByColumn(OrderByItem item, SelectStatement statement,
            IList<DbfColumn> columns)
        {
            foreach (var selectColumn in statement.Columns)
            {
                if (selectColumn.Alias != null &&
                    string.Equals(selectColumn.Alias, item.ColumnName, StringComparison.OrdinalIgnoreCase))
                    return selectColumn.Ordinal;
            }

            return ResolveColumn(item.ColumnName, item.Position, columns);
        }

        private static void BindExpression(SqlExpression expression, IList<DbfColumn> columns)
        {
            switch (expression)
            {
                case SqlColumnExpression column:
                    column.Ordinal = ResolveColumn(column.Name, column.Position, columns);
                    break;
                case SqlBinaryExpression binary:
                    BindExpression(binary.Left, columns);
                    BindExpression(binary.Right, columns);
                    break;
                case SqlNotExpression not:
                    BindExpression(not.Operand, columns);
                    break;
                case SqlBetweenExpression between:
                    BindExpression(between.Operand, columns);
                    BindExpression(between.Low, columns);
                    BindExpression(between.High, columns);
                    break;
                case SqlInExpression inExpression:
                    BindExpression(inExpression.Operand, columns);
                    foreach (var value in inExpression.Values)
                    {
                        BindExpression(value, columns);
                    }

                    break;
                case SqlLikeExpression like:
                    BindExpression(like.Operand, columns);
                    BindExpression(like.Pattern, columns);
                    break;
                case SqlIsNullExpression isNull:
                    BindExpression(isNull.Operand, columns);
                    break;
            }
        }

        private static int ResolveColumn(string name, int position, IList<DbfColumn> columns)
        {
            for (var ordinal = 0; ordinal < columns.Count; ordinal++)
            {
                if (columns[ordinal].ColumnName == name) return ordinal;
            }

            for (var ordinal = 0; ordinal < columns.Count; ordinal++)
            {
                if (string.Equals(columns[ordinal].ColumnName, name, StringComparison.OrdinalIgnoreCase))
                    return ordinal;
            }

            throw new SqlParseException($"Unknown column '{name}' at position {position}.", position);
        }
    }
}
