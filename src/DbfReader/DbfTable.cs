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
        }

        public string Path { get; private set; }

        public Encoding CurrentEncoding { get; set; }

        public DbfHeader Header { get; }

        public IList<DbfColumn> Columns { get; }

        public bool IsClosed => _binaryReader != null;

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