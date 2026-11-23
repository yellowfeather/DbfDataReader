using System;

namespace DbfDataReader
{
    public class DbfValueDate : DbfValue<DateTime?>
    {
        public DbfValueDate(int start, int length) : base(start, length)
        {
        }

        public override void Read(ReadOnlySpan<byte> bytes)
        {
            if (AsciiFieldParser.TryParseDate(bytes, "yyyyMMdd", out var value))
                Value = value;
            else
                Value = null;
        }

        public override string ToString()
        {
            return Value?.ToString("d") ?? string.Empty;
        }
    }
}
