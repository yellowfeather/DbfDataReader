using System;
using System.Buffers.Binary;

namespace DbfDataReader.Cdx
{
    // Encodes search values into the binary key formats Visual FoxPro uses for
    // non-character index tags, where unsigned byte-wise comparison matches value
    // order: integer keys are big-endian with the sign bit flipped, and double-based
    // keys (numeric, float, double, date) are big-endian IEEE 754 doubles with the
    // sign bit flipped for non-negatives and every bit flipped for negatives. The
    // integer format is verified against the tags in test/fixtures/foxprodb.
    internal static class CdxKeyEncoder
    {
        public const int IntegerKeyLength = 4;
        public const int DoubleKeyLength = 8;

        // days from 0001-01-01 to the start of the Julian period, matching the
        // DateTime interpretation in DbfValueDateTime
        private const int JulianDayOfDayOne = 1721426;

        private static readonly DateTime DayOne = new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        public static byte[] EncodeInteger(int value)
        {
            var key = new byte[IntegerKeyLength];
            BinaryPrimitives.WriteInt32BigEndian(key, value);
            key[0] ^= 0x80;

            return key;
        }

        public static int DecodeInteger(ReadOnlySpan<byte> key)
        {
            Span<byte> bytes = stackalloc byte[IntegerKeyLength];
            key.CopyTo(bytes);
            bytes[0] ^= 0x80;

            return BinaryPrimitives.ReadInt32BigEndian(bytes);
        }

        public static byte[] EncodeDouble(double value)
        {
            var key = new byte[DoubleKeyLength];
            BinaryPrimitives.WriteInt64BigEndian(key, BitConverter.DoubleToInt64Bits(value));

            if ((key[0] & 0x80) == 0)
            {
                key[0] ^= 0x80;
            }
            else
            {
                for (var i = 0; i < key.Length; i++) key[i] ^= 0xFF;
            }

            return key;
        }

        public static double DecodeDouble(ReadOnlySpan<byte> key)
        {
            Span<byte> bytes = stackalloc byte[DoubleKeyLength];
            key.CopyTo(bytes);

            if ((bytes[0] & 0x80) != 0)
            {
                bytes[0] ^= 0x80;
            }
            else
            {
                for (var i = 0; i < bytes.Length; i++) bytes[i] ^= 0xFF;
            }

            return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(bytes));
        }

        // date and datetime keys are Julian day numbers (with the time of day as the
        // fractional part) in the double key format
        public static double ToJulianDay(DateTime value)
        {
            return (value.Date - DayOne).Days + JulianDayOfDayOne + value.TimeOfDay.TotalDays;
        }

        public static byte[] EncodeDate(DateTime value)
        {
            return EncodeDouble(ToJulianDay(value));
        }
    }
}
