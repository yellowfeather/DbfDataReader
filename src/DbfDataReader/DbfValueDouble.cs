using System;
using System.Globalization;

namespace DbfDataReader
{
    public class DbfValueDouble : DbfValue<double?>
    {
        public DbfValueDouble(int start, int length, int decimalCount) : base(start, length)
        {
            DecimalCount = decimalCount;
        }

        public int DecimalCount { get; }

        public override void Read(ReadOnlySpan<byte> bytes)
        {
            Value = BitConverter.ToDouble(bytes);
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