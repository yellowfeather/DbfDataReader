using System.IO;

namespace DbfDataReader
{
    public class DbfValueLong : DbfValue<long?>
    {
        public DbfValueLong(int length) : base(length)
        {
        }

        public override void Read(BinaryReader binaryReader)
        {
            Value = binaryReader.ReadInt32();
        }
    }
}