using System.IO;

namespace DbfReader
{
    public class DbfColumn
    {
        public DbfColumn(BinaryReader binaryReader, int index)
        {
            Index = index;
            Read(binaryReader);
        }

        public int Index { get; private set; }
        public string Name { get; private set; }
        public DbfColumnType ColumnType { get; private set; }
        public int Length { get; private set; }
        public int DecimalCount { get; private set; }

        private void Read(BinaryReader binaryReader)
        {
            var rawName = new string(binaryReader.ReadChars(11));
            Name = rawName.Trim();

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