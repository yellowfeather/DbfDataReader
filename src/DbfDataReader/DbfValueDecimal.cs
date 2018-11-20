using System.Globalization;
using System.IO;

namespace DbfDataReader
{
    public class DbfValueDecimal : DbfValue<decimal?>
    {
        private static readonly NumberFormatInfo _decimalNumberFormat = new NumberFormatInfo
            {NumberDecimalSeparator = "."};

        public DbfValueDecimal(int length, int decimalCount) : base(length)
        {
            DecimalCount = decimalCount;
        }

        public int DecimalCount { get; }

        public override void Read(BinaryReader binaryReader)
        {
            if (binaryReader.PeekChar() == '\0')
            {
                binaryReader.ReadBytes(Length);
                Value = null;
            }
            else
            {
                var stringValue = new string(binaryReader.ReadChars(Length));

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
                ? $"0.{new string('0', DecimalCount)}"
                : null;

            return Value?.ToString(format, NumberFormatInfo.CurrentInfo) ?? string.Empty;
        }
    }
}