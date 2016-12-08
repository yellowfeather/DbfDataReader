using System.IO;

namespace DbfReader
{
    public class DbfValueDouble : DbfValue<double?>
    {
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