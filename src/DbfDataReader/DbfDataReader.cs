using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;

namespace DbfDataReader
{
    public class DbfDataReader : DbDataReader, IDbColumnSchemaGenerator
    {
        private readonly DbfDataReaderOptions _options;

        public DbfDataReader(string path)
            : this(path, new DbfDataReaderOptions())
        {
        }

        public DbfDataReader(string path, DbfDataReaderOptions options)
        {
            _options = options;
            DbfTable = new DbfTable(path, options.Encoding, options.StringTrimming);
            DbfRecord = new DbfRecord(DbfTable);
        }

        public DbfDataReader(Stream stream, DbfDataReaderOptions options)
        {
            _options = options;
            DbfTable = new DbfTable(stream, options.Encoding, options.StringTrimming);
            DbfRecord = new DbfRecord(DbfTable);
        }

        public DbfDataReader(Stream stream, Stream memoStream, DbfDataReaderOptions options)
        {
            _options = options;
            DbfTable = new DbfTable(stream, memoStream, options.Encoding, options.StringTrimming);
            DbfRecord = new DbfRecord(DbfTable);
        }

        public DbfTable DbfTable { get; private set; }

        public DbfRecord DbfRecord { get; private set; }

        public override void Close()
        {
            try
            {
                DbfTable?.Close();
            }
            finally
            {
                DbfTable = null;
                DbfRecord = null;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Close();
            }
        }

        public DbfRecord ReadRecord()
        {
            DbfRecord dbfRecord;
            bool skip;
            do
            {
                dbfRecord = DbfTable.ReadRecord();
                if (dbfRecord == null)
                    break;

                skip = _options.SkipDeletedRecords && DbfRecord.IsDeleted;
            } while (skip);

            return dbfRecord;
        }

        public T GetValue<T>(int ordinal)
        {
            return DbfRecord.GetValue<T>(ordinal);
        }

        public T? GetNullableValue<T>(int ordinal) where T : struct
        {
            return GetValue<T>(ordinal);
        }

        public override bool GetBoolean(int ordinal)
        {
            return (bool) GetValue(ordinal);
        }

        public override byte GetByte(int ordinal)
        {
            return GetValue<byte>(ordinal);
        }

        public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
        {
            throw new NotImplementedException();
        }

        public override char GetChar(int ordinal)
        {
            return GetValue<char>(ordinal);
        }

        public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length)
        {
            throw new NotImplementedException();
        }

        public override string GetDataTypeName(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override DateTime GetDateTime(int ordinal)
        {
            return GetValue<DateTime>(ordinal);
        }

        public override decimal GetDecimal(int ordinal)
        {
            return GetValue<decimal>(ordinal);
        }

        public override double GetDouble(int ordinal)
        {
            return GetValue<double>(ordinal);
        }

        public override IEnumerator GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public override bool NextResult()
        {
            return false;
        }

        public override bool Read()
        {
            bool result;
            bool skip;
            do
            {
                result = DbfTable.Read(DbfRecord);
                if (!result)
                    break;

                skip = _options.SkipDeletedRecords && DbfRecord.IsDeleted;
            } while (skip);

            return result;
        }

        public override int Depth => throw new NotImplementedException();

        public override bool IsClosed => DbfTable.IsClosed;

        public override int RecordsAffected => throw new NotImplementedException();

        public override object this[string name]
        {
            get
            {
                var ordinal = GetOrdinal(name);
                return GetValue(ordinal);
            }
        }

        public override object this[int ordinal] => GetValue(ordinal);

        public override int FieldCount => DbfTable.Columns.Count;

        public override bool HasRows => DbfTable.Header.RecordCount > 0;

        public override bool IsDBNull(int ordinal)
        {
            var value = GetValue(ordinal);
            return value == null;
        }

        public override int GetValues(object[] values)
        {
            throw new NotImplementedException();
        }

        public override object GetValue(int ordinal)
        {
            return DbfRecord.GetValue(ordinal);
        }

        public override string GetString(int ordinal)
        {
            return DbfRecord.GetStringValue(ordinal);
        }

        public override int GetOrdinal(string name)
        {
            var ordinal = 0;

            foreach (var dbfColumn in DbfTable.Columns)
            {
                if (dbfColumn.ColumnName == name) return ordinal;
                ordinal++;
            }
            ordinal = 0; 
            foreach (var dbfColumn in DbfTable.Columns)
            {
                if (String.Equals(dbfColumn.ColumnName,name,StringComparison.OrdinalIgnoreCase)) return ordinal;
                ordinal++;
            }

            throw new IndexOutOfRangeException();
        }

        public override string GetName(int ordinal)
        {
            var dbfColumn = DbfTable.Columns[ordinal];
            return dbfColumn.ColumnName;
        }

        public override long GetInt64(int ordinal)
        {
            return GetValue<long>(ordinal);
        }

        public override int GetInt32(int ordinal)
        {
            return GetValue<int>(ordinal);
        }

        public override short GetInt16(int ordinal)
        {
            return GetValue<short>(ordinal);
        }

        public override Guid GetGuid(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override float GetFloat(int ordinal)
        {
            return GetValue<float>(ordinal);
        }

        public override Type GetFieldType(int ordinal)
        {
            return DbfRecord.GetFieldType(ordinal);
        }

        public ReadOnlyCollection<DbColumn> GetColumnSchema()
        {
            var columns = DbfTable.Columns.Select(c => c as DbColumn).ToList();
            return columns.AsReadOnly();
        }

        public override DataTable GetSchemaTable()
        {
            var columnSchema = GetColumnSchema();
            return GetSchemaTable(columnSchema);
        }

		public static DataTable GetSchemaTable(ReadOnlyCollection<DbColumn> columnSchema)
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