using System;
using System.IO;
using System.Text;

namespace DbfDataReader
{
    public class DbfMemoDbase3 : DbfMemo
    {
        private const byte EndOfMemo = 0x1a;
        private const byte NullByte = 0x00;

        // reused across memos; grows only when a memo spans more blocks than any before it
        private byte[] _buffer = new byte[DefaultBlockSize];

        public DbfMemoDbase3(string path) : base(path)
        {
        }

        public DbfMemoDbase3(string path, Encoding encoding) : base(path, encoding)
        {
        }

        public DbfMemoDbase3(Stream stream, Encoding encoding) : base(stream, encoding)
        {
        }

        /// <summary>
        /// Reads the memo that starts at <paramref name="startBlock"/>.
        /// </summary>
        /// <remarks>
        /// A dBASE III memo has no stored length: it runs over consecutive 512 byte blocks
        /// and ends at the first end-of-memo marker (0x1A). The rest of the final block is
        /// padding, which may be NULs or stale data, so reading stops at the marker rather
        /// than at a block boundary. A NUL is also treated as the end, for files that omit
        /// the marker. The bytes are decoded once, so a multi-byte character that straddles
        /// a block boundary is decoded correctly.
        /// </remarks>
        public override string BuildMemo(long startBlock)
        {
            var stream = BinaryReader.BaseStream;
            stream.Seek(Offset(startBlock), SeekOrigin.Begin);

            var length = 0;

            while (true)
            {
                if (_buffer.Length - length < DefaultBlockSize)
                {
                    Array.Resize(ref _buffer, _buffer.Length * 2);
                }

                var read = ReadBlock(stream, _buffer, length);
                var end = _buffer.AsSpan(length, read).IndexOfAny(EndOfMemo, NullByte);
                if (end >= 0)
                {
                    length += end;
                    break;
                }

                length += read;

                // a short block is the end of the file: the memo is unterminated
                if (read < DefaultBlockSize) break;
            }

            return CurrentEncoding.GetString(_buffer, 0, length).Trim(' ');
        }

        private static int ReadBlock(Stream stream, byte[] buffer, int offset)
        {
            var total = 0;

            while (total < DefaultBlockSize)
            {
                var read = stream.Read(buffer, offset + total, DefaultBlockSize - total);
                if (read == 0) break;

                total += read;
            }

            return total;
        }
    }
}
