using System;
using System.IO;
using Shouldly;
using Xunit;

namespace DbfReader.Tests
{
    public class Dbase3Tests : IDisposable
    {
        private const string FixturePath = "./test/fixtures/dbase_03.dbf";

        public Dbase3Tests()
        {
            DbfTable = new DbfTable(FixturePath);
        }

        public void Dispose()
        {
            DbfTable.Dispose();
        }

        public DbfTable DbfTable { get; }

        public DbfHeader DbfHeader => DbfTable.Header;

        [Fact]
        public void Should_report_correct_record_count()
        {
            DbfHeader.RecordCount.ShouldBe(14);
        }

        [Fact]
        public void Should_report_correct_version_number()
        {
            DbfHeader.Version.ShouldBe(0x03);
        }

        [Fact]
        public void Should_report_that_the_file_is_not_foxpro()
        {
            DbfHeader.IsFoxPro.ShouldBeFalse();
        }

        [Fact]
        public void Should_have_the_correct_number_of_columns()
        {
            DbfTable.Columns.Count.ShouldBe(31);
        }

        [Fact]
        public void Should_have_the_correct_column_schema()
        {
            ValidateColumn(DbfTable.Columns[0],  "Point_ID",     DbfColumnType.Character, 12, 0);
            ValidateColumn(DbfTable.Columns[1],  "Type",         DbfColumnType.Character, 20, 0);
            ValidateColumn(DbfTable.Columns[2],  "Shape",        DbfColumnType.Character, 20, 0);
            ValidateColumn(DbfTable.Columns[3],  "Circular_D",   DbfColumnType.Character, 20, 0);         
            ValidateColumn(DbfTable.Columns[4],  "Non_circul",   DbfColumnType.Character, 60, 0);         
            ValidateColumn(DbfTable.Columns[5],  "Flow_prese",   DbfColumnType.Character, 20, 0);         
            ValidateColumn(DbfTable.Columns[6],  "Condition",    DbfColumnType.Character, 20, 0);         
            ValidateColumn(DbfTable.Columns[7],  "Comments",     DbfColumnType.Character, 60, 0);         
            ValidateColumn(DbfTable.Columns[8],  "Date_Visit",   DbfColumnType.Date,       8, 0);         
            ValidateColumn(DbfTable.Columns[9],  "Time",         DbfColumnType.Character, 10, 0);         
            ValidateColumn(DbfTable.Columns[10], "Max_PDOP",     DbfColumnType.Number,     5, 1);         
            ValidateColumn(DbfTable.Columns[11], "Max_HDOP",     DbfColumnType.Number,     5, 1);         
            ValidateColumn(DbfTable.Columns[12], "Corr_Type",    DbfColumnType.Character, 36, 0);         
            ValidateColumn(DbfTable.Columns[13], "Rcvr_Type",    DbfColumnType.Character, 36, 0);         
            ValidateColumn(DbfTable.Columns[14], "GPS_Date",     DbfColumnType.Date,       8, 0);         
            ValidateColumn(DbfTable.Columns[15], "GPS_Time",     DbfColumnType.Character, 10, 0);         
            ValidateColumn(DbfTable.Columns[16], "Update_Sta",   DbfColumnType.Character, 36, 0);         
            ValidateColumn(DbfTable.Columns[17], "Feat_Name",    DbfColumnType.Character, 20, 0);         
            ValidateColumn(DbfTable.Columns[18], "Datafile",     DbfColumnType.Character, 20, 0);         
            ValidateColumn(DbfTable.Columns[19], "Unfilt_Pos",   DbfColumnType.Number,    10, 0);         
            ValidateColumn(DbfTable.Columns[20], "Filt_Pos",     DbfColumnType.Number,    10, 0);         
            ValidateColumn(DbfTable.Columns[21], "Data_Dicti",   DbfColumnType.Character, 20, 0);         
            ValidateColumn(DbfTable.Columns[22], "GPS_Week",     DbfColumnType.Number,     6, 0);         
            ValidateColumn(DbfTable.Columns[23], "GPS_Second",   DbfColumnType.Number,    12, 3);         
            ValidateColumn(DbfTable.Columns[24], "GPS_Height",   DbfColumnType.Number,    16, 3);         
            ValidateColumn(DbfTable.Columns[25], "Vert_Prec",    DbfColumnType.Number,    16, 1);         
            ValidateColumn(DbfTable.Columns[26], "Horz_Prec",    DbfColumnType.Number,    16, 1);         
            ValidateColumn(DbfTable.Columns[27], "Std_Dev",      DbfColumnType.Number,    16, 6);         
            ValidateColumn(DbfTable.Columns[28], "Northing",     DbfColumnType.Number,    16, 3);         
            ValidateColumn(DbfTable.Columns[29], "Easting",      DbfColumnType.Number,    16, 3);         
            ValidateColumn(DbfTable.Columns[30], "Point_ID",     DbfColumnType.Number,     9, 0);         
        }

        [Fact]
        public void Should_have_correct_row_values()
        {
            using (var stream = new FileStream("./test/fixtures/dbase_03.csv", FileMode.Open))
            using (var csvFile = new StreamReader(stream)) {
                 // skip the header row
                csvFile.ReadLine();
                
                var dbfRecord = new DbfRecord(DbfTable);
                while (DbfTable.Read(dbfRecord))
                {
                    var csvLine = csvFile.ReadLine();
                    var csvValues = csvLine.Split(',');

                    var index = 0;
                    foreach (var dbfValue in dbfRecord.Values)
                    {
                        dbfValue.ToString().ShouldBe(csvValues[index++]);
                    }
                }
            }
        }

        public void ValidateColumn(DbfColumn dbfColumn, 
            string expectedName,
            DbfColumnType expectedColumnType,
            int expectedLength,
            int expectedDecimalCount)
        {
            dbfColumn.Name.ShouldBe(expectedName);
            dbfColumn.ColumnType.ShouldBe(expectedColumnType);
            dbfColumn.Length.ShouldBe(expectedLength);
            dbfColumn.DecimalCount.ShouldBe(expectedDecimalCount);
        }
    }
}
