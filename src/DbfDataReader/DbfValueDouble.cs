using System.IO;

namespace DbfDataReader
{
    public class DbfValueDouble : DbfValue<double?>
    {
        public DbfValueDouble(int length) : base(length)
        {
        }

        public override void Read(BinaryReader binaryReader)
        {
            if (binaryReader.PeekChar() == '\0')
            {
                binaryReader.ReadBytes(2);
                Value = null;
            }
            else
            {
                Value = binaryReader.ReadInt16();
            }
        }
    }
}