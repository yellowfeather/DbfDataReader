using System.IO;

namespace DbfReader
{
    public class DbfValueCurrency : DbfValue<float?>
    {
        public override void Read(BinaryReader binaryReader)
        {
            if (binaryReader.PeekChar() == '\0')
            {
                binaryReader.ReadBytes(8);
                Value = null;
            }
            else
            {
                var value = binaryReader.ReadUInt64();
                Value = value/10000.0f;
            }
        }
    }
}