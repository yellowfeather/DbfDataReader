using System;
using System.Globalization;

namespace DbfDataReader
{
    public class DbfValueCurrency : DbfValue<decimal?>
    {
        public DbfValueCurrency(int start, int length, int decimalCount) : base(start, length)
        {
            DecimalCount = decimalCount;
        }

        public int DecimalCount { get; set; }

        public override void Read(ReadOnlySpan<byte> bytes)
        {
            // currency is stored as a 64-bit integer scaled by 10,000
            var value = BitConverter.ToInt64(bytes);
            Value = value / 10000m;
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