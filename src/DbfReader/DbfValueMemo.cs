using System.IO;

namespace DbfReader
{
    public class DbfValueMemo : DbfValueString
    {
        public DbfValueMemo(int length)
            : base(length)
        {
        }

        public override void Read(BinaryReader binaryReader)
        {
            // TODO: read from memo file
            var value = new string(binaryReader.ReadChars(Length));
            Value = value.TrimEnd((char)0);
        }
    }
}