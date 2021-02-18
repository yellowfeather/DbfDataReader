using System;

namespace DbfDataReader.Benchmarks
{
    public static class DbfDataReaderExtensions
    {
        public static void ReadField(this DbfDataReader dbfDataReader, int ordinal)
        {
            var fieldType = dbfDataReader.GetFieldType(ordinal);
            var typeCode = Type.GetTypeCode(fieldType);
            switch (typeCode)
            {
                case TypeCode.Boolean:
                    dbfDataReader.GetBoolean(ordinal);
                    break;
                case TypeCode.Int32:
                    dbfDataReader.GetInt32(ordinal);
                    break;
                case TypeCode.DateTime:
                    dbfDataReader.GetDateTime(ordinal);
                    break;
                case TypeCode.Single:
                    dbfDataReader.GetFloat(ordinal);
                    break;
                case TypeCode.Double:
                    dbfDataReader.GetDouble(ordinal);
                    break;
                case TypeCode.Decimal:
                    dbfDataReader.GetDecimal(ordinal);
                    break;
                case TypeCode.String:
                    dbfDataReader.GetString(ordinal);
                    break;
                case TypeCode.Object:
                    dbfDataReader.GetValue(ordinal);
                    break;
                default:
                    Console.WriteLine($"Unrecognised type: fieldType: {fieldType}, typeCode: {typeCode} ");
                    // no cheating
                    throw new NotSupportedException();
            }
        }
    }
}