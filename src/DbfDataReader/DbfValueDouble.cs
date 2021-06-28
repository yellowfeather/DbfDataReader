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
#if NET48
            Value = BitConverter.ToDouble(bytes.ToArray(), 0);
#else
            Value = BitConverter.ToDouble(bytes);
#endif
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