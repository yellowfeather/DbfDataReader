using System.IO;
using System.Text;

namespace DbfDataReader
{
    public class DbfColumn
    {
        private readonly Encoding _encoding;

        public DbfColumn(BinaryReader binaryReader, int index, Encoding encoding)
        {
            Index = index;
            _encoding = encoding;
            Read(binaryReader);
        }

        public int Index { get; }
        public string Name { get; private set; }
        public DbfColumnType ColumnType { get; private set; }
        public int Length { get; private set; }
        public int DecimalCount { get; private set; }

        private void Read(BinaryReader binaryReader)
        {
            var rawName = binaryReader.ReadString(11, _encoding);
            Name = rawName.TrimEnd((char) 0);

            var type = binaryReader.ReadByte();
            ColumnType = (DbfColumnType) type;

            // ignore field data address
            binaryReader.ReadUInt32();

            Length = binaryReader.ReadByte();
            DecimalCount = binaryReader.ReadByte();

            // skip the reserved bytes
            binaryReader.ReadBytes(14);
        }
    }
}