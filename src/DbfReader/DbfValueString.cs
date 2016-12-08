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
            var value = new string(binaryReader.ReadChars(Length));
            Value = value.TrimEnd((char)0);
        }

        public string Value { get; private set; }
    }
}