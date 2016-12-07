using System.IO;

namespace DbfReader
{
    public class DbfValueLong : IDbfValue
    {
        public void Read(BinaryReader binaryReader)
        {
            if (binaryReader.PeekChar() == '\0') return;

            Value = binaryReader.ReadUInt32();
        }

        public long? Value { get; private set; }
    }
}