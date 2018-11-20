using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DbfDataReader
{
    public class DbfTable : Disposable
    {
        private const byte Terminator = 0x0d;
        private const int HeaderMetaDataSize = 33;
        private const int ColumnMetaDataSize = 32;

        public DbfTable(string path, Encoding encoding)
        {
            if (!File.Exists(path)) throw new FileNotFoundException();

            Path = path;
            CurrentEncoding = encoding;

            var stream = new FileStream(path, FileMode.Open);
            BinaryReader = new BinaryReader(stream, encoding, false);

            Header = new DbfHeader(BinaryReader);
            Columns = ReadColumns(BinaryReader);
            SkipToFirstRecord(BinaryReader);

            var memoPath = MemoPath();
            if (!string.IsNullOrEmpty(memoPath)) Memo = CreateMemo(memoPath);
        }

        public DbfTable(Stream stream, Encoding encoding)
            : this(stream, null, encoding)
        {
        }

        public DbfTable(Stream stream, Stream memoStream, Encoding encoding)
        {
            Path = string.Empty;
            CurrentEncoding = encoding;

            BinaryReader = new BinaryReader(stream, encoding, true);

            Header = new DbfHeader(BinaryReader);
            Columns = ReadColumns(BinaryReader);
            SkipToFirstRecord(BinaryReader);

            if (memoStream != null)
                Memo = CreateMemo(memoStream);
        }

        public string Path { get; }

        public Encoding CurrentEncoding { get; set; }

        public DbfHeader Header { get; }

        public BinaryReader BinaryReader { get; private set; }

        public DbfMemo Memo { get; private set; }

        public IList<DbfColumn> Columns { get; }

        public bool IsClosed => BinaryReader == null;

        public void Close()
        {
            Dispose(true);
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!disposing) return;
                BinaryReader?.Dispose();
                Memo?.Dispose();
            }
            finally
            {
                BinaryReader = null;
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

        public IList<DbfColumn> ReadColumns(BinaryReader binaryReader)
        {
            var columns = new List<DbfColumn>();

            var index = 0;
            while (binaryReader.PeekChar() != Terminator)
            {
                var column = new DbfColumn(binaryReader, index++, CurrentEncoding);
                columns.Add(column);
            }

            var terminator = binaryReader.ReadByte();
            if (terminator != Terminator) throw new DbfFileFormatException();

            return columns;
        }

        public void SkipToFirstRecord(BinaryReader binaryReader)
        {
            var numBytesToSkip = Header.HeaderLength - (HeaderMetaDataSize + ColumnMetaDataSize * Columns.Count);
            BinaryReader.ReadBytes(numBytesToSkip);
        }

        public DbfRecord ReadRecord()
        {
            var dbfRecord = new DbfRecord(this);
            return !dbfRecord.Read(BinaryReader) ? null : dbfRecord;
        }

        public bool Read(DbfRecord dbfRecord)
        {
            return dbfRecord.Read(BinaryReader);
        }
    }
}