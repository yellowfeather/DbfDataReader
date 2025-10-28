using System;
using System.Text;

namespace DbfDataReader
{
    public class DbfValueWideString : DbfValue<string>
    {
        private const char NullChar = '\0';

        public DbfValueWideString(int start, int length) : this(start, length, StringTrimmingOption.Trim)
        {
        }

        public DbfValueWideString(int start, int length, StringTrimmingOption stringTrimming) : base(start, length)
        {
            StringTrimming = stringTrimming;
        }

        protected readonly StringTrimmingOption StringTrimming;
        
        public override void Read(ReadOnlySpan<byte> bytes)
        {
            if (bytes[0] == NullChar)
            {
                Value = null;
                return;
            }

            var value = Encoding.Unicode.GetString(bytes);
            Value = TrimString(value);
        }

        private string TrimString(string value)
        {
            var trimmedNull = value.Trim(NullChar);

            return StringTrimming switch
            {
                StringTrimmingOption.None => trimmedNull,
                StringTrimmingOption.Trim => trimmedNull.Trim(' '),
                StringTrimmingOption.TrimStart => trimmedNull.TrimStart(' '),
                StringTrimmingOption.TrimEnd => trimmedNull.TrimEnd(' '),
                _ => trimmedNull.Trim(' '),
            };
        }
    }
}