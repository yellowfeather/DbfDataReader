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
            var nullIdx = rawName.IndexOf((char)0);
            if (nullIdx >= 0)
            {
                rawName = rawName.Substring(0, nullIdx);   // trim off everything past & including the first NUL byte
            }
			Name = rawName;

            var type = binaryReader.ReadByte();
            ColumnType = (DbfColumnType) type;

            // ignore field data address
            binaryReader.ReadUInt32();

            Length = binaryReader.ReadByte();
            DecimalCount = binaryReader.ReadByte();

            // skip the reserved bytes:
            // - Int16: reserved1
            // - Byte: workarea_id
            // - Byte: reserved2
            // - Byte: reserved3
            // - Byte: set_fields_flag
            // - String7: reserved4
            // - Byte: index_field_flag
            binaryReader.ReadBytes(14);
        }
    }
}