using System;
using System.IO;

namespace DbfDataReader
{
    public class DbfValueCurrency : DbfValue<float?>
    {
        private readonly DbfDataReaderOptions options;

        public DbfValueCurrency(int length, int decimalCount) : this(length, decimalCount, new DbfDataReaderOptions())
        {
        }

        public DbfValueCurrency(int length, int decimalCount, DbfDataReaderOptions options) : base(length)
        {
            DecimalCount = decimalCount;
            this.options = options;
        }

        public int DecimalCount { get; set; }

        public override void Read(BinaryReader binaryReader)
        {
            var bytes = binaryReader.ReadBytes(Length);
            if (!options.SkipNullBinaryCheck && bytes[0] == '\0')
            {
                Value = null;
            }
            else
            {
                var value = BitConverter.ToUInt64(bytes, 0);
                Value = value / 10000.0f;
            }
        }

        public override string ToString()
        {
            var format = $"f{DecimalCount}";
            return Value?.ToString(format) ?? string.Empty;
        }
    }
}