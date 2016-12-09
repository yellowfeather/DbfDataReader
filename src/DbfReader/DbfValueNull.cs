using System.IO;

namespace DbfReader
{
    public class DbfValueNull : IDbfValue
    {
        public DbfValueNull(int length)
        {
            Length = length;
        }

        public int Length { get; }

        public void Read(BinaryReader binaryReader)
        {
            // binaryReader.ReadBytes(Length);
            binaryReader.ReadByte();
        }

        public override string ToString()
        {
            return string.Empty;
        }
    }
}