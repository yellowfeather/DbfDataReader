using CsvHelper;
using Shouldly;
using System;
using System.Globalization;
using System.IO;

namespace DbfDataReader.Tests
{
    public abstract class DbaseTests : IDisposable
    {
        protected DbaseTests(string fixturePath, DbfDataReaderOptions options = null)
        {
            FixturePath = fixturePath;
            DbfTable = options == null 
                ? new DbfTable(fixturePath)
                : new DbfTable(fixturePath, options.Encoding, options.StringTrimming, options.ReadFloatsAsDecimals);
        }

        public void Dispose()
        {
            DbfTable.Dispose();
            DbfTable = null;
        }

        public string FixturePath { get; }

        public DbfTable DbfTable { get; protected set; }

        public DbfHeader DbfHeader => DbfTable.Header;

        protected void ValidateColumnSchema(string path)
        {
            using (var dbfColumns = DbfTable.Columns.GetEnumerator())
            {
                dbfColumns.MoveNext();

                foreach (var line in FixtureHelpers.GetFieldLines(path))
                {
                    var dbfColumn = dbfColumns.Current;
                    ValidateColumn(dbfColumn, line);

                    dbfColumns.MoveNext();
                }
            }
        }

        protected void ValidateColumn(DbfColumn dbfColumn, string line)
        {
            var expectedName = line.Substring(0, 16).Trim();
            var expectedColumnType = (DbfColumnType)line.Substring(17, 1)[0];
            var expectedLength = int.Parse(line.Substring(28, 10));
            var expectedDecimalCount = int.Parse(line.Substring(39));

            dbfColumn.ColumnName.ShouldBe(expectedName);
            dbfColumn.ColumnType.ShouldBe(expectedColumnType);
            dbfColumn.Length.ShouldBe(expectedLength);
            dbfColumn.DecimalCount.ShouldBe(expectedDecimalCount);
        }

        protected void ValidateRowValues(string path)
        {
            var dbfRecord = new DbfRecord(DbfTable);

            using (var textReader = File.OpenText(path))
            using (var csvParser = new CsvParser(textReader, CultureInfo.InvariantCulture))
            {
                csvParser.Read();

                var row = 1;
                while (DbfTable.Read(dbfRecord))
                {
                    csvParser.Read();

                    var index = 0;
                    foreach (var dbfValue in dbfRecord.Values)
                    {
                        var value = dbfValue.ToString();
                        var csvValue = csvParser[index];
                        value.ShouldBe(csvValue, $"Row: {row}, column: {index} ({DbfTable.Columns[index].ColumnName})", StringCompareShould.IgnoreLineEndings);

                        index++;
                    }

                    row++;
                }
            }
        }
    }
}