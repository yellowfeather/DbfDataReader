using System.Data;
using System.Data.Common;

namespace DbfDataReader
{
    public class DbfDbParameter : DbParameter
    {
        public override DbType DbType { get; set; } = DbType.Object;

        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;

        public override bool IsNullable { get; set; }

        public override string ParameterName { get; set; }

        public override int Size { get; set; }

        public override string SourceColumn { get; set; }

        public override bool SourceColumnNullMapping { get; set; }

        public override object Value { get; set; }

        public override void ResetDbType()
        {
            DbType = DbType.Object;
        }
    }
}
