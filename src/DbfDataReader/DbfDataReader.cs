using System;
using System.Collections;
using System.Data.Common;
using System.IO;

namespace DbfDataReader
{
    public class DbfDataReader : DbDataReader
    {
        private readonly DbfDataReaderOptions _options;

        public DbfDataReader(string path)
            : this(path, new DbfDataReaderOptions())
        {
        }

        public DbfDataReader(string path, DbfDataReaderOptions options)
        {
            _options = options;
            DbfTable = new DbfTable(path, options.Encoding);
            DbfRecord = new DbfRecord(DbfTable);
        }

        public DbfDataReader(Stream stream, DbfDataReaderOptions options)
        {
            _options = options;
            DbfTable = new DbfTable(stream, options.Encoding);
            DbfRecord = new DbfRecord(DbfTable);
        }

        public DbfDataReader(Stream stream, Stream memoStream, DbfDataReaderOptions options)
        {
            _options = options;
            DbfTable = new DbfTable(stream, memoStream, options.Encoding);
            DbfRecord = new DbfRecord(DbfTable);
        }

        public DbfTable DbfTable { get; private set; }

        public DbfRecord DbfRecord { get; private set; }

#if NETSTANDARD1_6_1
        public void Close()
#else
        public override void Close()
#endif
        {
            try
            {
                DbfTable.Close();
            }
            finally
            {
                DbfTable = null;
                DbfRecord = null;
            }
        }

#if NETSTANDARD1_6_1
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Close();
            }
        }
#endif

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
            return DbfRecord.GetValue<string>(ordinal);
        }

        public override int GetOrdinal(string name)
        {
            var ordinal = 0;

            foreach (var dbfColumn in DbfTable.Columns)
            {
                if (dbfColumn.Name == name) return ordinal;
                ordinal++;
            }

            return -1;
        }

        public override string GetName(int ordinal)
        {
            var dbfColumn = DbfTable.Columns[ordinal];
            return dbfColumn.Name;
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
    }
}