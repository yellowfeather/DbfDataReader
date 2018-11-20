using System;
using System.Globalization;
using System.IO;

namespace DbfDataReader
{
    public class DbfValueFloat : DbfValue<float?>
    {
        private static readonly NumberFormatInfo
            _floatNumberFormat = new NumberFormatInfo {NumberDecimalSeparator = "."};

        [Obsolete("This constructor should no longer be used. Use DbfValueFloat(System.Int32, System.Int32) instead.")]
        public DbfValueFloat(int length) : this(length, 0)
        {
        }

        public DbfValueFloat(int length, int decimalCount) : base(length)
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
                ? $"0.{new string('0', DecimalCount)}"
                : null;

            return Value?.ToString(format, NumberFormatInfo.CurrentInfo) ?? string.Empty;
        }
    }
}