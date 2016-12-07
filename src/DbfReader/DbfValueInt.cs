using System.IO;

namespace DbfReader
{
    public class DbfValueInt : IDbfValue
    {
        public void Read(BinaryReader binaryReader)
        {
            Value = binaryReader.ReadInt16();
        }

        public int? Value { get; private set; }
    }
}