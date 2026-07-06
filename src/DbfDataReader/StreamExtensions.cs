#if NETSTANDARD2_1
using System.IO;

namespace DbfDataReader
{
    internal static class StreamExtensions
    {
        public static void ReadExactly(this Stream stream, byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                var read = stream.Read(buffer, offset, count);
                if (read == 0) throw new EndOfStreamException();

                offset += read;
                count -= read;
            }
        }
    }
}
#endif
