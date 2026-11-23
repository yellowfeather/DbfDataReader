using System;
using System.Buffers.Binary;
using System.Text;

namespace DbfDataReader.Cdx
{
    public class CdxIndexHeader
    {
        public const int HeaderSize = 1024;

        internal CdxIndexHeader(ReadOnlySpan<byte> bytes, long offset)
        {
            Offset = offset;

            RootNodePointer = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(0, 4));
            FreeNodeListPointer = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(4, 4));
            KeyLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(12, 2));
            Options = (CdxIndexOptions)bytes[14];
            Signature = bytes[15];

            // 16 - 501  - reserved
            Order = (CdxIndexOrder)BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(502, 2));
            // 504 - 505 - reserved
            var forExpressionLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(506, 2));
            // 508 - 509 - reserved
            var keyExpressionLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(510, 2));

            if ((Options | CdxIndexOptions.All) != CdxIndexOptions.All)
                throw new CdxException(CdxErrorCode.InvalidIndexOptions);

            // the last 512 bytes are a pool holding the key expression followed by the FOR expression
            var pool = bytes.Slice(512, 512);
            if (keyExpressionLength + forExpressionLength > pool.Length)
                throw new CdxException(CdxErrorCode.InvalidExpressionPoolLength);

            KeyExpression = ReadExpression(pool.Slice(0, keyExpressionLength));
            ForExpression = Options.HasFlag(CdxIndexOptions.HasForClause)
                ? ReadExpression(pool.Slice(keyExpressionLength, forExpressionLength))
                : string.Empty;
        }

        internal long Offset { get; }

        public long RootNodePointer { get; }

        public int FreeNodeListPointer { get; }

        public int KeyLength { get; }

        public CdxIndexOptions Options { get; }

        public byte Signature { get; }

        public CdxIndexOrder Order { get; }

        public string KeyExpression { get; }

        public string ForExpression { get; }

        private static string ReadExpression(ReadOnlySpan<byte> bytes)
        {
            var length = bytes.Length;
            while (length > 0 && bytes[length - 1] == 0x00) length--;

            return Encoding.ASCII.GetString(bytes.Slice(0, length));
        }
    }
}
