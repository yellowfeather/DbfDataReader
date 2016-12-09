using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DbfReader
{
    public class DbfTable : IDisposable
    {
        private const byte Terminator = 0x0d;
        private const int HeaderMetaDataSize = 33;
        private const int ColumnMetaDataSize = 32;

        private readonly BinaryReader _binaryReader;

        public DbfTable(string path)
            : this(path, Encoding.UTF8)
        {
            
        }

        public DbfTable(string path, Encoding encoding)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException();
            }

            Path = path;
            CurrentEncoding = encoding;

            var stream = new FileStream(path, FileMode.Open);
            _binaryReader = new BinaryReader(stream, encoding);

            Header = new DbfHeader(_binaryReader);
            Columns = ReadColumns(_binaryReader);
            SkipToFirstRecord(_binaryReader);

            var memoPath = MemoPath();
            if (!string.IsNullOrEmpty(memoPath))
            {
                Memo = CreateMemo(memoPath);
            }
        }

        public DbfTable(Stream stream, Encoding encoding)
        {
            Path = string.Empty;
            CurrentEncoding = encoding;

            _binaryReader = new BinaryReader(stream, encoding);

            Header = new DbfHeader(_binaryReader);
            Columns = ReadColumns(_binaryReader);
            SkipToFirstRecord(_binaryReader);
        }

        public void Dispose()
        {
            _binaryReader?.Dispose();
            Memo?.Dispose();
        }

        public string Path { get; }

        public Encoding CurrentEncoding { get; set; }

        public DbfHeader Header { get; }

        public DbfMemo Memo { get; }

        public IList<DbfColumn> Columns { get; }

        public bool IsClosed => _binaryReader != null;

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
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return string.Empty;
        }

        public DbfMemo CreateMemo(string path)
        {
            DbfMemo memo;

            if (Header.IsFoxPro)
            {
                memo = new DbfMemoFoxPro(path);
            }
            else
            {
                if (Header.Version == 0x83)
                {
                    memo = new DbfMemoDbase3(path);
                }
                else
                {
                    memo = new DbfMemoDbase4(path);
                }
            }

            return memo;
        }

        public IList<DbfColumn> ReadColumns(BinaryReader binaryReader)
        {
            var columns = new List<DbfColumn>();

            var index = 0;
            while (binaryReader.PeekChar() != Terminator)
            {
                var column = new DbfColumn(binaryReader, index++);
                columns.Add(column);
            }

            var terminator = binaryReader.ReadByte();
            if (terminator != Terminator)
            {
                throw new DbfFileFormatException();
            }

            return columns;
        }

        public void SkipToFirstRecord(BinaryReader binaryReader)
        {
            var numBytesToSkip = Header.HeaderLength - (HeaderMetaDataSize + (ColumnMetaDataSize * Columns.Count));
            _binaryReader.ReadBytes(numBytesToSkip);
        }

        public DbfRecord ReadRecord()
        {
            var dbfRecord = new DbfRecord(this);
            dbfRecord.Read(_binaryReader);
            return dbfRecord;
        }

        public bool Read(DbfRecord dbfRecord)
        {
            return dbfRecord.Read(_binaryReader);
        }
    }
}