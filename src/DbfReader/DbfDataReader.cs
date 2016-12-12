using System;
using System.Collections;
using System.Data.Common;
using System.Text;

namespace DbfReader
{
    public class DbfDataReader : DbDataReader
    {
        private readonly DbfTable _dbfTable;
        private readonly DbfRecord _dbfRecord;

        public DbfDataReader(string path)
        {
            _dbfTable = new DbfTable(path);
            _dbfRecord = new DbfRecord(_dbfTable);
        }

        public DbfDataReader(string path, Encoding encoding)
        {
            _dbfTable = new DbfTable(path, encoding);
            _dbfRecord = new DbfRecord(_dbfTable);
        }

        public new void Dispose()
        {
            _dbfTable?.Dispose();
        }

        public DbfRecord ReadRecord()
        {
            return _dbfTable.ReadRecord();
        }

        public T GetNullableValue<T>(int ordinal) where T : struct
        {
            var value = _dbfRecord.GetValue(ordinal);
            var nullableValue = value as Nullable<T>;
            return nullableValue.Value;
        }

        public override bool GetBoolean(int ordinal)
        {
            return GetNullableValue<bool>(ordinal);
        }

        public override byte GetByte(int ordinal)
        {
            return GetNullableValue<byte>(ordinal);
        }

        public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
        {
            throw new System.NotImplementedException();
        }

        public override char GetChar(int ordinal)
        {
            return GetNullableValue<char>(ordinal);
        }

        public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length)
        {
            throw new System.NotImplementedException();
        }

        public override string GetDataTypeName(int ordinal)
        {
            throw new System.NotImplementedException();
        }

        public override DateTime GetDateTime(int ordinal)
        {
            return GetNullableValue<DateTime>(ordinal);
        }

        public override decimal GetDecimal(int ordinal)
        {
            return GetNullableValue<decimal>(ordinal);
        }

        public override double GetDouble(int ordinal)
        {
            return GetNullableValue<double>(ordinal);
        }

        public override IEnumerator GetEnumerator()
        {
            throw new System.NotImplementedException();
        }

        public override bool NextResult()
        {
            return false;
        }

        public override bool Read()
        {
            return _dbfTable.Read(_dbfRecord);
        }

        public override int Depth
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsClosed => _dbfTable.IsClosed;

        public override int RecordsAffected
        {
            get { throw new NotImplementedException(); }
        }

        public override object this[string name]
        {
            get 
            {
                var ordinal = GetOrdinal(name);
                return GetValue(ordinal); 
            }
        }

        public override object this[int ordinal]
        {
            get { return GetValue(ordinal); }
        }

        public override int FieldCount => _dbfTable.Columns.Count;

        public override bool HasRows => _dbfTable.Header.RecordCount > 0;

        public override bool IsDBNull(int ordinal)
        {
            var value = GetValue(ordinal);
            return value == null;
        }

        public override int GetValues(object[] values)
        {
            throw new System.NotImplementedException();
        }

        public override object GetValue(int ordinal)
        {
            return _dbfRecord.GetValue(ordinal);
        }

        public override string GetString(int ordinal)
        {
            return _dbfRecord.GetValue<string>(ordinal);
        }

        public override int GetOrdinal(string name)
        {
            int ordinal = 0;

            foreach (var dbfColumn in _dbfTable.Columns)
            {
                if (dbfColumn.Name == name)
                {
                    return ordinal;
                }
                ordinal++;
            }

            return -1;
        }

        public override string GetName(int ordinal)
        {
            var dbfColumn = _dbfTable.Columns[ordinal]; 
            return dbfColumn.Name;
        }

        public override long GetInt64(int ordinal)
        {
            return GetNullableValue<long>(ordinal);
        }

        public override int GetInt32(int ordinal)
        {
            return GetNullableValue<int>(ordinal);
        }

        public override short GetInt16(int ordinal)
        {
            return GetNullableValue<short>(ordinal);
        }

        public override Guid GetGuid(int ordinal)
        {
            throw new System.NotImplementedException();
        }

        public override float GetFloat(int ordinal)
        {
            return GetNullableValue<float>(ordinal);
        }

        public override Type GetFieldType(int ordinal)
        {
            throw new System.NotImplementedException();
        }
    }
}