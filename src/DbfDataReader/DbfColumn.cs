using System;
using System.Data.Common;
using System.Text;

namespace DbfDataReader
{
    public class DbfColumn : DbColumn
    {
        public const int DbfColumnSize = 32;

        public DbfColumn(ReadOnlySpan<byte> bytes, int start, int ordinal, Encoding encoding)
        {
            Start = start;
            ColumnOrdinal = ordinal;
            Read(bytes, encoding);
        }

        public DbfColumnType ColumnType { get; private set; }
        public int Start { get; }
        public int Length { get; private set; }
        public int DecimalCount { get; private set; }

        private void Read(ReadOnlySpan<byte> bytes, Encoding encoding)
        {
            var rawName = encoding.GetString(bytes.Slice(0, 11));
            var nullIdx = rawName.IndexOf((char)0);
            if (nullIdx >= 0)
            {
                rawName = rawName.Substring(0, nullIdx);   // trim off everything past & including the first NUL byte
            }
            ColumnName = rawName;

            ColumnType = (DbfColumnType) bytes[11];

            // ignore field data address

            var length = bytes[16];
            var decimalCount = bytes[17];

            if (ColumnType == DbfColumnType.Character)
            {
                Length =  BitConverter.ToInt16(bytes.Slice(16, 2));
                DecimalCount = 0;
            }
            else if (ColumnType == DbfColumnType.WideCharacter)
            {
                Length =  BitConverter.ToInt16(bytes.Slice(16, 2));
                DecimalCount = 0;
            }
            else
            {
                Length = length;
                DecimalCount = decimalCount;
            }

            DataType = GetDataType(ColumnType);
            DataTypeName = DataType.ToString();

            // skip the reserved bytes:
            // - Int16: reserved1
            // - Byte: workarea_id
            // - Byte: reserved2
            // - Byte: reserved3
            // - Byte: set_fields_flag
            // - String7: reserved4
            // - Byte: index_field_flag
        }

        private Type GetDataType(DbfColumnType columnType)
        {
            switch (columnType)
            {
                case DbfColumnType.Number:
                    if (DecimalCount == 0) {
                        if (Lenght < 10) {
                            return typeof(int)
                        }
                        else {
                            return typeof(long);
                        }
                    }
                    else {
                        return typeof(decimal);
                    }
                case DbfColumnType.SignedLong:
                    return typeof(long);
                case DbfColumnType.Float:
                    return typeof(float);
                case DbfColumnType.Currency:
                    return typeof(decimal);
                case DbfColumnType.Date:
                    return typeof(DateTime);
                case DbfColumnType.DateTime:
                    return typeof(DateTime);
                case DbfColumnType.Boolean:
                    return typeof(bool);
                case DbfColumnType.Memo:
                    return typeof(string);
                case DbfColumnType.Double:
                    return typeof(double);
                case DbfColumnType.General:
                case DbfColumnType.Character:
                case DbfColumnType.WideCharacter:
                    return typeof(string);
                default:
                    return typeof(object);
            }
        }
    }
}
