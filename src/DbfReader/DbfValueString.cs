using System.IO;

namespace DbfReader
{
    public class DbfValueString : IDbfValue
    {
        public DbfValueString(int length)
        {
            Length = length;
        }

        public int Length { get; }

        public void Read(BinaryReader binaryReader)
        {
            Value = new string(binaryReader.ReadChars(Length));
        }

        public string Value { get; private set; }
    }
}