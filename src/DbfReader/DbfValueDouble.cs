using System.IO;

namespace DbfReader
{
    public class DbfValueDouble : IDbfValue
    {
        public void Read(BinaryReader binaryReader)
        {
            Value = binaryReader.ReadInt16();
        }

        public double? Value { get; private set; }
    }
}