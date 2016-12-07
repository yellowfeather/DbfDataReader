using System.IO;

namespace DbfReader
{
    public class DbfValueFloat : IDbfValue
    {
        public void Read(BinaryReader binaryReader)
        {
            Value = binaryReader.ReadInt16();
        }

        public float? Value { get; private set; }
    }
}