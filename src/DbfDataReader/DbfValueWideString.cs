using System;
using System.Text;

namespace DbfDataReader
{
    public class DbfValueWideString : DbfValue<string>
    {
        private const char NullChar = '\0';

        public DbfValueWideString(int start, int length) : base(start, length)
        {
        }
        
        public override void Read(ReadOnlySpan<byte> bytes)
        {
            if (bytes[0] == NullChar)
            {
                Value = null;
                return;
            }

            var value = Encoding.Unicode.GetString(bytes);
            Value = value.Trim(NullChar, ' ');
        }
    }
}