using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests
{
    [Collection("dbase_03")]
    public class AsyncReadTests
    {
        private const string Dbase03FixturePath = "../../../../fixtures/dbase_03.dbf";
        private const string Dbase30FixturePath = "../../../../fixtures/dbase_30.dbf";
        private const int Dbase03RecordCount = 14;

        [Fact]
        public async Task Should_read_the_same_values_asynchronously_as_synchronously()
        {
            var expectedRows = ReadAllRowsAsStrings(Dbase03FixturePath);

            var rows = new List<string>();
            using var dbfTable = new DbfTable(Dbase03FixturePath);
            var dbfRecord = new DbfRecord(dbfTable);

            while (await dbfTable.ReadAsync(dbfRecord))
            {
                rows.Add(JoinValues(dbfRecord));
            }

            rows.ShouldBe(expectedRows);
        }

        [Fact]
        public async Task Should_read_memo_values_asynchronously()
        {
            var expectedRows = ReadAllRowsAsStrings(Dbase30FixturePath);

            var rows = new List<string>();
            using var dbfTable = new DbfTable(Dbase30FixturePath);
            var dbfRecord = new DbfRecord(dbfTable);

            while (await dbfTable.ReadAsync(dbfRecord))
            {
                rows.Add(JoinValues(dbfRecord));
            }

            rows.ShouldBe(expectedRows);
        }

        [Fact]
        public async Task Should_track_record_index_when_reading_asynchronously()
        {
            using var dbfTable = new DbfTable(Dbase03FixturePath);
            var dbfRecord = new DbfRecord(dbfTable);

            dbfRecord.RecordIndex.ShouldBe(-1);

            var expectedRecordIndex = 0;
            while (await dbfTable.ReadAsync(dbfRecord))
            {
                dbfRecord.RecordIndex.ShouldBe(expectedRecordIndex);
                expectedRecordIndex++;
            }

            expectedRecordIndex.ShouldBe(Dbase03RecordCount);
        }

        [Fact]
        public async Task Should_seek_then_read_asynchronously()
        {
            var expectedRows = ReadAllRowsAsStrings(Dbase03FixturePath);

            using var dbfTable = new DbfTable(Dbase03FixturePath);
            var dbfRecord = new DbfRecord(dbfTable);

            dbfTable.Seek(7);

            (await dbfTable.ReadAsync(dbfRecord)).ShouldBeTrue();
            dbfRecord.RecordIndex.ShouldBe(7);
            JoinValues(dbfRecord).ShouldBe(expectedRows[7]);
        }

        [Fact]
        public async Task Should_read_records_asynchronously_with_read_record_async()
        {
            using var dbfTable = new DbfTable(Dbase03FixturePath);

            var count = 0;
            DbfRecord dbfRecord;
            while ((dbfRecord = await dbfTable.ReadRecordAsync()) != null)
            {
                dbfRecord.RecordIndex.ShouldBe(count);
                count++;
            }

            count.ShouldBe(Dbase03RecordCount);
        }

        [Fact]
        public async Task Should_skip_deleted_records_when_reading_asynchronously()
        {
            var options = new DbfDataReaderOptions
            {
                SkipDeletedRecords = true
            };

            using var dbfDataReader = new DbfDataReader(Dbase03FixturePath, options);
            var rowCount = 0;
            while (await dbfDataReader.ReadAsync())
            {
                rowCount++;

                _ = dbfDataReader.GetString(0);
                _ = dbfDataReader.GetDecimal(10);
            }

            rowCount.ShouldBe(12);
        }

        [Fact]
        public async Task Should_throw_when_reading_with_a_cancelled_token()
        {
            using var dbfDataReader = new DbfDataReader(Dbase03FixturePath);
            var cancellationToken = new CancellationToken(canceled: true);

            var exception = await Record.ExceptionAsync(() => dbfDataReader.ReadAsync(cancellationToken));

            exception.ShouldBeAssignableTo<OperationCanceledException>();
        }

        private static string JoinValues(DbfRecord dbfRecord)
        {
            return string.Join("|", dbfRecord.Values.Select(v => v.ToString()));
        }

        private static List<string> ReadAllRowsAsStrings(string path)
        {
            var rows = new List<string>();

            using var dbfTable = new DbfTable(path);
            var dbfRecord = new DbfRecord(dbfTable);

            while (dbfTable.Read(dbfRecord))
            {
                rows.Add(JoinValues(dbfRecord));
            }

            return rows;
        }
    }
}
