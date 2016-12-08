using System.IO;

namespace DbfReader
{
    public class DbfValueString : DbfValue<string>
    {
        public DbfValueString(int length)
        {
            Length = length;
        }

        public int Length { get; }

        public override void Read(BinaryReader binaryReader)
        {
            var value = new string(binaryReader.ReadChars(Length));
            Value = value.TrimEnd('\0', ' ');
        }
    }
}