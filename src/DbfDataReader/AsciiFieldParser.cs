using System;
using System.Globalization;

namespace DbfDataReader
{
    // Parses field text straight from the record buffer without allocating an
    // intermediate string. Bytes above 0x7f decode to '?' exactly as
    // Encoding.ASCII.GetString would, so results match string-based parsing.
    internal static class AsciiFieldParser
    {
        private const byte MaxAscii = 0x7f;

        public static bool TryParse(ReadOnlySpan<byte> bytes, NumberStyles styles, NumberFormatInfo format,
            out decimal value)
        {
            Span<char> chars = stackalloc char[bytes.Length];
            Decode(bytes, chars);
            return decimal.TryParse(chars, styles, format, out value);
        }

        public static bool TryParse(ReadOnlySpan<byte> bytes, NumberStyles styles, NumberFormatInfo format,
            out int value)
        {
            Span<char> chars = stackalloc char[bytes.Length];
            Decode(bytes, chars);
            return int.TryParse(chars, styles, format, out value);
        }

        public static bool TryParse(ReadOnlySpan<byte> bytes, NumberStyles styles, NumberFormatInfo format,
            out long value)
        {
            Span<char> chars = stackalloc char[bytes.Length];
            Decode(bytes, chars);
            return long.TryParse(chars, styles, format, out value);
        }

        public static bool TryParse(ReadOnlySpan<byte> bytes, NumberStyles styles, NumberFormatInfo format,
            out float value)
        {
            Span<char> chars = stackalloc char[bytes.Length];
            Decode(bytes, chars);
            return float.TryParse(chars, styles, format, out value);
        }

        public static bool TryParseDate(ReadOnlySpan<byte> bytes, string dateFormat, out DateTime value)
        {
            Span<char> chars = stackalloc char[bytes.Length];
            Decode(bytes, chars);

            // everything past and including the first NUL byte is padding
            ReadOnlySpan<char> text = chars;
            var nullIndex = text.IndexOf('\0');
            if (nullIndex >= 0)
            {
                text = text.Slice(0, nullIndex);
            }

            if (text.IsWhiteSpace())
            {
                value = default;
                return false;
            }

            return DateTime.TryParseExact(text, dateFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowLeadingWhite | DateTimeStyles.AllowTrailingWhite, out value);
        }

        private static void Decode(ReadOnlySpan<byte> bytes, Span<char> chars)
        {
            for (var i = 0; i < bytes.Length; i++)
            {
                var b = bytes[i];
                chars[i] = b <= MaxAscii ? (char) b : '?';
            }
        }
    }
}
