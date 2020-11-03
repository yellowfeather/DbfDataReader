using System;
using System.Globalization;
using System.IO;

namespace DbfDataReader
{
    public class DbfValueDate : DbfValue<DateTime?>
    {
        public DbfValueDate(int length) : base(length)
        {
        }

        public override void Read(BinaryReader binaryReader)
        {
            var value = new string(binaryReader.ReadChars(8));
            var nullIdx = value.IndexOf((char)0);
            if (nullIdx >= 0)
            {
                value = value.Substring(0, nullIdx);   // trim off everything past & including the first NUL byte
            }

            if (string.IsNullOrWhiteSpace(value))
                Value = null;
            else if (DateTime.TryParseExact(value, "yyyyMMdd", null,
                DateTimeStyles.AllowLeadingWhite | DateTimeStyles.AllowTrailingWhite,
                out DateTime valueOut))
                Value = valueOut;
            else
                Value = null;
        }

        public override string ToString()
        {
            return Value?.ToString("d") ?? string.Empty;
        }
    }
}
