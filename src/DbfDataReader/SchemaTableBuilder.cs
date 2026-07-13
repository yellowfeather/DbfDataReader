using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace DbfDataReader
{
    internal static class SchemaTableBuilder
    {
        public static DataTable Build(IReadOnlyList<DbColumn> columnSchema)
        {
            var table = new DataTable("SchemaTable")
            {
                Columns =
                {
                    new DataColumn(SchemaTableColumn.ColumnName, typeof(string)),
                    new DataColumn(SchemaTableColumn.ColumnOrdinal, typeof(int)),
                    new DataColumn(SchemaTableColumn.ColumnSize, typeof(int)),
                    new DataColumn(SchemaTableColumn.NumericPrecision, typeof(short)),
                    new DataColumn(SchemaTableColumn.NumericScale, typeof(short)),
                    new DataColumn(SchemaTableColumn.DataType, typeof(Type)),
                    new DataColumn(SchemaTableColumn.AllowDBNull, typeof(bool)),

                    new DataColumn(SchemaTableColumn.BaseColumnName, typeof(string)),
                    new DataColumn(SchemaTableColumn.BaseSchemaName, typeof(string)),
                    new DataColumn(SchemaTableColumn.BaseTableName, typeof(string)),

                    new DataColumn(SchemaTableColumn.IsAliased, typeof(bool)),
                    new DataColumn(SchemaTableColumn.IsExpression, typeof(bool)),
                    new DataColumn(SchemaTableColumn.IsKey, typeof(bool)),
                    new DataColumn(SchemaTableColumn.IsLong, typeof(bool)),
                    new DataColumn(SchemaTableColumn.IsUnique, typeof(bool)),

                    new DataColumn(SchemaTableColumn.ProviderType, typeof(int)),
                    new DataColumn(SchemaTableColumn.NonVersionedProviderType, typeof(int)),
                }
            };

            object dbNull = DBNull.Value;
            foreach (var column in columnSchema)
            {
                var row = table.NewRow();
                row[0] = column.ColumnName ?? dbNull;
                row[1] = column.ColumnOrdinal ?? dbNull;
                row[2] = column.ColumnSize ?? dbNull;
                row[3] = column.NumericPrecision ?? dbNull;
                row[4] = column.NumericScale ?? dbNull;
                row[5] = column.DataType ?? dbNull;
                row[6] = column.AllowDBNull ?? dbNull;

                row[7] = column.BaseColumnName ?? dbNull;
                row[8] = column.BaseSchemaName ?? dbNull;
                row[9] = column.BaseTableName ?? dbNull;

                row[10] = column.IsAliased ?? dbNull;
                row[11] = column.IsExpression ?? dbNull;
                row[12] = column.IsKey ?? dbNull;
                row[13] = column.IsLong ?? dbNull;
                row[14] = column.IsUnique ?? dbNull;

                var code = (int)Type.GetTypeCode(column.DataType);
                row[15] = code;
                row[16] = code;

                table.Rows.Add(row);
            }

            return table;
        }
    }
}
