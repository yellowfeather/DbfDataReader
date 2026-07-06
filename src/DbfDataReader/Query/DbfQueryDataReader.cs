using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Threading;
using System.Threading.Tasks;

namespace DbfDataReader.Query
{
    // wraps the raw table reader, exposing only the projected columns under their
    // output names, filtering on the WHERE expression, sorting on ORDER BY (which
    // buffers the matching rows in memory), and enforcing the TOP/LIMIT row limit
    internal sealed class DbfQueryDataReader : DbDataReader, IDbColumnSchemaGenerator
    {
        private readonly DbfDataReader _reader;
        private readonly IReadOnlyList<DbfColumn> _columns; // underlying columns, in projected order
        private readonly IReadOnlyList<int> _ordinals; // projected ordinal -> underlying ordinal
        private readonly IReadOnlyList<string> _names; // output names (aliases applied)
        private readonly SqlExpressionEvaluator _filter; // null when there is no WHERE clause
        private readonly Func<int, object> _readerValueAccessor;
        private readonly IReadOnlyList<OrderByItem> _orderBy;
        private readonly int _limit; // -1 when unlimited
        private int _rowsReturned;

        private List<object[]> _sortedRows; // built on first read when sorting
        private int _sortedRowIndex;
        private object[] _currentRow; // non-null when the current row comes from the sort buffer

        public DbfQueryDataReader(DbfDataReader reader, SelectStatement statement, SqlExpressionEvaluator filter)
        {
            _reader = reader;
            _filter = filter;
            _readerValueAccessor = reader.GetValue;
            _orderBy = statement.OrderBy;

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
            if (_orderBy.Count > 0)
            {
                _sortedRows ??= BuildSortedRows();
                return AdvanceSorted();
            }

            if (LimitReached()) return false;

            while (_reader.Read())
            {
                if (_filter != null && !_filter.Matches(_readerValueAccessor)) continue;

                _rowsReturned++;
                return true;
            }

            return false;
        }

        public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
        {
            if (_orderBy.Count > 0)
            {
                _sortedRows ??= await BuildSortedRowsAsync(cancellationToken).ConfigureAwait(false);
                return AdvanceSorted();
            }

            if (LimitReached()) return false;

            while (await _reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (_filter != null && !_filter.Matches(_readerValueAccessor)) continue;

                _rowsReturned++;
                return true;
            }

            return false;
        }

        private bool AdvanceSorted()
        {
            if (LimitReached() || _sortedRowIndex >= _sortedRows.Count) return false;

            _currentRow = _sortedRows[_sortedRowIndex];
            _sortedRowIndex++;
            _rowsReturned++;
            return true;
        }

        private List<object[]> BuildSortedRows()
        {
            var rows = new List<object[]>();
            while (_reader.Read())
            {
                if (_filter != null && !_filter.Matches(_readerValueAccessor)) continue;

                rows.Add(SnapshotCurrentRow());
            }

            SortRows(rows);
            return rows;
        }

        private async Task<List<object[]>> BuildSortedRowsAsync(CancellationToken cancellationToken)
        {
            var rows = new List<object[]>();
            while (await _reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (_filter != null && !_filter.Matches(_readerValueAccessor)) continue;

                rows.Add(SnapshotCurrentRow());
            }

            SortRows(rows);
            return rows;
        }

        private object[] SnapshotCurrentRow()
        {
            var row = new object[_reader.FieldCount];
            for (var ordinal = 0; ordinal < row.Length; ordinal++)
            {
                row[ordinal] = _reader.GetValue(ordinal);
            }

            return row;
        }

        private void SortRows(List<object[]> rows)
        {
            if (rows.Count < 2) return;

            var indices = new int[rows.Count];
            for (var i = 0; i < indices.Length; i++) indices[i] = i;

            // the original index is the final tiebreaker, making the sort stable
            Array.Sort(indices, (x, y) =>
            {
                var cmp = CompareRows(rows[x], rows[y]);
                return cmp != 0 ? cmp : x.CompareTo(y);
            });

            var sorted = new object[rows.Count][];
            for (var i = 0; i < indices.Length; i++) sorted[i] = rows[indices[i]];

            for (var i = 0; i < sorted.Length; i++) rows[i] = sorted[i];
        }

        private int CompareRows(object[] x, object[] y)
        {
            foreach (var item in _orderBy)
            {
                var cmp = CompareRowValues(x[item.Ordinal], y[item.Ordinal], item.Position);
                if (item.Descending) cmp = -cmp;
                if (cmp != 0) return cmp;
            }

            return 0;
        }

        // nulls sort before every value ascending (and therefore last descending)
        private static int CompareRowValues(object x, object y, int position)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            return SqlValueComparer.CompareValues(x, y, position);
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

            // DbDataReader.GetOrdinal is documented to throw IndexOutOfRangeException for
            // unknown column names; DbfDataReader.GetOrdinal behaves the same way
#pragma warning disable S112
            throw new IndexOutOfRangeException();
#pragma warning restore S112
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
            return GetUnderlyingValue(_ordinals[ordinal]);
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
            return GetValue(ordinal) == null;
        }

        public override bool GetBoolean(int ordinal)
        {
            return GetFieldValue<bool>(ordinal);
        }

        public override byte GetByte(int ordinal)
        {
            return GetFieldValue<byte>(ordinal);
        }

        public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
        {
            throw new NotImplementedException();
        }

        public override char GetChar(int ordinal)
        {
            return GetFieldValue<char>(ordinal);
        }

        public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length)
        {
            throw new NotImplementedException();
        }

        public override DateTime GetDateTime(int ordinal)
        {
            return GetFieldValue<DateTime>(ordinal);
        }

        public override decimal GetDecimal(int ordinal)
        {
            return GetFieldValue<decimal>(ordinal);
        }

        public override double GetDouble(int ordinal)
        {
            return GetFieldValue<double>(ordinal);
        }

        public override float GetFloat(int ordinal)
        {
            return GetFieldValue<float>(ordinal);
        }

        public override Guid GetGuid(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override short GetInt16(int ordinal)
        {
            return GetFieldValue<short>(ordinal);
        }

        public override int GetInt32(int ordinal)
        {
            return GetFieldValue<int>(ordinal);
        }

        public override long GetInt64(int ordinal)
        {
            return GetFieldValue<long>(ordinal);
        }

        public override string GetString(int ordinal)
        {
            var value = GetValue(ordinal);
            if (value == null || value is string) return (string)value;

            throw new InvalidCastException(
                $"Unable to cast object of type '{value.GetType().FullName}' to type 'System.String' " +
                $"at ordinal '{ordinal}'.");
        }

        public override T GetFieldValue<T>(int ordinal)
        {
            var value = GetValue(ordinal);
            if (value is null)
                throw new SqlNullValueException(
                    $"Data is Null. This method or property cannot be called on Null values. Ordinal {ordinal}");

            if (value is T typed) return typed;

            throw new InvalidCastException(
                $"Unable to cast object of type '{value.GetType().FullName}' to type '{typeof(T).FullName}' " +
                $"at ordinal '{ordinal}'.");
        }

        private object GetUnderlyingValue(int underlyingOrdinal)
        {
            return _currentRow != null ? _currentRow[underlyingOrdinal] : _reader.GetValue(underlyingOrdinal);
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
