using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DbfDataReader
{
    public class DbfRecord
    {
        private const byte EndOfFile = 0x1a;

        private readonly Encoding _encoding;
        private readonly StringTrimmingOption _stringTrimming;
        private readonly bool _readFloatsAsDecimals;
        private readonly int _recordLength;
        private readonly long _dataOffset;
        private readonly byte[] _buffer;

        // column-subset parsing (issue #296): when enabled, an ordinal's value is only
        // readable after it has been parsed for the current row; the stamps guard
        // against exposing the previous row's content through the reused value objects
        private int[] _parsedVersions;
        private int _rowVersion;

        public DbfRecord(DbfTable dbfTable)
        {
            _encoding = dbfTable.CurrentEncoding;
            _stringTrimming = dbfTable.StringTrimming;
            _readFloatsAsDecimals = dbfTable.ReadFloatsAsDecimals;
            _recordLength = dbfTable.Header.RecordLength;
            _dataOffset = dbfTable.DataOffset;
            _buffer = new byte[_recordLength];

            Values = new List<IDbfValue>();

            foreach (var dbfColumn in dbfTable.Columns)
            {
                var dbfValue = CreateDbfValue(dbfColumn, dbfTable.Memo);
                Values.Add(dbfValue);
            }
        }

        public bool IsDeleted { get; private set; }

        public int RecordIndex { get; private set; } = -1;

        public IList<IDbfValue> Values { get; set; }

        private IDbfValue CreateDbfValue(DbfColumn dbfColumn, DbfMemo memo)
        {
            IDbfValue value;

            switch (dbfColumn.ColumnType)
            {
                case DbfColumnType.Number:
                    if (dbfColumn.DecimalCount == 0) {
                        if (dbfColumn.Length < 10) {
                            value = new DbfValueInt(dbfColumn.Start, dbfColumn.Length);
                        }
                        else {
                            value = new DbfValueInt64(dbfColumn.Start, dbfColumn.Length);
                        }
                    }
                    else
                        value = new DbfValueDecimal(dbfColumn.Start, dbfColumn.Length, dbfColumn.DecimalCount);
                    break;
                case DbfColumnType.SignedLong:
                    value = new DbfValueLong(dbfColumn.Start, dbfColumn.Length);
                    break;
                case DbfColumnType.Float:
                    if (_readFloatsAsDecimals)
                    {
                        value = new DbfValueDecimal(dbfColumn.Start, dbfColumn.Length, dbfColumn.DecimalCount);
                    }
                    else {
                        value = new DbfValueFloat(dbfColumn.Start, dbfColumn.Length, dbfColumn.DecimalCount);
                    }                    
                    break;
                case DbfColumnType.Currency:
                    value = new DbfValueCurrency(dbfColumn.Start, dbfColumn.Length, dbfColumn.DecimalCount);
                    break;
                case DbfColumnType.Date:
                    value = new DbfValueDate(dbfColumn.Start, dbfColumn.Length);
                    break;
                case DbfColumnType.DateTime:
                    value = new DbfValueDateTime(dbfColumn.Start, dbfColumn.Length);
                    break;
                case DbfColumnType.Boolean:
                    value = new DbfValueBoolean(dbfColumn.Start, dbfColumn.Length);
                    break;
                case DbfColumnType.Memo:
                    value = new DbfValueMemo(dbfColumn.Start, dbfColumn.Length, memo, _encoding);
                    break;
                case DbfColumnType.Double:
                    value = new DbfValueDouble(dbfColumn.Start, dbfColumn.Length, dbfColumn.DecimalCount);
                    break;
                case DbfColumnType.General:
                case DbfColumnType.Character:
                    value = new DbfValueString(dbfColumn.Start, dbfColumn.Length, _encoding, _stringTrimming);
                    break;
                case DbfColumnType.WideCharacter:
                    value = new DbfValueWideString(dbfColumn.Start, dbfColumn.Length, _stringTrimming);
                    break;
                default:
                    value = new DbfValueNull(dbfColumn.Start, dbfColumn.Length);
                    break;
            }

            return value;
        }

        public bool Read(Stream stream)
        {
            if (!ReadRaw(stream)) return false;

            try
            {
                ParseValues();
                return true;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }

        public async ValueTask<bool> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            if (!await ReadRawAsync(stream, cancellationToken).ConfigureAwait(false)) return false;

            try
            {
                ParseValues();
                return true;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }

        // reads the raw record into the buffer and sets IsDeleted and RecordIndex
        // without parsing any column values; Values keeps the previous row's contents
        internal bool ReadRaw(Stream stream)
        {
            var position = stream.Position;
            if (position == stream.Length) return false;

            try
            {
                var read = stream.Read(_buffer, 0, _recordLength);
                if (read <= 0)
                    return false;
                while (read < _recordLength)
                {
                    var r = stream.Read(_buffer, read, _recordLength - read);
                    if (r == 0)
                        return false;
                    read += r;
                }
            }
            catch (EndOfStreamException)
            {
                return false;
            }

            return ReadStatus(position);
        }

        internal async ValueTask<bool> ReadRawAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            var position = stream.Position;
            if (position == stream.Length) return false;

            try
            {
                var read = await stream.ReadAsync(_buffer.AsMemory(0, _recordLength), cancellationToken)
                    .ConfigureAwait(false);
                if (read <= 0)
                    return false;
                while (read < _recordLength)
                {
                    var r = await stream.ReadAsync(_buffer.AsMemory(read, _recordLength - read), cancellationToken)
                        .ConfigureAwait(false);
                    if (r == 0)
                        return false;
                    read += r;
                }
            }
            catch (EndOfStreamException)
            {
                return false;
            }

            return ReadStatus(position);
        }

        private bool ReadStatus(long position)
        {
            _rowVersion++;

            var status = _buffer[0];
            if (status == EndOfFile) return false;

            IsDeleted = status == 0x2A;
            RecordIndex = (int) ((position - _dataOffset) / _recordLength);

            return true;
        }

        private void ParseValues()
        {
            var span = new ReadOnlySpan<byte>(_buffer);

            foreach (var dbfValue in Values)
            {
                var slice = span.Slice(dbfValue.Start, dbfValue.Length);
                dbfValue.Read(slice);
            }

            MarkAllParsed();
        }

        // restricts value access to ordinals parsed for the current row; used by the
        // query engine, which parses only the columns a query references
        internal void EnableSubsetParsing()
        {
            _parsedVersions ??= new int[Values.Count];
        }

        // parses only the given ordinals for the current row; may be called again with
        // further ordinals once the row is known to be needed. Returns false when the
        // record data extends past the end of a stream (matching Read's contract).
        internal bool TryParseValues(int[] ordinals)
        {
            if (_parsedVersions == null)
                throw new InvalidOperationException(
                    "EnableSubsetParsing must be called before parsing column subsets.");

            try
            {
                var span = new ReadOnlySpan<byte>(_buffer);
                foreach (var ordinal in ordinals)
                {
                    var dbfValue = Values[ordinal];
                    dbfValue.Read(span.Slice(dbfValue.Start, dbfValue.Length));
                    _parsedVersions[ordinal] = _rowVersion;
                }

                return true;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }

        private void MarkAllParsed()
        {
            if (_parsedVersions == null) return;

            for (var ordinal = 0; ordinal < _parsedVersions.Length; ordinal++)
            {
                _parsedVersions[ordinal] = _rowVersion;
            }
        }

        private void EnsureParsed(int ordinal)
        {
            if (_parsedVersions != null && _parsedVersions[ordinal] != _rowVersion)
            {
                throw new InvalidOperationException(
                    $"The value at ordinal {ordinal} was not parsed for the current row; the query's column subset does not include it.");
            }
        }

        public object GetValue(int ordinal)
        {
            EnsureParsed(ordinal);
            var dbfValue = Values[ordinal];
            return dbfValue.GetValue();
        }

        public bool IsNull(int ordinal)
        {
            EnsureParsed(ordinal);
            return Values[ordinal].IsNull;
        }

        public T GetValue<T>(int ordinal)
        {
            EnsureParsed(ordinal);
            var dbfValue = Values[ordinal];
            if (dbfValue is DbfValue<T> typedValue)
            {
                var value = typedValue.Value;
                if (value is null) throw DataIsNull(ordinal);
                return value;
            }

            return GetBoxedValue<T>(dbfValue, ordinal);
        }

        // typed getters store T? but return T; reading through DbfValue<T?>
        // avoids boxing the value on the way out
        internal T GetStructValue<T>(int ordinal) where T : struct
        {
            EnsureParsed(ordinal);
            if (Values[ordinal] is DbfValue<T?> typedValue)
            {
                var value = typedValue.Value;
                if (value is null) throw DataIsNull(ordinal);
                return value.GetValueOrDefault();
            }

            return GetValue<T>(ordinal);
        }

        private static T GetBoxedValue<T>(IDbfValue dbfValue, int ordinal)
        {
            try
            {
                var value = dbfValue.GetValue();
                if (value is null) throw DataIsNull(ordinal);
                return (T) value;
            }
            catch (InvalidCastException)
            {
                throw new InvalidCastException(
                    $"Unable to cast object of type '{dbfValue.GetValue().GetType().FullName}' to type '{typeof(T).FullName}' at ordinal '{ordinal}'.");
            }
        }

        private static SqlNullValueException DataIsNull(int ordinal)
        {
            return new SqlNullValueException(
                $"Data is Null. This method or property cannot be called on Null values. Ordinal {ordinal}");
        }

        public string GetStringValue(int ordinal)
        {
            EnsureParsed(ordinal);
            var dbfValue = Values[ordinal];
            try
            {
                return (string) dbfValue.GetValue();
            }
            catch (InvalidCastException)
            {
                throw new InvalidCastException(
                    $"Unable to cast object of type '{dbfValue.GetValue().GetType().FullName}' to type '{typeof(string).FullName}' at ordinal '{ordinal}'.");
            }
        }

        public Type GetFieldType(int ordinal)
        {
            var dbfValue = Values[ordinal];
            return dbfValue.GetFieldType();
        }
    }
}