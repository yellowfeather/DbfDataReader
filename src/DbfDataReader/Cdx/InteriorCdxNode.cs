using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace DbfDataReader.Cdx
{
    internal sealed class InteriorCdxNode : BaseCdxNode
    {
        private const int KeyAreaOffset = 12;
        private const int KeyAreaLength = 500;

        private InteriorCdxNode(long offset, CdxIndexHeader indexHeader, CdxNodeAttributes attributes, int keyCount,
            int leftSibling, int rightSibling, InteriorCdxKeyEntry[] keyEntries)
            : base(offset, indexHeader, attributes, keyCount, leftSibling, rightSibling)
        {
            KeyEntries = keyEntries;
        }

        public IReadOnlyList<InteriorCdxKeyEntry> KeyEntries { get; }

        public static InteriorCdxNode Read(CdxIndexHeader indexHeader, long offset, CdxNodeAttributes attributes,
            int keyCount, int leftSibling, int rightSibling, ReadOnlySpan<byte> bytes)
        {
            if (leftSibling < NoSibling || rightSibling < NoSibling)
                throw new CdxException(CdxErrorCode.InvalidInteriorNodeSibling);

            // each entry is the key followed by two big-endian UInt32 values: the record number
            // and the file offset of the child node (the documented "4 hex characters" IDX format
            // does not apply to compound index interior nodes)
            var entrySize = indexHeader.KeyLength + 8;
            if (keyCount * entrySize > KeyAreaLength)
                throw new CdxException(CdxErrorCode.InvalidInteriorNodeKeyCount);

            var keyArea = bytes.Slice(KeyAreaOffset, KeyAreaLength);

            var entries = new InteriorCdxKeyEntry[keyCount];
            for (var i = 0; i < keyCount; i++)
            {
                var entry = keyArea.Slice(i * entrySize, entrySize);

                var keyBytes = entry.Slice(0, indexHeader.KeyLength).ToArray();
                var recordNumber = BinaryPrimitives.ReadUInt32BigEndian(entry.Slice(indexHeader.KeyLength, 4));
                var nodePointer = (int)BinaryPrimitives.ReadUInt32BigEndian(entry.Slice(indexHeader.KeyLength + 4, 4));

                entries[i] = new InteriorCdxKeyEntry(keyBytes, recordNumber, nodePointer);
            }

            return new InteriorCdxNode(offset, indexHeader, attributes, keyCount, leftSibling, rightSibling, entries);
        }
    }

    internal sealed class InteriorCdxKeyEntry
    {
        public InteriorCdxKeyEntry(byte[] keyBytes, uint recordNumber, int nodePointer)
        {
            KeyBytes = keyBytes;
            RecordNumber = recordNumber;
            NodePointer = nodePointer;
        }

        public byte[] KeyBytes { get; }

        public uint RecordNumber { get; }

        public int NodePointer { get; }
    }
}
