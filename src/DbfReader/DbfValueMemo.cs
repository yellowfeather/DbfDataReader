using System.IO;

namespace DbfReader
{
    public class DbfValueMemo : IDbfValue
    {
        public DbfValueMemo(int length)
        {
            Length = length;
        }

        public int Length { get; }

        public void Read(BinaryReader binaryReader)
        {
            // TODO: read from memo file
            Value = new string(binaryReader.ReadChars(Length));
        }

        public string Value { get; private set; }
    }
}