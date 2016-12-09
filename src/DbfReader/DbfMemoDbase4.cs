using System.IO;
using System.Text;

namespace DbfReader
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
            throw new System.NotImplementedException();
        }
    }
}