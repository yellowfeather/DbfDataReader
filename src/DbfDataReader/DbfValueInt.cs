using System;
using System.Globalization;

namespace DbfDataReader
{
    public class DbfValueInt : DbfValue<int?>
    {
        private static readonly NumberFormatInfo _intNumberFormat = new NumberFormatInfo();

        public DbfValueInt(int start, int length) : base(start, length)
        {
        }

        public override void Read(ReadOnlySpan<byte> bytes)
        {
            if (bytes[0] == '\0')
            {
                Value = null;
            }
            else if (AsciiFieldParser.TryParse(bytes,
                NumberStyles.Integer | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                _intNumberFormat, out int value))
            {
                Value = value;
            }
            else
            {
                Value = null;
            }
        }
    }
}