using System.IO;

namespace DbfDataReader
{
    public class DbfValueLong : DbfValue<long?>
    {
        private readonly DbfDataReaderOptions options;

        public DbfValueLong(int length) : this(length, new DbfDataReaderOptions())
        {
        }

        public DbfValueLong(int length, DbfDataReaderOptions options) : base(length)
        {
            this.options = options;
        }

        public override void Read(BinaryReader binaryReader)
        {
            if (!options.SkipNullBinaryCheck && binaryReader.PeekChar() == '\0')
            {
                binaryReader.ReadBytes(4);
                Value = null;
            }
            else
            {
                Value = binaryReader.ReadUInt32();
            }
        }
    }
}