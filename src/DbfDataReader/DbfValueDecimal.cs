using System;
using System.Globalization;
using System.Text;

namespace DbfDataReader
{
    public class DbfValueDecimal : DbfValue<decimal?>
    {
        private static readonly NumberFormatInfo _decimalNumberFormat = new NumberFormatInfo
            {NumberDecimalSeparator = "."};

        public DbfValueDecimal(int start, int length, int decimalCount) : base(start, length)
        {
            DecimalCount = decimalCount;
        }

        public int DecimalCount { get; }

        public override void Read(ReadOnlySpan<byte> bytes)
        {
            if (bytes[0] == '\0')
            {
                Value = null;
            }
            else
            {
                var stringValue = Encoding.ASCII.GetString(bytes);

                if (decimal.TryParse(stringValue,
                    NumberStyles.Float | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                    _decimalNumberFormat, out var value))
                    Value = value;
                else
                    Value = null;
            }
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