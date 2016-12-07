using System.IO;

namespace DbfReader
{
    public class DbfValueDecimal : IDbfValue
    {
        public void Read(BinaryReader binaryReader)
        {
            Value = binaryReader.ReadInt16();
        }

        public decimal? Value { get; private set; }
    }
}