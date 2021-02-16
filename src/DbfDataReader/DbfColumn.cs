using System;
using System.Data.Common;
using System.IO;
using System.Text;

namespace DbfDataReader
{
    public class DbfColumn : DbColumn
    {
        private readonly Encoding _encoding;

        public DbfColumn(BinaryReader binaryReader, int ordinal, Encoding encoding)
        {
            ColumnOrdinal = ordinal;
            _encoding = encoding;
            Read(binaryReader);
        }

        public DbfColumnType ColumnType { get; private set; }
        public int Length { get; private set; }
        public int DecimalCount { get; private set; }

        private void Read(BinaryReader binaryReader)
        {
            var rawName = binaryReader.ReadString(11, _encoding);
            var nullIdx = rawName.IndexOf((char)0);
            if (nullIdx >= 0)
            {
                rawName = rawName.Substring(0, nullIdx);   // trim off everything past & including the first NUL byte
            }
			ColumnName = rawName;

            var type = binaryReader.ReadByte();
            ColumnType = (DbfColumnType) type;

            // ignore field data address
            binaryReader.ReadUInt32();

            Length = binaryReader.ReadByte();
            DecimalCount = binaryReader.ReadByte();

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
            binaryReader.ReadBytes(14);
        }

        private Type GetDataType(DbfColumnType columnType)
        {
            switch (columnType)
            {
                case DbfColumnType.Number:
                    return DecimalCount == 0 ? typeof(int) : typeof(decimal);
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
                    return typeof(string);
                default:
                    return typeof(object);
            }
        }
    }
}