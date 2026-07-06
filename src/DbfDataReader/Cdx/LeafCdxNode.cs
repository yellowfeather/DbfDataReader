using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace DbfDataReader.Cdx
{
    internal sealed class LeafCdxNode : BaseCdxNode
    {
        private const int PackedAreaOffset = 24;
        private const int PackedAreaLength = 488;

        private LeafCdxNode(long offset, CdxIndexHeader indexHeader, CdxNodeAttributes attributes, int keyCount,
            int leftSibling, int rightSibling, CdxKeyEntry[] entries)
            : base(offset, indexHeader, attributes, keyCount, leftSibling, rightSibling)
        {
            Entries = entries;
        }

        public IReadOnlyList<CdxKeyEntry> Entries { get; }

        public static LeafCdxNode Read(CdxIndexHeader indexHeader, long offset, CdxNodeAttributes attributes,
            int keyCount, int leftSibling, int rightSibling, ReadOnlySpan<byte> bytes, Encoding encoding)
        {
            var recordNumberMask = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(14, 4));
            var duplicateCountMask = bytes[18];
            var trailingCountMask = bytes[19];
            var recordNumberBits = bytes[20];
            var duplicateCountBits = bytes[21];
            var packedEntryLength = bytes[23];

            if (packedEntryLength > 8)
                throw new CdxException(CdxErrorCode.PackedKeyEntryLengthTooLong);
            if (keyCount * packedEntryLength > PackedAreaLength)
                throw new CdxException(CdxErrorCode.InvalidLeafNodeKeyCount);

            var packed = bytes.Slice(PackedAreaOffset, PackedAreaLength);

            // Packed entries sit at the front of the packed area; the new bytes of each key sit
            // at the back, growing backwards. Keys are prefix-compressed against the previous key
            // (duplicate count) and suffix-compressed by dropping trailing padding (trailing count).
            var keyLength = indexHeader.KeyLength;
            var keyValueSource = PackedAreaLength;
            var entries = new CdxKeyEntry[keyCount];
            byte[] previousKey = null;

            for (var i = 0; i < keyCount; i++)
            {
                var packedEntry = CdxKeyPacking.ReadPackedEntry(packed, i * packedEntryLength, packedEntryLength);

                var recordNumber = (int)(packedEntry & recordNumberMask);
                packedEntry >>= recordNumberBits;
                var duplicateBytes = (int)(packedEntry & duplicateCountMask);
                packedEntry >>= duplicateCountBits;
                var trailingBytes = (int)(packedEntry & trailingCountMask);

                var newBytesCount = keyLength - duplicateBytes - trailingBytes;
                keyValueSource -= newBytesCount;

                if (newBytesCount < 0 || keyValueSource < 0)
                    throw new CdxException(CdxErrorCode.InvalidLeafNodeCalculatedKeyStartIndex);
                if (previousKey == null && duplicateBytes > 0)
                    throw new CdxException(CdxErrorCode.FirstLeafNodeKeyEntryHasDuplicateBytes);

                var actualKeyLength = keyLength - trailingBytes;
                var keyBytes = new byte[actualKeyLength];

                var duplicated = Math.Min(duplicateBytes, actualKeyLength);
                for (var d = 0; d < duplicated; d++)
                {
                    keyBytes[d] = previousKey[d];
                }

                for (int b = duplicateBytes, source = 0; source < newBytesCount && b < actualKeyLength; b++, source++)
                {
                    keyBytes[b] = packed[keyValueSource + source];
                }

                entries[i] = new CdxKeyEntry(keyBytes, recordNumber, encoding);
                previousKey = keyBytes;
            }

            return new LeafCdxNode(offset, indexHeader, attributes, keyCount, leftSibling, rightSibling, entries);
        }
    }

    internal static class CdxKeyPacking
    {
        // A packed entry is a little-endian integer of up to eight bytes laid out, from the least
        // significant bit, as: [record number][duplicate count][trailing count].
        public static long ReadPackedEntry(ReadOnlySpan<byte> bytes, int startIndex, int length)
        {
            long packedEntry = 0;
            for (var i = length - 1; i >= 0; i--)
            {
                packedEntry = (packedEntry << 8) | bytes[startIndex + i];
            }

            return packedEntry;
        }
    }
}
