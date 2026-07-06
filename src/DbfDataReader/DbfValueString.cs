using System;
using System.Text;

namespace DbfDataReader
{
    public class DbfValueString : DbfValue<string>
    {
        private const char NullChar = '\0';
        private const byte NullByte = 0x00;
        private const byte SpaceByte = 0x20;
        private const int Utf8CodePage = 65001;

        public DbfValueString(int start, int length, Encoding encoding) : this(start, length, encoding, StringTrimmingOption.Trim)
        {
        }

        public DbfValueString(int start, int length, Encoding encoding, StringTrimmingOption stringTrimming) : base(start, length)
        {
            Encoding = encoding;
            StringTrimming = stringTrimming;

            // trimming the raw bytes (one right-sized string instead of a padded
            // string plus a trimmed copy) is only safe when 0x00/0x20 can never be
            // part of a multi-byte sequence; other encodings decode first
            _trimAsBytes = encoding.IsSingleByte || encoding.CodePage == Utf8CodePage;
        }

        protected readonly Encoding Encoding;
        protected readonly StringTrimmingOption StringTrimming;
        private readonly bool _trimAsBytes;

        public override void Read(ReadOnlySpan<byte> bytes)
        {
            if (bytes[0] == NullChar)
            {
                Value = null;
                return;
            }

            Value = _trimAsBytes
                ? Encoding.GetString(TrimPadding(bytes))
                : TrimString(Encoding.GetString(bytes));
        }

        private ReadOnlySpan<byte> TrimPadding(ReadOnlySpan<byte> bytes)
        {
            // parity with TrimString: NULs always come off both ends first,
            // then spaces according to the trimming option
            bytes = TrimByte(bytes, NullByte, trimStart: true, trimEnd: true);

            return StringTrimming switch
            {
                StringTrimmingOption.None => bytes,
                StringTrimmingOption.TrimStart => TrimByte(bytes, SpaceByte, trimStart: true, trimEnd: false),
                StringTrimmingOption.TrimEnd => TrimByte(bytes, SpaceByte, trimStart: false, trimEnd: true),
                _ => TrimByte(bytes, SpaceByte, trimStart: true, trimEnd: true),
            };
        }

        private static ReadOnlySpan<byte> TrimByte(ReadOnlySpan<byte> bytes, byte trim, bool trimStart, bool trimEnd)
        {
            var start = 0;
            var end = bytes.Length;

            if (trimStart)
            {
                while (start < end && bytes[start] == trim) start++;
            }

            if (trimEnd)
            {
                while (end > start && bytes[end - 1] == trim) end--;
            }

            return bytes[start..end];
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