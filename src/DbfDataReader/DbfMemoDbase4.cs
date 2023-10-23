using System.IO;
using System.Text;

namespace DbfDataReader
{
    public class DbfMemoDbase4 : DbfMemo
    {
        public DbfMemoDbase4(string path) : base(path)
        {
        }

        public DbfMemoDbase4(string path, Encoding encoding) : base(path, encoding)
        {
        }

        public DbfMemoDbase4(Stream stream, Encoding encoding) : base(stream, encoding)
        {
        }

        public override string BuildMemo(long startBlock)
        {
            var offset = Offset(startBlock);
            BinaryReader.BaseStream.Seek(offset, SeekOrigin.Begin);
            // Read the bytes 4-7 as length of the memo field
            BinaryReader.BaseStream.Seek(offset + 4, SeekOrigin.Begin);
            var memoLength = BinaryReader.ReadInt32() - 8;
            // Set at beginning of memo
            BinaryReader.BaseStream.Seek(offset + 8, SeekOrigin.Begin);
            var stringBuilder = new StringBuilder();
            var block = BinaryReader.ReadString(memoLength, CurrentEncoding);
            stringBuilder.Append(block);
            return stringBuilder.ToString();
        }
    }
}