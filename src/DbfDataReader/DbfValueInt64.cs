using System;
using System.Globalization;
using System.Text;

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
            else
            {
#if NET48
                var stringValue = Encoding.ASCII.GetString(bytes.ToArray());
#else
                var stringValue = Encoding.ASCII.GetString(bytes);
#endif

                if (Int64.TryParse(stringValue,
                    NumberStyles.Integer | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                    _intNumberFormat, out var value))
                    Value = value;
                else
                    Value = null;
            }
        }
    }
}