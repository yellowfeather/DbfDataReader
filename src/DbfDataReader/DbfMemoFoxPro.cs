using System.IO;
using System.Text;

namespace DbfDataReader
{
    public class DbfMemoFoxPro : DbfMemo
    {
        public DbfMemoFoxPro(string path) : this(path, EncodingProvider.GetEncoding(1252))
        {
        }

        public DbfMemoFoxPro(string path, Encoding encoding) : base(path, encoding)
        {
            BlockSize = CalculateBlockSize();
        }

        public DbfMemoFoxPro(Stream stream, Encoding encoding) : base(stream, encoding)
        {
            BlockSize = CalculateBlockSize();
        }

        public override int BlockSize { get; }

        private int CalculateBlockSize()
        {
            BinaryReader.BaseStream.Seek(0, SeekOrigin.Begin);

            BinaryReader.ReadUInt32(); // next block
            BinaryReader.ReadUInt16(); // unused
            return BinaryReader.ReadBigEndianInt16();
        }

        public override string BuildMemo(long startBlock)
        {
            var offset = Offset(startBlock);
            BinaryReader.BaseStream.Seek(offset, SeekOrigin.Begin);

            var blockType = BinaryReader.ReadBigEndianInt32();
            var memoLength = BinaryReader.ReadBigEndianInt32();

            if (blockType != 1 || memoLength == 0) return string.Empty;

            var value = BinaryReader.ReadString(memoLength, CurrentEncoding);
            var nullIdx = value.IndexOf((char)0);
            if (nullIdx >= 0)
            {
                value = value.Substring(0, nullIdx);   // trim off everything past & including the first NUL byte
            }
            value = value.TrimEnd(' ');
            return value;
        }
    }
}