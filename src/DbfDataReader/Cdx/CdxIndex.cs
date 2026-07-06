using System;
using System.Collections.Generic;

namespace DbfDataReader.Cdx
{
    public class CdxIndex
    {
        private const byte Pad = 0x20;

        private readonly CdxFile _file;
        private readonly BaseCdxNode _rootNode;

        internal CdxIndex(CdxFile file, CdxIndexHeader header, BaseCdxNode rootNode)
        {
            _file = file;
            Header = header;
            _rootNode = rootNode;
        }

        public CdxIndexHeader Header { get; }

        public string KeyExpression => Header.KeyExpression;

        public string ForExpression => Header.ForExpression;

        public IEnumerable<CdxKeyEntry> EnumerateEntries()
        {
            var leaf = GetLeftmostLeafNode();
            var chainLength = 0;

            while (true)
            {
                foreach (var entry in leaf.Entries)
                {
                    yield return entry;
                }

                if (leaf.RightSibling == BaseCdxNode.NoSibling) yield break;

                GuardNodeChain(ref chainLength);
                leaf = (LeafCdxNode)ReadNode(leaf.RightSibling);
            }
        }

        public int Count()
        {
            var leaf = GetLeftmostLeafNode();
            var chainLength = 0;
            var total = 0;

            while (true)
            {
                total += leaf.KeyCount;

                if (leaf.RightSibling == BaseCdxNode.NoSibling) return total;

                GuardNodeChain(ref chainLength);
                leaf = (LeafCdxNode)ReadNode(leaf.RightSibling);
            }
        }

        public IEnumerable<CdxKeyEntry> Search(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            return Search(PadKey(key));
        }

        public IEnumerable<CdxKeyEntry> Search(byte[] key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (key.Length != Header.KeyLength)
                throw new ArgumentException(
                    $"Key length {key.Length} does not match the index key length {Header.KeyLength}.", nameof(key));

            return Search(storedKey => CdxKeyComparer.Compare(storedKey, key));
        }

        // The comparison receives a stored key and returns less than zero if the stored key sorts
        // before the wanted range, zero if it is within the range, and greater than zero if it
        // sorts after the range. Stored leaf keys may be shorter than the index key length as
        // their trailing padding is trimmed.
        public IEnumerable<CdxKeyEntry> Search(Func<byte[], int> keyComparison)
        {
            if (keyComparison == null) throw new ArgumentNullException(nameof(keyComparison));
            if (Header.Order != CdxIndexOrder.Ascending)
                throw new NotSupportedException("Searching descending indexes is not supported.");

            return SearchCore(keyComparison);
        }

        private IEnumerable<CdxKeyEntry> SearchCore(Func<byte[], int> keyComparison)
        {
            var node = _rootNode;
            var depth = 0;

            // Interior entry keys are the upper bound of their subtree, so descend into the
            // first entry that is not below the wanted range.
            while (node is InteriorCdxNode interiorNode)
            {
                if (interiorNode.KeyEntries.Count == 0)
                    throw new CdxException(CdxErrorCode.InteriorNodeHasNoKeyEntries);

                BaseCdxNode next = null;
                foreach (var entry in interiorNode.KeyEntries)
                {
                    if (keyComparison(entry.KeyBytes) >= 0)
                    {
                        next = ReadNode(entry.NodePointer);
                        break;
                    }
                }

                if (next == null) yield break;

                GuardNodeChain(ref depth);
                node = next;
            }

            // Scan leaf entries, following right siblings while matches can continue across nodes.
            var leaf = (LeafCdxNode)node;
            var chainLength = 0;

            while (true)
            {
                var entries = leaf.Entries;
                var lastMatchedIndex = -1;

                for (var i = 0; i < entries.Count; i++)
                {
                    var cmp = keyComparison(entries[i].KeyBytes);
                    if (cmp < 0) continue;
                    if (cmp > 0) yield break;

                    yield return entries[i];
                    lastMatchedIndex = i;
                }

                if (lastMatchedIndex != entries.Count - 1) yield break;
                if (leaf.RightSibling == BaseCdxNode.NoSibling) yield break;

                GuardNodeChain(ref chainLength);
                leaf = (LeafCdxNode)ReadNode(leaf.RightSibling);
            }
        }

        private LeafCdxNode GetLeftmostLeafNode()
        {
            var node = _rootNode;
            var depth = 0;

            while (node is InteriorCdxNode interiorNode)
            {
                if (interiorNode.KeyEntries.Count == 0)
                    throw new CdxException(CdxErrorCode.InteriorNodeHasNoKeyEntries);

                GuardNodeChain(ref depth);
                node = ReadNode(interiorNode.KeyEntries[0].NodePointer);
            }

            var leaf = (LeafCdxNode)node;
            if (leaf.LeftSibling != BaseCdxNode.NoSibling)
                throw new CdxException(CdxErrorCode.LeftmostNodeHasLeftSibling);

            return leaf;
        }

        private BaseCdxNode ReadNode(long offset)
        {
            return _file.ReadNode(offset, Header);
        }

        private void GuardNodeChain(ref int chainLength)
        {
            chainLength++;
            if (chainLength > _file.MaxNodeCount) throw new CdxException(CdxErrorCode.NodeChainTooLong);
        }

        private byte[] PadKey(string key)
        {
            var bytes = _file.CurrentEncoding.GetBytes(key);
            if (bytes.Length > Header.KeyLength)
                throw new ArgumentException(
                    $"Key length {bytes.Length} exceeds the index key length {Header.KeyLength}.", nameof(key));
            if (bytes.Length == Header.KeyLength) return bytes;

            var padded = new byte[Header.KeyLength];
            Array.Copy(bytes, padded, bytes.Length);
            for (var i = bytes.Length; i < padded.Length; i++)
            {
                padded[i] = Pad;
            }

            return padded;
        }
    }
}
