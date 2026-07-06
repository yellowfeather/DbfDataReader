using System;
using System.Collections.Generic;
using System.IO;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests
{
    [Collection("dbase_03")]
    public class SeekTests
    {
        private const string Dbase03FixturePath = "../../../../fixtures/dbase_03.dbf";
        private const int Dbase03RecordCount = 14;

        [Fact]
        public void Should_track_record_index_while_reading_sequentially()
        {
            using var dbfTable = new DbfTable(Dbase03FixturePath);
            var dbfRecord = new DbfRecord(dbfTable);

            dbfRecord.RecordIndex.ShouldBe(-1);

            var expectedRecordIndex = 0;
            while (dbfTable.Read(dbfRecord))
            {
                dbfRecord.RecordIndex.ShouldBe(expectedRecordIndex);
                expectedRecordIndex++;
            }

            expectedRecordIndex.ShouldBe(Dbase03RecordCount);
        }

        [Fact]
        public void Should_read_the_record_at_the_seeked_index()
        {
            var expectedValues = ReadFirstColumnValues();

            using var dbfTable = new DbfTable(Dbase03FixturePath);
            var dbfRecord = new DbfRecord(dbfTable);

            dbfTable.Seek(7);

            dbfTable.Read(dbfRecord).ShouldBeTrue();
            dbfRecord.RecordIndex.ShouldBe(7);
            dbfRecord.GetStringValue(0).ShouldBe(expectedValues[7]);
        }

        [Fact]
        public void Should_seek_back_to_the_first_record_after_reading_all_records()
        {
            var expectedValues = ReadFirstColumnValues();

            using var dbfTable = new DbfTable(Dbase03FixturePath);
            var dbfRecord = new DbfRecord(dbfTable);

            while (dbfTable.Read(dbfRecord))
            {
            }

            dbfTable.Seek(0);

            dbfTable.Read(dbfRecord).ShouldBeTrue();
            dbfRecord.RecordIndex.ShouldBe(0);
            dbfRecord.GetStringValue(0).ShouldBe(expectedValues[0]);
        }

        [Fact]
        public void Should_read_no_record_when_seeking_past_the_last_record()
        {
            using var dbfTable = new DbfTable(Dbase03FixturePath);
            var dbfRecord = new DbfRecord(dbfTable);

            dbfTable.Seek(Dbase03RecordCount + 100);

            dbfTable.Read(dbfRecord).ShouldBeFalse();
        }

        [Fact]
        public void Should_throw_when_seeking_to_a_negative_record_index()
        {
            using var dbfTable = new DbfTable(Dbase03FixturePath);

            Should.Throw<ArgumentOutOfRangeException>(() => dbfTable.Seek(-1));
        }

        [Fact]
        public void Should_throw_when_seeking_a_non_seekable_stream()
        {
            using var fileStream = File.OpenRead(Dbase03FixturePath);
            using var dbfTable = new DbfTable(new NonSeekableStream(fileStream));

            Should.Throw<NotSupportedException>(() => dbfTable.Seek(0));
        }

        [Fact]
        public void Should_seek_when_the_dbf_data_starts_at_a_non_zero_stream_offset()
        {
            var expectedValues = ReadFirstColumnValues();

            const int padding = 64;
            using var memoryStream = new MemoryStream();
            memoryStream.Write(new byte[padding], 0, padding);
            using (var fileStream = File.OpenRead(Dbase03FixturePath))
            {
                fileStream.CopyTo(memoryStream);
            }
            memoryStream.Position = padding;

            using var dbfTable = new DbfTable(memoryStream);
            var dbfRecord = new DbfRecord(dbfTable);

            dbfTable.Seek(2);

            dbfTable.Read(dbfRecord).ShouldBeTrue();
            dbfRecord.RecordIndex.ShouldBe(2);
            dbfRecord.GetStringValue(0).ShouldBe(expectedValues[2]);
        }

        [Fact]
        public void Should_seek_and_report_record_index_via_dbf_data_reader()
        {
            var expectedValues = ReadFirstColumnValues();

            using var dbfDataReader = new DbfDataReader(Dbase03FixturePath);

            dbfDataReader.Seek(5);

            dbfDataReader.Read().ShouldBeTrue();
            dbfDataReader.RecordIndex.ShouldBe(5);
            dbfDataReader.GetString(0).ShouldBe(expectedValues[5]);
        }

        private static List<string> ReadFirstColumnValues()
        {
            var values = new List<string>();

            using var dbfTable = new DbfTable(Dbase03FixturePath);
            var dbfRecord = new DbfRecord(dbfTable);

            while (dbfTable.Read(dbfRecord))
            {
                values.Add(dbfRecord.GetStringValue(0));
            }

            return values;
        }

        private sealed class NonSeekableStream : Stream
        {
            private readonly Stream _inner;

            public NonSeekableStream(Stream inner)
            {
                _inner = inner;
            }

            public override bool CanRead => _inner.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _inner.Length;

            public override long Position
            {
                get => _inner.Position;
                set => throw new NotSupportedException();
            }

            public override void Flush() => _inner.Flush();

            public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
