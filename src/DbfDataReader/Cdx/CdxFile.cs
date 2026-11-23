using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DbfDataReader.Cdx
{
    public class CdxFile : Disposable
    {
        private readonly bool _leaveOpen;
        private readonly long _startOffset;
        private readonly byte[] _nodeBuffer = new byte[BaseCdxNode.NodeSize];

        private Dictionary<string, CdxIndex> _taggedIndexes;

        public CdxFile(string path, Encoding encoding = null)
        {
            if (!File.Exists(path)) throw new FileNotFoundException();

            Path = path;
            CurrentEncoding = encoding ?? Encoding.ASCII;
            Stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            Init();
        }

        public CdxFile(Stream stream, Encoding encoding = null, bool leaveOpen = false)
        {
            Path = string.Empty;
            CurrentEncoding = encoding ?? Encoding.ASCII;
            Stream = stream;
            _leaveOpen = leaveOpen;
            _startOffset = stream.CanSeek ? stream.Position : 0;

            Init();
        }

        private void Init()
        {
            Header = ReadHeader(0);
            if (!Header.Options.HasFlag(CdxIndexOptions.IsCompoundIndexHeader))
                throw new CdxException(CdxErrorCode.NotACompoundIndexHeader);

            RootNode = ReadNode(Header.RootNodePointer, Header);
        }

        public string Path { get; }

        public Encoding CurrentEncoding { get; }

        public Stream Stream { get; private set; }

        public CdxIndexHeader Header { get; private set; }

        public bool IsClosed => Stream == null;

        // The file root node is a tag directory: its keys are tag names and its record
        // numbers are the file offsets of the tag index headers.
        internal BaseCdxNode RootNode { get; private set; }

        internal int MaxNodeCount => (int)(Stream.Length / BaseCdxNode.NodeSize) + 1;

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!disposing) return;
                if (!_leaveOpen) Stream?.Dispose();
            }
            finally
            {
                Stream = null;
            }
        }

        public IReadOnlyCollection<string> TagNames
        {
            get
            {
                ReadTaggedIndexes();
                return _taggedIndexes.Keys;
            }
        }

        public IReadOnlyDictionary<string, CdxIndex> ReadTaggedIndexes()
        {
            if (_taggedIndexes != null) return _taggedIndexes;

            var tagDirectory = new CdxIndex(this, Header, RootNode);

            var taggedIndexes = new Dictionary<string, CdxIndex>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in tagDirectory.EnumerateEntries())
            {
                taggedIndexes[entry.Key] = ReadIndex(entry.RecordNumber);
            }

            _taggedIndexes = taggedIndexes;
            return _taggedIndexes;
        }

        public CdxIndex GetIndex(string tagName)
        {
            if (tagName == null) throw new ArgumentNullException(nameof(tagName));

            if (!ReadTaggedIndexes().TryGetValue(tagName, out var index))
                throw new ArgumentException($"Tag '{tagName}' was not found in the index file.", nameof(tagName));

            return index;
        }

        private CdxIndex ReadIndex(long offset)
        {
            var header = ReadHeader(offset);
            if (!header.Options.HasFlag(CdxIndexOptions.IsCompoundIndexHeader))
                throw new CdxException(CdxErrorCode.NotACompoundIndexHeader);

            var rootNode = ReadNode(header.RootNodePointer, header);
            if (!rootNode.Attributes.HasFlag(CdxNodeAttributes.RootNode))
                throw new CdxException(CdxErrorCode.RootNodeDoesNotHaveRootAttribute);

            return new CdxIndex(this, header, rootNode);
        }

        private CdxIndexHeader ReadHeader(long offset)
        {
            var buffer = new byte[CdxIndexHeader.HeaderSize];
            Stream.Seek(_startOffset + offset, SeekOrigin.Begin);
            Stream.ReadExactly(buffer, 0, buffer.Length);

            return new CdxIndexHeader(buffer, offset);
        }

        internal BaseCdxNode ReadNode(long offset, CdxIndexHeader indexHeader)
        {
            Stream.Seek(_startOffset + offset, SeekOrigin.Begin);
            Stream.ReadExactly(_nodeBuffer, 0, BaseCdxNode.NodeSize);

            return BaseCdxNode.Read(indexHeader, offset, _nodeBuffer, CurrentEncoding);
        }
    }
}
