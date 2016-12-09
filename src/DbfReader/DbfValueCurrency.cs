using System;
using System.IO;

namespace DbfReader
{
    public class DbfValueCurrency : DbfValue<float?>
    {
        public DbfValueCurrency(int length, int decimalCount) : base(length)
        {
            DecimalCount = decimalCount;
        }

        public int DecimalCount { get; set; }

        public override void Read(BinaryReader binaryReader)
        {
            var bytes = binaryReader.ReadBytes(Length);
            if (bytes[0] == '\0')
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