using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DbfDataReader
{
    public class DbfTable : Disposable
    {
        private const byte Terminator = 0x0d;

        public DbfTable(string path, Encoding encoding = null, StringTrimmingOption stringTrimming = StringTrimmingOption.Trim, bool readFloatsAsDecimals = false)
        {
            if (!File.Exists(path)) throw new FileNotFoundException();

            Path = path;
            CurrentEncoding = encoding;
            StringTrimming = stringTrimming;
            ReadFloatsAsDecimals = readFloatsAsDecimals;

            Stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            Init();

            var memoPath = MemoPath();
            if (!string.IsNullOrEmpty(memoPath)) Memo = CreateMemo(memoPath);
        }

        public DbfTable(Stream stream, Encoding encoding = null, StringTrimmingOption stringTrimming = StringTrimmingOption.Trim, bool readFloatsAsDecimals = false)
            : this(stream, null, encoding, stringTrimming, readFloatsAsDecimals)
        {
        }

        public DbfTable(Stream stream, Stream memoStream, Encoding encoding = null, StringTrimmingOption stringTrimming = StringTrimmingOption.Trim, bool readFloatsAsDecimals = false)
        {
            Path = string.Empty;
            CurrentEncoding = encoding;
            StringTrimming = stringTrimming;
            ReadFloatsAsDecimals = readFloatsAsDecimals;
            Stream = stream;

            Init();

            if (memoStream != null)
                Memo = CreateMemo(memoStream);
        }

        private void Init()
        {
            Header = new DbfHeader(Stream);
            CurrentEncoding ??= EncodingProvider.GetEncoding(Header.LanguageDriver);
            Columns = ReadColumns(Stream);
        }

        public string Path { get; }

        public Encoding CurrentEncoding { get; private set; }

        public DbfHeader Header { get; private set; }

        public Stream Stream { get; private set; }

        public DbfMemo Memo { get; private set; }

        public IList<DbfColumn> Columns { get; private set; }

        public StringTrimmingOption StringTrimming { get; private set; }

        public bool ReadFloatsAsDecimals { get; set; }

        public bool IsClosed => Stream == null;

        public void Close()
        {
            Dispose(true);
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!disposing) return;
                Stream?.Dispose();
                Memo?.Dispose();
            }
            finally
            {
                Stream = null;
                Memo = null;
            }
        }

        public string MemoPath()
        {
            var paths = new[]
            {
                System.IO.Path.ChangeExtension(Path, "fpt"),
                System.IO.Path.ChangeExtension(Path, "FPT"),
                System.IO.Path.ChangeExtension(Path, "dbt"),
                System.IO.Path.ChangeExtension(Path, "DBT")
            };

            foreach (var path in paths)
                if (File.Exists(path))
                    return path;

            return string.Empty;
        }

        public DbfMemo CreateMemo(Stream memoStream)
        {
            DbfMemo memo;

            if (Header.IsFoxPro)
            {
                memo = new DbfMemoFoxPro(memoStream, CurrentEncoding);
            }
            else
            {
                if (Header.Version == 0x83)
                    memo = new DbfMemoDbase3(memoStream, CurrentEncoding);
                else
                    memo = new DbfMemoDbase4(memoStream, CurrentEncoding);
            }

            return memo;
        }

        public DbfMemo CreateMemo(string path)
        {
            DbfMemo memo;

            if (Header.IsFoxPro)
            {
                memo = new DbfMemoFoxPro(path, CurrentEncoding);
            }
            else
            {
                if (Header.Version == 0x83)
                    memo = new DbfMemoDbase3(path, CurrentEncoding);
                else
                    memo = new DbfMemoDbase4(path, CurrentEncoding);
            }

            return memo;
        }

        public IList<DbfColumn> ReadColumns(Stream stream)
        {
            var count = Header.HeaderLength - DbfHeader.DbfHeaderSize;
            var buffer = new byte[count];
            stream.Read(buffer, 0, count);
            var span = new ReadOnlySpan<byte>(buffer);

            var columns = new List<DbfColumn>();

            var start = 0;
            var startField = 1;
            var ordinal = 0;
            while (span[start] != Terminator)
            {
                var slice = span.Slice(start, DbfColumn.DbfColumnSize);
                var column = new DbfColumn(slice, startField, ordinal, CurrentEncoding);
                columns.Add(column);

                ordinal++;
                start = ordinal * DbfColumn.DbfColumnSize;
                startField += column.Length;
            }

            return columns;
        }

        public DbfRecord ReadRecord()
        {
            var dbfRecord = new DbfRecord(this);
            return !dbfRecord.Read(Stream) ? null : dbfRecord;
        }

        public bool Read(DbfRecord dbfRecord)
        {
            return dbfRecord.Read(Stream);
        }
    }
}