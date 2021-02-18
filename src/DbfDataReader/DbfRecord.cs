using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DbfDataReader
{
    public class DbfRecord
    {
        private const byte EndOfFile = 0x1a;

        private readonly Encoding _encoding;
        private readonly int _recordLength;
        private readonly byte[] _buffer;

        public DbfRecord(DbfTable dbfTable)
        {
            _encoding = dbfTable.CurrentEncoding;
            _recordLength = dbfTable.Header.RecordLength;
            _buffer = new byte[_recordLength];

            Values = new List<IDbfValue>();

            foreach (var dbfColumn in dbfTable.Columns)
            {
                var dbfValue = CreateDbfValue(dbfColumn, dbfTable.Memo);
                Values.Add(dbfValue);
            }
        }

        public bool IsDeleted { get; private set; }

        public IList<IDbfValue> Values { get; set; }

        private IDbfValue CreateDbfValue(DbfColumn dbfColumn, DbfMemo memo)
        {
            IDbfValue value;

            switch (dbfColumn.ColumnType)
            {
                case DbfColumnType.Number:
                    if (dbfColumn.DecimalCount == 0)
                        value = new DbfValueInt(dbfColumn.Length);
                    else
                        value = new DbfValueDecimal(dbfColumn.Length, dbfColumn.DecimalCount);
                    break;
                case DbfColumnType.SignedLong:
                    value = new DbfValueLong(dbfColumn.Length);
                    break;
                case DbfColumnType.Float:
                    value = new DbfValueFloat(dbfColumn.Length, dbfColumn.DecimalCount);
                    break;
                case DbfColumnType.Currency:
                    value = new DbfValueCurrency(dbfColumn.Length, dbfColumn.DecimalCount);
                    break;
                case DbfColumnType.Date:
                    value = new DbfValueDate(dbfColumn.Length);
                    break;
                case DbfColumnType.DateTime:
                    value = new DbfValueDateTime(dbfColumn.Length);
                    break;
                case DbfColumnType.Boolean:
                    value = new DbfValueBoolean(dbfColumn.Length);
                    break;
                case DbfColumnType.Memo:
                    value = new DbfValueMemo(dbfColumn.Length, memo, _encoding);
                    break;
                case DbfColumnType.Double:
                    value = new DbfValueDouble(dbfColumn.Length, dbfColumn.DecimalCount);
                    break;
                case DbfColumnType.General:
                case DbfColumnType.Character:
                    value = new DbfValueString(dbfColumn.Length, _encoding);
                    break;
                default:
                    value = new DbfValueNull(dbfColumn.Length);
                    break;
            }

            return value;
        }

        public bool Read(Stream stream)
        {
            if (stream.Position == stream.Length) return false;

            try
            {
                stream.Read(_buffer, 0, _recordLength);
                var memoryStream = new MemoryStream(_buffer, false);
                var binaryReader = new BinaryReader(memoryStream, _encoding);

                var value = binaryReader.ReadByte();
                if (value == EndOfFile) return false;

                IsDeleted = value == 0x2A;

                foreach (var dbfValue in Values) dbfValue.Read(binaryReader);
                return true;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }

        public object GetValue(int ordinal)
        {
            var dbfValue = Values[ordinal];
            return dbfValue.GetValue();
        }

        public T GetValue<T>(int ordinal)
        {
            var dbfValue = Values[ordinal];
            try
            {
                return (T) dbfValue.GetValue();
            }
            catch (InvalidCastException)
            {
                throw new InvalidCastException(
                    $"Unable to cast object of type '{dbfValue.GetValue().GetType().FullName}' to type '{typeof(T).FullName}' at ordinal '{ordinal}'.");
            }
        }

        public Type GetFieldType(int ordinal)
        {
            var dbfValue = Values[ordinal];
            return dbfValue.GetFieldType();
        }
    }
}