using System.Data.Common;

namespace DbfDataReader.Query
{
    // schema for a projected column: the underlying column's shape under its output
    // name and projected ordinal
    internal sealed class DbfQueryColumn : DbColumn
    {
        public DbfQueryColumn(DbfColumn column, string outputName, int ordinal)
        {
            ColumnName = outputName;
            ColumnOrdinal = ordinal;
            DataType = column.DataType;
            DataTypeName = column.DataTypeName;
            BaseColumnName = column.ColumnName;
        }
    }
}
