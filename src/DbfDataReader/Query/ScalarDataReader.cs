using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace DbfDataReader.Query
{
    // a single-row, single-column result, used for aggregate queries such as COUNT(*)
    internal sealed class ScalarDataReader : DbDataReader, IDbColumnSchemaGenerator
    {
        private readonly string _name;
        private readonly object _value;
        private int _rowsRemaining;
        private bool _hasCurrentRow;

        public ScalarDataReader(string name, object value, int rowCount)
        {
            _name = name;
            _value = value;
            _rowsRemaining = rowCount;
        }

        public override int FieldCount => 1;

        public override bool HasRows => _rowsRemaining > 0 || _hasCurrentRow;

        public override bool IsClosed => false;

        public override int Depth => 0;

        public override int RecordsAffected => -1;

        public override object this[int ordinal] => GetValue(ordinal);

        public override object this[string name] => GetValue(GetOrdinal(name));

        public override bool Read()
        {
            if (_rowsRemaining <= 0)
            {
                _hasCurrentRow = false;
                return false;
            }

            _rowsRemaining--;
            _hasCurrentRow = true;
            return true;
        }

        public override Task<bool> ReadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Read());
        }

        public override bool NextResult()
        {
            return false;
        }

        public override string GetName(int ordinal)
        {
            CheckOrdinal(ordinal);
            return _name;
        }

        public override int GetOrdinal(string name)
        {
            if (string.Equals(name, _name, StringComparison.OrdinalIgnoreCase)) return 0;

            // DbDataReader.GetOrdinal is documented to throw IndexOutOfRangeException
            // for unknown column names
#pragma warning disable S112
            throw new IndexOutOfRangeException();
#pragma warning restore S112
        }

        public override Type GetFieldType(int ordinal)
        {
            CheckOrdinal(ordinal);
            return _value.GetType();
        }

        public override string GetDataTypeName(int ordinal)
        {
            return GetFieldType(ordinal).Name;
        }

        public override object GetValue(int ordinal)
        {
            CheckOrdinal(ordinal);
            return _value;
        }

        public override int GetValues(object[] values)
        {
            values[0] = _value;
            return 1;
        }

        public override bool IsDBNull(int ordinal)
        {
            CheckOrdinal(ordinal);
            return false;
        }

        public override T GetFieldValue<T>(int ordinal)
        {
            var value = GetValue(ordinal);
            if (value is T typed) return typed;

            return (T)Convert.ChangeType(value, typeof(T),
                System.Globalization.CultureInfo.InvariantCulture);
        }

        public override bool GetBoolean(int ordinal) => GetFieldValue<bool>(ordinal);
        public override byte GetByte(int ordinal) => GetFieldValue<byte>(ordinal);
        public override char GetChar(int ordinal) => GetFieldValue<char>(ordinal);
        public override DateTime GetDateTime(int ordinal) => GetFieldValue<DateTime>(ordinal);
        public override decimal GetDecimal(int ordinal) => GetFieldValue<decimal>(ordinal);
        public override double GetDouble(int ordinal) => GetFieldValue<double>(ordinal);
        public override float GetFloat(int ordinal) => GetFieldValue<float>(ordinal);
        public override short GetInt16(int ordinal) => GetFieldValue<short>(ordinal);
        public override int GetInt32(int ordinal) => GetFieldValue<int>(ordinal);
        public override long GetInt64(int ordinal) => GetFieldValue<long>(ordinal);
        public override string GetString(int ordinal) => GetFieldValue<string>(ordinal);

        public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
        {
            throw new NotImplementedException();
        }

        public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length)
        {
            throw new NotImplementedException();
        }

        public override Guid GetGuid(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override IEnumerator GetEnumerator()
        {
            return new DbEnumerator(this, closeReader: false);
        }

        public ReadOnlyCollection<DbColumn> GetColumnSchema()
        {
            return new List<DbColumn> { new ScalarColumn(_name, _value.GetType()) }.AsReadOnly();
        }

        public override DataTable GetSchemaTable()
        {
            return SchemaTableBuilder.Build(GetColumnSchema());
        }

        private static void CheckOrdinal(int ordinal)
        {
            if (ordinal != 0) throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        private sealed class ScalarColumn : DbColumn
        {
            public ScalarColumn(string name, Type type)
            {
                ColumnName = name;
                ColumnOrdinal = 0;
                DataType = type;
                DataTypeName = type.Name;
            }
        }
    }
}
