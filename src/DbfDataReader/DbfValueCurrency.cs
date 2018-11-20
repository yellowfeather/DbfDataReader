using System;
using System.Globalization;
using System.IO;

namespace DbfDataReader
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

            var value = BitConverter.ToUInt64(bytes, 0);
            Value = value / 10000.0f;
        }

        public override string ToString()
        {
            var format = DecimalCount != 0
                ? $"0.{new string('0', DecimalCount)}"
                : null;

            return Value?.ToString(format, NumberFormatInfo.CurrentInfo) ?? string.Empty;
        }
    }
}