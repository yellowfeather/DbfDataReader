using System;
using System.Collections.Generic;
using System.IO;

namespace DbfReader
{
    public class DbfRecord
    {
        private const byte EndOfFile = 0x1a;

        public DbfRecord(DbfTable dbfTable)
        {
            Values = new List<IDbfValue>();

            foreach (var dbfColumn in dbfTable.Columns)
            {
                var dbfValue = CreateDbfValue(dbfColumn);
                Values.Add(dbfValue);
            }
        }

        private static IDbfValue CreateDbfValue(DbfColumn dbfColumn)
        {
            IDbfValue value;

            switch (dbfColumn.ColumnType)
            {
                case DbfColumnType.Number:
                    if (dbfColumn.DecimalCount == 0)
                    {
                        value = new DbfValueInt(dbfColumn.Length);
                    }
                    else
                    {
                        value = new DbfValueDecimal(dbfColumn.Length, dbfColumn.DecimalCount);
                    }
                    break;
                case DbfColumnType.Signedlong:
                    value = new DbfValueLong();
                    break;
                case DbfColumnType.Float:
                    value = new DbfValueFloat();
                    break;
                case DbfColumnType.Currency:
                    value = new DbfValueCurrency();
                    break;
                case DbfColumnType.Date:
                    value = new DbfValueDate();
                    break;
                case DbfColumnType.DateTime:
                    value = new DbfValueDateTime();
                    break;
                case DbfColumnType.Boolean:
                    value = new DbfValueBoolean();
                    break;
                case DbfColumnType.Memo:
                    value = new DbfValueMemo(dbfColumn.Length);
                    break;
                case DbfColumnType.Double:
                    value = new DbfValueDouble();
                    break;
                case DbfColumnType.General:
                case DbfColumnType.Character:
                    value = new DbfValueString(dbfColumn.Length);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return value;
        }

        public bool Read(BinaryReader binaryReader)
        {
            if (binaryReader.BaseStream.Position == binaryReader.BaseStream.Length)
            {
                return false;
            }

            try
            {
                var value = binaryReader.ReadByte();
                if (value == EndOfFile)
                {
                    return false;
                }

                IsDeleted = (value == 0x2A);

                foreach (var dbfValue in Values)
                {
                    dbfValue.Read(binaryReader);
                }
                return true;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }

        public bool IsDeleted { get; private set; }

        public IList<IDbfValue> Values { get; set; }
    }
}