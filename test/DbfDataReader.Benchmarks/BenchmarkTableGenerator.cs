using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DbfDataReader.Benchmarks
{
    // Generates a large DBF table with a matching compound index so that index
    // benchmarks have something realistic to run against (the repository fixtures are
    // a handful of rows). The CDX writer produces the simplest structure the library's
    // strict reader accepts: uncompressed leaf entries (no duplicate/trailing
    // compression), max-key interior entries, and a two-tag directory. Generated files
    // are verified through the public API before any benchmark runs.
    public static class BenchmarkTableGenerator
    {
        private const int NodeSize = 512;
        private const int HeaderSize = 1024;
        private const int PackedAreaLength = 488;
        private const int PackedEntryLength = 6; // 32-bit record number + 8-bit dup + 8-bit trail

        public static string DbfPath(string directory) => Path.Combine(directory, "bench.dbf");

        public static void Generate(string directory, int rowCount)
        {
            Directory.CreateDirectory(directory);
            WriteDbf(DbfPath(directory), rowCount);
            WriteCdx(Path.Combine(directory, "bench.cdx"), rowCount);
        }

        public static string Code(int rowIndex) => $"C{rowIndex % 500:D6}";

        // record layout: status byte + ID ('I', 4) + CODE ('C', 10) + NAME ('C', 40) + VALUE ('N', 10)
        private static void WriteDbf(string path, int rowCount)
        {
            var columns = new (string Name, char Type, byte Length)[]
            {
                ("ID", 'I', 4),
                ("CODE", 'C', 10),
                ("NAME", 'C', 40),
                ("VALUE", 'N', 10)
            };

            var headerLength = (ushort)(32 + columns.Length * 32 + 1);
            var recordLength = (ushort)(1 + columns.Sum(c => c.Length));

            using var stream = File.Create(path);
            using var writer = new BinaryWriter(stream, Encoding.ASCII);

            var header = new byte[32];
            header[0] = 0x03; // dBase III, no memo
            header[1] = 26;
            header[2] = 7;
            header[3] = 6;
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), (uint)rowCount);
            BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(8), headerLength);
            BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(10), recordLength);
            header[29] = 0x03; // cp1252
            writer.Write(header);

            foreach (var (name, type, length) in columns)
            {
                var descriptor = new byte[32];
                Encoding.ASCII.GetBytes(name).CopyTo(descriptor, 0);
                descriptor[11] = (byte)type;
                descriptor[16] = length;
                writer.Write(descriptor);
            }

            writer.Write((byte)0x0D);

            var record = new byte[recordLength];
            for (var i = 0; i < rowCount; i++)
            {
                record[0] = 0x20;
                BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(1), i + 1);
                WriteText(record, 5, 10, Code(i));
                WriteText(record, 15, 40, $"ROW {i} FILLER TEXT");
                WriteText(record, 55, 10, ((i * 7) % 100000).ToString().PadLeft(10));
                writer.Write(record);
            }

            writer.Write((byte)0x1A);
        }

        private static void WriteText(byte[] record, int offset, int length, string text)
        {
            for (var i = 0; i < length; i++)
            {
                record[offset + i] = (byte)(i < text.Length ? text[i] : ' ');
            }
        }

        private static void WriteCdx(string path, int rowCount)
        {
            // ID: unique ascending integer keys; CODE: duplicated character keys
            var idEntries = Enumerable.Range(0, rowCount)
                .Select(i => (Key: EncodeIntegerKey(i + 1), RecNo: i + 1))
                .ToList();
            var codeEntries = Enumerable.Range(0, rowCount)
                .Select(i => (Key: PadKey(Code(i), 10), RecNo: i + 1))
                .OrderBy(e => e.Key, ByteArrayComparer.Instance)
                .ThenBy(e => e.RecNo)
                .ToList();

            var idNodes = BuildTree(idEntries, 4);
            var codeNodes = BuildTree(codeEntries, 10);

            var idHeaderOffset = HeaderSize + NodeSize; // after file header + tag directory
            var idNodesOffset = idHeaderOffset + HeaderSize;
            var codeHeaderOffset = idNodesOffset + idNodes.Count * NodeSize;
            var codeNodesOffset = codeHeaderOffset + HeaderSize;

            using var stream = File.Create(path);

            // file header: the root points at the tag directory; tag-name keys are 10 bytes
            WriteHeader(stream, 0, HeaderSize, keyLength: 10, keyExpression: "");

            // tag directory: one leaf, entries sorted by tag name, record numbers are
            // the file offsets of the tag headers
            var directory = new TreeNode(isLeaf: true)
            {
                Entries =
                {
                    (PadKey("CODE", 10), codeHeaderOffset, -1),
                    (PadKey("ID", 10), idHeaderOffset, -1)
                }
            };
            WriteNode(stream, HeaderSize, directory, keyLength: 10, isRoot: true, offsetOf: null,
                trailFor: key => 10 - TrimmedLength(key));

            WriteHeader(stream, idHeaderOffset, idNodesOffset + idNodes.Count * NodeSize, keyLength: 4,
                keyExpression: "ID", rootNodeOffset: idNodesOffset + (idNodes.Count - 1) * NodeSize);
            WriteTree(stream, idNodes, idNodesOffset, 4);

            WriteHeader(stream, codeHeaderOffset, codeNodesOffset + codeNodes.Count * NodeSize, keyLength: 10,
                keyExpression: "CODE", rootNodeOffset: codeNodesOffset + (codeNodes.Count - 1) * NodeSize);
            WriteTree(stream, codeNodes, codeNodesOffset, 10);
        }

        private sealed class TreeNode
        {
            public TreeNode(bool isLeaf)
            {
                IsLeaf = isLeaf;
            }

            public bool IsLeaf { get; }
            public List<(byte[] Key, int RecNo, int ChildIndex)> Entries { get; } = new();
            public int LeftIndex = -1;
            public int RightIndex = -1;
        }

        // builds leaves left to right, then interior levels bottom-up; the returned
        // list is in file order and the last node is the root
        private static List<TreeNode> BuildTree(List<(byte[] Key, int RecNo)> entries, int keyLength)
        {
            var nodes = new List<TreeNode>();
            var leafCapacity = PackedAreaLength / (PackedEntryLength + keyLength);
            var interiorCapacity = 500 / (keyLength + 8);

            // leaf level
            var level = new List<int>();
            for (var start = 0; start < entries.Count; start += leafCapacity)
            {
                var node = new TreeNode(isLeaf: true);
                foreach (var (key, recNo) in entries.Skip(start).Take(leafCapacity))
                {
                    node.Entries.Add((key, recNo, -1));
                }

                nodes.Add(node);
                level.Add(nodes.Count - 1);
            }

            LinkSiblings(nodes, level);

            // interior levels until a single root remains
            while (level.Count > 1)
            {
                var parents = new List<int>();
                for (var start = 0; start < level.Count; start += interiorCapacity)
                {
                    var node = new TreeNode(isLeaf: false);
                    foreach (var childIndex in level.Skip(start).Take(interiorCapacity))
                    {
                        var child = nodes[childIndex];
                        var (maxKey, maxRecNo, _) = child.Entries[^1];
                        node.Entries.Add((maxKey, maxRecNo, childIndex));
                    }

                    nodes.Add(node);
                    parents.Add(nodes.Count - 1);
                }

                LinkSiblings(nodes, parents);
                level = parents;
            }

            // move the root to the end so its offset is the last node slot
            var rootIndex = level[0];
            if (rootIndex != nodes.Count - 1)
            {
                var root = nodes[rootIndex];
                nodes.RemoveAt(rootIndex);
                nodes.Add(root);
            }

            return nodes;
        }

        private static void LinkSiblings(List<TreeNode> nodes, List<int> level)
        {
            for (var i = 0; i < level.Count; i++)
            {
                nodes[level[i]].LeftIndex = i > 0 ? level[i - 1] : -1;
                nodes[level[i]].RightIndex = i < level.Count - 1 ? level[i + 1] : -1;
            }
        }

        private static void WriteTree(Stream stream, List<TreeNode> nodes, int baseOffset, int keyLength)
        {
            int OffsetOf(int index) => baseOffset + index * NodeSize;

            for (var i = 0; i < nodes.Count; i++)
            {
                WriteNode(stream, OffsetOf(i), nodes[i], keyLength, isRoot: i == nodes.Count - 1, OffsetOf,
                    trailFor: _ => 0);
            }
        }

        private static void WriteNode(Stream stream, long offset, TreeNode node, int keyLength, bool isRoot,
            Func<int, int> offsetOf, Func<byte[], int> trailFor)
        {
            var buffer = new byte[NodeSize];
            var attributes = (ushort)((node.IsLeaf ? 2 : 0) | (isRoot ? 1 : 0));

            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(0), attributes);
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(2), (ushort)node.Entries.Count);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4),
                node.LeftIndex < 0 ? -1 : offsetOf(node.LeftIndex));
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(8),
                node.RightIndex < 0 ? -1 : offsetOf(node.RightIndex));

            if (node.IsLeaf)
            {
                WriteLeafEntries(buffer, node, keyLength, trailFor);
            }
            else
            {
                WriteInteriorEntries(buffer, node, keyLength, offsetOf);
            }

            stream.Seek(offset, SeekOrigin.Begin);
            stream.Write(buffer, 0, buffer.Length);
        }

        private static void WriteLeafEntries(byte[] buffer, TreeNode node, int keyLength, Func<byte[], int> trailFor)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(14), 0xFFFFFFFF); // record number mask
            buffer[18] = 0xFF; // duplicate count mask
            buffer[19] = 0xFF; // trailing count mask
            buffer[20] = 32; // record number bits
            buffer[21] = 8; // duplicate count bits
            buffer[22] = 8; // trailing count bits
            buffer[23] = PackedEntryLength;

            var packed = buffer.AsSpan(24, PackedAreaLength);
            var keyStart = PackedAreaLength;

            for (var i = 0; i < node.Entries.Count; i++)
            {
                var (key, recNo, _) = node.Entries[i];
                var trail = trailFor(key);
                var newBytes = keyLength - trail;

                var packedEntry = (ulong)(uint)recNo | ((ulong)trail << 40);
                for (var b = 0; b < PackedEntryLength; b++)
                {
                    packed[i * PackedEntryLength + b] = (byte)(packedEntry >> (8 * b));
                }

                keyStart -= newBytes;
                key.AsSpan(0, newBytes).CopyTo(packed.Slice(keyStart, newBytes));
            }
        }

        private static void WriteInteriorEntries(byte[] buffer, TreeNode node, int keyLength,
            Func<int, int> offsetOf)
        {
            var entrySize = keyLength + 8;
            for (var i = 0; i < node.Entries.Count; i++)
            {
                var (key, recNo, childIndex) = node.Entries[i];
                var entry = buffer.AsSpan(12 + i * entrySize, entrySize);

                key.CopyTo(entry);
                BinaryPrimitives.WriteUInt32BigEndian(entry.Slice(keyLength), (uint)recNo);
                BinaryPrimitives.WriteUInt32BigEndian(entry.Slice(keyLength + 4), (uint)offsetOf(childIndex));
            }
        }

        private static void WriteHeader(Stream stream, long offset, long endOffset, int keyLength,
            string keyExpression, long rootNodeOffset = -1)
        {
            var buffer = new byte[HeaderSize];

            var root = rootNodeOffset >= 0 ? rootNodeOffset : offset + HeaderSize;
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0), (uint)root);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4), -1); // free node list
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(12), (ushort)keyLength);
            buffer[14] = 0x60; // compact + compound
            buffer[15] = 1; // signature
            // 502: ascending order (zero); 506: FOR expression length (zero)
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(510), (ushort)(keyExpression.Length + 1));
            Encoding.ASCII.GetBytes(keyExpression).CopyTo(buffer, 512);

            stream.Seek(offset, SeekOrigin.Begin);
            stream.Write(buffer, 0, buffer.Length);

            if (stream.Length < endOffset) stream.SetLength(endOffset);
        }

        private static byte[] EncodeIntegerKey(int value)
        {
            var key = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(key, value);
            key[0] ^= 0x80;
            return key;
        }

        private static byte[] PadKey(string text, int length)
        {
            var key = new byte[length];
            for (var i = 0; i < length; i++)
            {
                key[i] = (byte)(i < text.Length ? text[i] : ' ');
            }

            return key;
        }

        private static int TrimmedLength(byte[] key)
        {
            var length = key.Length;
            while (length > 0 && key[length - 1] == 0x20) length--;
            return length;
        }

        private sealed class ByteArrayComparer : IComparer<byte[]>
        {
            public static readonly ByteArrayComparer Instance = new();

            public int Compare(byte[] x, byte[] y)
            {
                for (var i = 0; i < Math.Min(x!.Length, y!.Length); i++)
                {
                    var cmp = x[i].CompareTo(y[i]);
                    if (cmp != 0) return cmp;
                }

                return x.Length.CompareTo(y.Length);
            }
        }

        // sanity checks through the public API; throws when the generated files do not
        // behave identically with and without indexes
        public static void Verify(string directory, int rowCount)
        {
            using (var table = new DbfTable(DbfPath(directory)))
            {
                if (table.Header.RecordCount != rowCount)
                    throw new InvalidOperationException("record count mismatch");
            }

            var probes = new[]
            {
                $"select * from bench.dbf where ID = {rowCount / 2}",
                "select * from bench.dbf where CODE = 'C000123'",
                $"select ID from bench.dbf where ID between {rowCount / 4} and {rowCount / 4 + 99}",
                "select top 10 ID from bench.dbf order by ID desc",
                "select count(*) from bench.dbf"
            };

            foreach (var sql in probes)
            {
                var indexed = Run(directory, sql, useIndexes: true);
                var scanned = Run(directory, sql, useIndexes: false);

                if (!indexed.SequenceEqual(scanned))
                    throw new InvalidOperationException($"index/scan mismatch for: {sql}");
                if (indexed.Count == 0)
                    throw new InvalidOperationException($"no rows for: {sql}");
            }
        }

        private static List<string> Run(string directory, string sql, bool useIndexes)
        {
            using var connection = new DbfDbConnection();
            connection.ConnectionString =
                $"Folder={directory};SkipDeletedRecords=false;UseIndexes={useIndexes}";
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = sql;

            var rows = new List<string>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var values = new object[reader.FieldCount];
                reader.GetValues(values);
                rows.Add(string.Join("|", values));
            }

            return rows;
        }
    }
}
