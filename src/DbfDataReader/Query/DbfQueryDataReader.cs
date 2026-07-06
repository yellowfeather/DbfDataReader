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
    // wraps the raw table reader, exposing only the projected columns under their
    // output names and enforcing the TOP/LIMIT row limit
    internal sealed class DbfQueryDataReader : DbDataReader, IDbColumnSchemaGenerator
    {
        private readonly DbfDataReader _reader;
        private readonly IReadOnlyList<DbfColumn> _columns; // underlying columns, in projected order
        private readonly IReadOnlyList<int> _ordinals; // projected ordinal -> underlying ordinal
        private readonly IReadOnlyList<string> _names; // output names (aliases applied)
        private readonly int _limit; // -1 when unlimited
        private int _rowsReturned;

        public DbfQueryDataReader(DbfDataReader reader, SelectStatement statement)
        {
            _reader = reader;

            var tableColumns = reader.DbfTable.Columns;
            var columns = new List<DbfColumn>();
            var ordinals = new List<int>();
            var names = new List<string>();

            if (statement.IsSelectAll)
            {
                for (var ordinal = 0; ordinal < tableColumns.Count; ordinal++)
                {
                    columns.Add(tableColumns[ordinal]);
                    ordinals.Add(ordinal);
                    names.Add(tableColumns[ordinal].ColumnName);
                }
            }
            else
            {
                foreach (var selectColumn in statement.Columns)
                {
                    columns.Add(tableColumns[selectColumn.Ordinal]);
                    ordinals.Add(selectColumn.Ordinal);
                    names.Add(selectColumn.OutputName);
                }
            }

            _columns = columns;
            _ordinals = ordinals;
            _names = names;
            _limit = statement.Top ?? -1;
        }

        public override int FieldCount => _names.Count;

        public override bool HasRows => _limit != 0 && _reader.HasRows;

        public override bool IsClosed => _reader.IsClosed;

        public override int Depth => 0;

        public override int RecordsAffected => -1;

        public override object this[int ordinal] => GetValue(ordinal);

        public override object this[string name] => GetValue(GetOrdinal(name));

        public override bool Read()
        {
            if (LimitReached()) return false;

            var result = _reader.Read();
            if (result) _rowsReturned++;

            return result;
        }

        public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
        {
            if (LimitReached()) return false;

            var result = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (result) _rowsReturned++;

            return result;
        }

        private bool LimitReached()
        {
            return _limit >= 0 && _rowsReturned >= _limit;
        }

        public override bool NextResult()
        {
            return false;
        }

        public override string GetName(int ordinal)
        {
            return _names[ordinal];
        }

        public override int GetOrdinal(string name)
        {
            for (var ordinal = 0; ordinal < _names.Count; ordinal++)
            {
                if (_names[ordinal] == name) return ordinal;
            }

            for (var ordinal = 0; ordinal < _names.Count; ordinal++)
            {
                if (string.Equals(_names[ordinal], name, StringComparison.OrdinalIgnoreCase)) return ordinal;
            }

            throw new IndexOutOfRangeException();
        }

        public override Type GetFieldType(int ordinal)
        {
            return _reader.GetFieldType(_ordinals[ordinal]);
        }

        public override string GetDataTypeName(int ordinal)
        {
            return _columns[ordinal].ColumnType.ToString();
        }

        public override object GetValue(int ordinal)
        {
            return _reader.GetValue(_ordinals[ordinal]);
        }

        public override int GetValues(object[] values)
        {
            for (var ordinal = 0; ordinal < FieldCount; ordinal++)
            {
                values[ordinal] = GetValue(ordinal);
            }

            return FieldCount;
        }

        public override bool IsDBNull(int ordinal)
        {
            return _reader.IsDBNull(_ordinals[ordinal]);
        }

        public override bool GetBoolean(int ordinal)
        {
            return _reader.GetBoolean(_ordinals[ordinal]);
        }

        public override byte GetByte(int ordinal)
        {
            return _reader.GetByte(_ordinals[ordinal]);
        }

        public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
        {
            return _reader.GetBytes(_ordinals[ordinal], dataOffset, buffer, bufferOffset, length);
        }

        public override char GetChar(int ordinal)
        {
            return _reader.GetChar(_ordinals[ordinal]);
        }

        public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length)
        {
            return _reader.GetChars(_ordinals[ordinal], dataOffset, buffer, bufferOffset, length);
        }

        public override DateTime GetDateTime(int ordinal)
        {
            return _reader.GetDateTime(_ordinals[ordinal]);
        }

        public override decimal GetDecimal(int ordinal)
        {
            return _reader.GetDecimal(_ordinals[ordinal]);
        }

        public override double GetDouble(int ordinal)
        {
            return _reader.GetDouble(_ordinals[ordinal]);
        }

        public override float GetFloat(int ordinal)
        {
            return _reader.GetFloat(_ordinals[ordinal]);
        }

        public override Guid GetGuid(int ordinal)
        {
            return _reader.GetGuid(_ordinals[ordinal]);
        }

        public override short GetInt16(int ordinal)
        {
            return _reader.GetInt16(_ordinals[ordinal]);
        }

        public override int GetInt32(int ordinal)
        {
            return _reader.GetInt32(_ordinals[ordinal]);
        }

        public override long GetInt64(int ordinal)
        {
            return _reader.GetInt64(_ordinals[ordinal]);
        }

        public override string GetString(int ordinal)
        {
            return _reader.GetString(_ordinals[ordinal]);
        }

        public override IEnumerator GetEnumerator()
        {
            return new DbEnumerator(this, closeReader: false);
        }

        public ReadOnlyCollection<DbColumn> GetColumnSchema()
        {
            var schema = new List<DbColumn>(_columns.Count);
            for (var ordinal = 0; ordinal < _columns.Count; ordinal++)
            {
                schema.Add(new DbfQueryColumn(_columns[ordinal], _names[ordinal], ordinal));
            }

            return schema.AsReadOnly();
        }

        public override DataTable GetSchemaTable()
        {
            return SchemaTableBuilder.Build(GetColumnSchema());
        }

        public override void Close()
        {
            _reader.Close();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                _reader.Dispose();
            }
        }
    }
}
