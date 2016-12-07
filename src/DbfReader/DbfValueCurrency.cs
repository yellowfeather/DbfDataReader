using System.IO;

namespace DbfReader
{
    public class DbfValueCurrency : IDbfValue
    {
        public void Read(BinaryReader binaryReader)
        {
            if (binaryReader.PeekChar() == '\0') return;

            var value = binaryReader.ReadUInt64();
            Value = value/10000.0f;
        }

        public float? Value { get; private set; }
    }
}