using System.IO;

namespace DbfReader
{
    public class DbfValueLong : DbfValue<long?>
    {
        public DbfValueLong(int length) : base(length)
        {
        }

        public override void Read(BinaryReader binaryReader)
        {
            if (binaryReader.PeekChar() == '\0')
            {
                binaryReader.ReadBytes(4);
                Value = null;
            }
            else
            {
                Value = binaryReader.ReadUInt32();
            }
        }
    }
}