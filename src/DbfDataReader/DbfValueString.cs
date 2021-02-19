using System;
using System.Text;

namespace DbfDataReader
{
    public class DbfValueString : DbfValue<string>
    {
        private const char NullChar = '\0';

        public DbfValueString(int start, int length, Encoding encoding) : base(start, length)
        {
            Encoding = encoding;
        }

        protected readonly Encoding Encoding;

        public override void Read(ReadOnlySpan<byte> bytes)
        {
            if (bytes[0] == NullChar)
            {
                Value = null;
            }

            var value = Encoding.GetString(bytes);
            Value = value.Trim(NullChar, ' ');
        }
    }
}