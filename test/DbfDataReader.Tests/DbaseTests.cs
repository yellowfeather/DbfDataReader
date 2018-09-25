using CsvHelper;
using Shouldly;
using System;
using System.IO;

namespace DbfDataReader.Tests
{
    public abstract class DbaseTests : IDisposable
    {
        protected DbaseTests(string fixturePath)
        {
            FixturePath = fixturePath;
            DbfTable = new DbfTable(fixturePath, EncodingProvider.GetEncoding(1252));
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
            using (var stream = new FileStream(path, FileMode.Open))
            using (var summaryFile = new StreamReader(stream))
            {
                var line = summaryFile.ReadLine();
                while (line != null && !line.StartsWith("---"))
                {
                    line = summaryFile.ReadLine();
                }

                foreach (var dbfColumn in DbfTable.Columns)
                {
                    line = summaryFile.ReadLine();
                    ValidateColumn(dbfColumn, line);
                }
            }
        }

        protected void ValidateColumn(DbfColumn dbfColumn, string line)
        {
            var expectedName = line.Substring(0, 16).Trim();
            var expectedColumnType = (DbfColumnType)line.Substring(17, 1)[0];
            var expectedLength = int.Parse(line.Substring(28, 10));
            var expectedDecimalCount = int.Parse(line.Substring(39));

            dbfColumn.Name.ShouldBe(expectedName);
            dbfColumn.ColumnType.ShouldBe(expectedColumnType);
            dbfColumn.Length.ShouldBe(expectedLength);
            dbfColumn.DecimalCount.ShouldBe(expectedDecimalCount);
        }

        protected void ValidateRowValues(string path)
        {
            var dbfRecord = new DbfRecord(DbfTable);

            using (var textReader = File.OpenText(path))
            using (var csvParser = new CsvParser(textReader))
            {
                csvParser.Read();

                var row = 1;
                while (DbfTable.Read(dbfRecord))
                {
                    var csvValues = csvParser.Read();

                    var index = 0;
                    foreach (var dbfValue in dbfRecord.Values)
                    {
                        var value = dbfValue.ToString();
                        var csvValue = csvValues[index];
                        value.ShouldBe(csvValue, $"Row: {row}, column: {index} ({DbfTable.Columns[index].Name})", StringCompareShould.IgnoreLineEndings);

                        index++;
                    }

                    row++;
                }
            }
        }
    }
}