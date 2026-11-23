using System;
using System.IO;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests
{
    [Collection("dbase_03")]
    public class PartialReadStreamTests
    {
        [Fact]
        public void Should_read_from_a_stream_that_returns_partial_reads()
        {
            const string fixturePath = "../../../../fixtures/dbase_03.dbf";

            using var fileStream = File.OpenRead(fixturePath);
            using var dbfTable = new DbfTable(new OneBytePerReadStream(fileStream));

            dbfTable.Columns.Count.ShouldBe(31);

            var dbfRecord = new DbfRecord(dbfTable);
            var count = 0;
            while (dbfTable.Read(dbfRecord)) count++;

            count.ShouldBe(14);
        }

        private sealed class OneBytePerReadStream : Stream
        {
            private readonly Stream _inner;

            public OneBytePerReadStream(Stream inner)
            {
                _inner = inner;
            }

            public override bool CanRead => _inner.CanRead;
            public override bool CanSeek => _inner.CanSeek;
            public override bool CanWrite => false;
            public override long Length => _inner.Length;

            public override long Position
            {
                get => _inner.Position;
                set => _inner.Position = value;
            }

            public override void Flush() => _inner.Flush();

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _inner.Read(buffer, offset, Math.Min(1, count));
            }

            public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
