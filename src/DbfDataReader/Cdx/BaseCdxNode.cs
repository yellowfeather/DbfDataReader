using System;
using System.Buffers.Binary;
using System.Text;

namespace DbfDataReader.Cdx
{
    internal abstract class BaseCdxNode
    {
        public const int NodeSize = 512;
        public const int NoSibling = -1;

        protected BaseCdxNode(long offset, CdxIndexHeader indexHeader, CdxNodeAttributes attributes, int keyCount,
            int leftSibling, int rightSibling)
        {
            Offset = offset;
            IndexHeader = indexHeader;
            Attributes = attributes;
            KeyCount = keyCount;
            LeftSibling = leftSibling;
            RightSibling = rightSibling;
        }

        public long Offset { get; }

        public CdxIndexHeader IndexHeader { get; }

        public CdxNodeAttributes Attributes { get; }

        public int KeyCount { get; }

        public int LeftSibling { get; }

        public int RightSibling { get; }

        public static BaseCdxNode Read(CdxIndexHeader indexHeader, long offset, ReadOnlySpan<byte> bytes,
            Encoding encoding)
        {
            var attributes = (CdxNodeAttributes)BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(0, 2));
            if ((attributes | CdxNodeAttributes.All) != CdxNodeAttributes.All)
                throw new CdxException(CdxErrorCode.InvalidNodeAttributes);

            return attributes.HasFlag(CdxNodeAttributes.LeafNode)
                ? LeafCdxNode.Read(indexHeader, offset, attributes, bytes, encoding)
                : (BaseCdxNode)InteriorCdxNode.Read(indexHeader, offset, attributes, bytes);
        }

        internal static (int KeyCount, int LeftSibling, int RightSibling) ReadCommonFields(ReadOnlySpan<byte> bytes)
        {
            var keyCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(2, 2));
            var leftSibling = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(4, 4));
            var rightSibling = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(8, 4));

            return (keyCount, leftSibling, rightSibling);
        }
    }
}
