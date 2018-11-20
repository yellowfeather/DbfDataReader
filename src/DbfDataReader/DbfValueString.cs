using System.IO;
using System.Text;

namespace DbfDataReader
{
    public class DbfValueString : DbfValue<string>
    {
        protected readonly Encoding CurrentEncoding;

        public DbfValueString(int length, Encoding encoding) : base(length)
        {
            CurrentEncoding = encoding;
        }

        public override void Read(BinaryReader binaryReader)
        {
            Value = binaryReader.ReadString(Length, CurrentEncoding);
        }
    }
}