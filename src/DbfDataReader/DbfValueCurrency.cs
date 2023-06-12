using System;
using System.Globalization;

namespace DbfDataReader
{
    public class DbfValueCurrency : DbfValue<float?>
    {
        public DbfValueCurrency(int start, int length, int decimalCount) : base(start, length)
        {
            DecimalCount = decimalCount;
        }

        public int DecimalCount { get; set; }

        public override void Read(ReadOnlySpan<byte> bytes)
        {
            var value = BitConverter.ToInt64(bytes);
            Value = value / 10000.0f;
        }

        public override string ToString()
        {
            var format = DecimalCount != 0
                ? $"F{DecimalCount}"
                : null;

            return Value?.ToString(format, NumberFormatInfo.CurrentInfo) ?? string.Empty;
        }
    }
}