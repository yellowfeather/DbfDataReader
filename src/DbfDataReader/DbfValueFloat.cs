using System;
using System.Globalization;
using System.Text;

namespace DbfDataReader
{
    public class DbfValueFloat : DbfValue<float?>
    {
        private static readonly NumberFormatInfo
            _floatNumberFormat = new NumberFormatInfo {NumberDecimalSeparator = "."};

        [Obsolete("This constructor should no longer be used. Use DbfValueFloat(System.Int32, System.Int32) instead.")]
        public DbfValueFloat(int start, int length) : this(start, length, 0)
        {
        }

        public DbfValueFloat(int start, int length, int decimalCount) : base(start, length)
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

                if (float.TryParse(stringValue,
                    NumberStyles.Float | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                    _floatNumberFormat, out var value))
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