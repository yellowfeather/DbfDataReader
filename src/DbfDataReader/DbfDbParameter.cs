using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace DbfDataReader
{
    public class DbfDbParameter : DbParameter
    {
        public override DbType DbType { get; set; } = DbType.Object;

        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;

        public override bool IsNullable { get; set; }

        [AllowNull]
        public override string ParameterName { get; set; } = string.Empty;

        public override int Size { get; set; }

        [AllowNull]
        public override string SourceColumn { get; set; } = string.Empty;

        public override bool SourceColumnNullMapping { get; set; }

        public override object? Value { get; set; }

        public override void ResetDbType()
        {
            DbType = DbType.Object;
        }
    }
}
