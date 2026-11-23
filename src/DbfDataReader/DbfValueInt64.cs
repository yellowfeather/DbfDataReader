using System;
using System.Globalization;

namespace DbfDataReader
{
    public class DbfValueInt64 : DbfValue<Int64?>
    {
        private static readonly NumberFormatInfo _intNumberFormat = new NumberFormatInfo();

        public DbfValueInt64(int start, int length) : base(start, length)
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
                _intNumberFormat, out long value))
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