using System.IO;
using System.Text;

namespace DbfDataReader
{
    public abstract class DbfMemo : Disposable
    {
        protected const int BlockHeaderSize = 8;
        protected const int DefaultBlockSize = 512;

        protected BinaryReader BinaryReader;

        protected DbfMemo(string path)
            : this(path, EncodingProvider.GetEncoding(1252))
        {
        }

        protected DbfMemo(string path, Encoding encoding)
        {
            if (!File.Exists(path)) throw new FileNotFoundException();

            Path = path;
            CurrentEncoding = encoding;

            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            BinaryReader = new BinaryReader(stream, encoding, false);
        }

        protected DbfMemo(Stream stream, Encoding encoding)
        {
            Path = string.Empty;
            CurrentEncoding = encoding;

            BinaryReader = new BinaryReader(stream, encoding, true);
        }

        public Encoding CurrentEncoding { get; set; }

        public virtual int BlockSize => DefaultBlockSize;

        public string Path { get; set; }

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
            }
            finally
            {
                BinaryReader = null;
            }
        }

        public abstract string BuildMemo(long startBlock);

        public string Get(long startBlock)
        {
            return startBlock <= 0 ? string.Empty : BuildMemo(startBlock);
        }

        public long Offset(long startBlock)
        {
            return startBlock * BlockSize;
        }

        public int ContentSize(int memoSize)
        {
            return memoSize - BlockSize + BlockHeaderSize;
        }

        public int BlockContentSize()
        {
            return BlockSize + BlockHeaderSize;
        }
    }
}