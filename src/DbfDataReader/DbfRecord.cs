using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DbfDataReader
{
    public class DbfRecord
    {
        private const byte EndOfFile = 0x1a;

        public DbfRecord(DbfTable dbfTable)
        {
            Values = new List<IDbfValue>();

            foreach (var dbfColumn in dbfTable.Columns)
            {
                var dbfValue = CreateDbfValue(dbfColumn, dbfTable.Memo, dbfTable.CurrentEncoding);
                Values.Add(dbfValue);
            }
        }

        public bool IsDeleted { get; private set; }

        public IList<IDbfValue> Values { get; set; }

        private static IDbfValue CreateDbfValue(DbfColumn dbfColumn, DbfMemo memo, Encoding encoding)
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
                    value = new DbfValueMemo(dbfColumn.Length, memo, encoding);
                    break;
                case DbfColumnType.Double:
                    value = new DbfValueDouble(dbfColumn.Length, dbfColumn.DecimalCount);
                    break;
                case DbfColumnType.General:
                case DbfColumnType.Character:
                    value = new DbfValueString(dbfColumn.Length, encoding);
                    break;
                default:
                    value = new DbfValueNull(dbfColumn.Length);
                    break;
            }

            return value;
        }

        public bool Read(BinaryReader binaryReader)
        {
            if (binaryReader.BaseStream.Position == binaryReader.BaseStream.Length) return false;

            try
            {
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