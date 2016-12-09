using System.IO;
using System.Text;

namespace DbfReader
{
    public class DbfMemoDbase3 : DbfMemo
    {
        public DbfMemoDbase3(string path) : base(path)
        {
        }

        public DbfMemoDbase3(string path, Encoding encoding) : base(path, encoding)
        {
        }

        public DbfMemoDbase3(Stream stream, Encoding encoding) : base(stream, encoding)
        {
        }

        public override string BuildMemo(long startBlock)
        {
            var offset = Offset(startBlock);
            _binaryReader.BaseStream.Seek(offset, SeekOrigin.Begin);

            var finished = false;
            var stringBuilder = new StringBuilder();

            do
            {
                var block = new string(_binaryReader.ReadChars(DefaultBlockSize));
                block = block.TrimEnd('\0', ' ');

                stringBuilder.Append(block);

                if (block.Length >= DefaultBlockSize)
                {
                    finished = true;
                }
            } while (!finished);

            return stringBuilder.ToString();
        }
    }
}