using System.IO;
using System.Text;

namespace DbfDataReader
{
    public class DbfMemoFoxPro : DbfMemo
    {
        public DbfMemoFoxPro(string path) : this(path, Encoding.UTF8)
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
            _binaryReader.BaseStream.Seek(0, SeekOrigin.Begin);

            _binaryReader.ReadUInt32(); // next block
            _binaryReader.ReadUInt16(); // unused
            return _binaryReader.ReadUInt16();
        }

        public override string BuildMemo(long startBlock)
        {
            var offset = Offset(startBlock);
            _binaryReader.BaseStream.Seek(offset, SeekOrigin.Begin);

            var blockType = _binaryReader.ReadUInt32();
            var memoLength = _binaryReader.ReadUInt32();

            if ((blockType != 1) || (memoLength == 0))
            {
                return string.Empty;
            }

            var memo = new string(_binaryReader.ReadChars(DefaultBlockSize));
            memo = memo.TrimEnd('\0', ' ');
            return memo;
        }
    }
}