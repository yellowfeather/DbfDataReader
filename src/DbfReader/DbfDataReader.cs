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

        public override bool GetBoolean(int ordinal)
        {
            throw new System.NotImplementedException();
        }

        public override byte GetByte(int ordinal)
        {
            throw new System.NotImplementedException();
        }

        public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
        {
            throw new System.NotImplementedException();
        }

        public override char GetChar(int ordinal)
        {
            throw new System.NotImplementedException();
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
            throw new System.NotImplementedException();
        }

        public override decimal GetDecimal(int ordinal)
        {
            throw new System.NotImplementedException();
        }

        public override double GetDouble(int ordinal)
        {
            throw new System.NotImplementedException();
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
            get { throw new System.NotImplementedException(); }
        }

        public override object this[int ordinal]
        {
            get { throw new System.NotImplementedException(); }
        }

        public override int FieldCount => _dbfTable.Columns.Count;

        public override bool HasRows => _dbfTable.Header.RecordCount > 0;

        public override bool IsDBNull(int ordinal)
        {
            throw new System.NotImplementedException();
        }

        public override int GetValues(object[] values)
        {
            throw new System.NotImplementedException();
        }

        public override object GetValue(int ordinal)
        {
            throw new System.NotImplementedException();
        }

        public override string GetString(int ordinal)
        {
            throw new System.NotImplementedException();
        }

        public override int GetOrdinal(string name)
        {
            throw new System.NotImplementedException();
        }

        public override string GetName(int ordinal)
        {
            throw new System.NotImplementedException();
        }

        public override long GetInt64(int ordinal)
        {
            throw new System.NotImplementedException();
        }

        public override int GetInt32(int ordinal)
        {
            throw new System.NotImplementedException();
        }

        public override short GetInt16(int ordinal)
        {
            throw new System.NotImplementedException();
        }

        public override Guid GetGuid(int ordinal)
        {
            throw new System.NotImplementedException();
        }

        public override float GetFloat(int ordinal)
        {
            throw new System.NotImplementedException();
        }

        public override Type GetFieldType(int ordinal)
        {
            throw new System.NotImplementedException();
        }
    }
}