using System.IO;

namespace DbfReader
{
    public class DbfValueBoolean : IDbfValue
    {
        public void Read(BinaryReader binaryReader)
        {
            Value = binaryReader.ReadBoolean();
        }

        public bool? Value { get; private set; }
    }
}