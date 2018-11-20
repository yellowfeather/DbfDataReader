using System;
using System.IO;
using System.Text;

namespace DbfDataReader
{
    public static class BinaryReaderExtensions
    {
        private const char NullChar = '\0';

        public static short ReadBigEndianInt16(this BinaryReader binaryReader)
        {
            var bytes = binaryReader.ReadBytes(2);
            Array.Reverse(bytes);
            return BitConverter.ToInt16(bytes, 0);
        }

        public static ushort ReadBigEndianUInt16(this BinaryReader binaryReader)
        {
            var bytes = binaryReader.ReadBytes(2);
            Array.Reverse(bytes);
            return BitConverter.ToUInt16(bytes, 0);
        }

        public static int ReadBigEndianInt32(this BinaryReader binaryReader)
        {
            var bytes = binaryReader.ReadBytes(4);
            Array.Reverse(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }
        public static uint ReadBigEndianUInt32(this BinaryReader binaryReader)
        {
            var bytes = binaryReader.ReadBytes(4);
            Array.Reverse(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }

        public static string ReadString(this BinaryReader binaryReader, int fieldLength, Encoding encoding)
        {
            var chars = binaryReader.ReadBytes(fieldLength);
            if (chars[0] == NullChar)
            {
                return null;
            }
            var value = encoding.GetString(chars);
            return value.Trim(NullChar, ' ');
        }
    }
}
