using System;
using System.Text;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests
{
    [Collection("INNAKLKVT20180904")]
    public class DbfMemo1251Tests : IDisposable
    {
        private const string FixturePath = "../../../../fixtures/INNAKLKVT20180904.dbf";

        public DbfMemo1251Tests()
        {
            var options = new DbfDataReaderOptions
            {
                Encoding = Encoding.GetEncoding("WINDOWS-1251")
            };
            DbfDataReader = new DbfDataReader(FixturePath, options);
        }

        public void Dispose()
        {
            DbfDataReader.Dispose();
            DbfDataReader = null;
        }

        public DbfDataReader DbfDataReader { get; set; }

        [Fact(Skip = "WIP")]
        public void Should_be_able_to_read_all_the_rows()
        {
            var rowCount = 0;
            while (DbfDataReader.Read())
            {
                rowCount++;

                var valueCol1 = DbfDataReader.GetDateTime(0);
                var valueCol2 = DbfDataReader.GetString(1);

                var valueCol14 = DbfDataReader.GetString(13);
            }

            rowCount.ShouldBe(14);
        }
    }
}