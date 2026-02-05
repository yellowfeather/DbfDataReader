using System;
using System.Text;

namespace DbfDataReader
{
    public class DbfValueString : DbfValue<string>
    {
        private const char NullChar = '\0';

        public DbfValueString(int start, int length, Encoding encoding) : this(start, length, encoding, StringTrimmingOption.Trim)
        {
        }

        public DbfValueString(int start, int length, Encoding encoding, StringTrimmingOption stringTrimming) : base(start, length)
        {
            Encoding = encoding;
            StringTrimming = stringTrimming;
        }

        protected readonly Encoding Encoding;
        protected readonly StringTrimmingOption StringTrimming;

        public override void Read(ReadOnlySpan<byte> bytes)
        {
            if (bytes[0] == NullChar)
            {
                Value = null;
                return;
            }

            var value = Encoding.GetString(bytes);
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