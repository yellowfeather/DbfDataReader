using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.IO;
using CsvHelper;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests
{
    [Collection("dbase_03")]
    public class DbfDataReaderTests
    {
        private const string FixturePath = "../../../../fixtures/dbase_03.dbf";
        private const string FixtureCsvPath = "../../../../fixtures/dbase_03.csv";
        private const string FixtureSummaryPath = "../../../../fixtures/dbase_03_summary.txt";

        [Fact]
        public void Should_resolve_names_to_ordinals()
        {
            // Test if GetOrdinal resolves names according to IDataRecord.GetOrdinal()
            // see https://learn.microsoft.com/en-us/dotnet/api/system.data.idatarecord.getordinal?view=net-7.0#system-data-idatarecord-getordinal(system-string)
            using var dbfDataReader = new DbfDataReader(FixturePath);
            dbfDataReader.GetOrdinal("Point_ID").ShouldBe(0);
            dbfDataReader.GetOrdinal("POINT_ID").ShouldBe(0);
            dbfDataReader.GetOrdinal("point_id").ShouldBe(0);
            dbfDataReader.GetOrdinal("Std_Dev").ShouldBe(27);
            dbfDataReader.GetOrdinal("STD_DEV").ShouldBe(27);
            dbfDataReader.GetOrdinal("std_dev").ShouldBe(27);
            Assert.Throws<IndexOutOfRangeException>(() => dbfDataReader.GetOrdinal("NO_SUCH_FIELD"));
        }

        [Fact]
        public void Should_have_valid_first_row_values()
        {
            using var dbfDataReader = new DbfDataReader(FixturePath);
            dbfDataReader.Read().ShouldBeTrue();

            dbfDataReader.GetString(0).ShouldBe("0507121");
            dbfDataReader.GetString(1).ShouldBe("CMP");
            dbfDataReader.GetString(2).ShouldBe("circular");
            dbfDataReader.GetString(3).ShouldBe("12");
            dbfDataReader.GetString(4).ShouldBe(string.Empty);
            dbfDataReader.GetString(5).ShouldBe("no");
            dbfDataReader.GetString(6).ShouldBe("Good");
            dbfDataReader.GetString(7).ShouldBe(string.Empty);
            dbfDataReader.GetDateTime(8).ShouldBe(new DateTime(2005, 7, 12));
            dbfDataReader.GetString(9).ShouldBe("10:56:30am");
            dbfDataReader.GetDecimal(10).ShouldBe(5.2m);
            dbfDataReader.GetDecimal(11).ShouldBe(2.0m);
            dbfDataReader.GetString(12).ShouldBe("Postprocessed Code");
            dbfDataReader.GetString(13).ShouldBe("GeoXT");
            dbfDataReader.GetDateTime(14).ShouldBe(new DateTime(2005, 7, 12));
            dbfDataReader.GetString(15).ShouldBe("10:56:52am");
            dbfDataReader.GetString(16).ShouldBe("New");
            dbfDataReader.GetString(17).ShouldBe("Driveway");
            dbfDataReader.GetString(18).ShouldBe("050712TR2819.cor");
            dbfDataReader.GetInt64(19).ShouldBe(2);
            dbfDataReader.GetInt64(20).ShouldBe(2);
            dbfDataReader.GetString(21).ShouldBe("MS4");
            dbfDataReader.GetInt32(22).ShouldBe(1331);
            dbfDataReader.GetDecimal(23).ShouldBe(226625.000m);
            dbfDataReader.GetDecimal(24).ShouldBe(1131.323m);
            dbfDataReader.GetDecimal(25).ShouldBe(3.1m);
            dbfDataReader.GetDecimal(26).ShouldBe(1.3m);
            dbfDataReader.GetDecimal(27).ShouldBe(0.897088m);
            dbfDataReader.GetDecimal(28).ShouldBe(557904.898m);
            dbfDataReader.GetDecimal(29).ShouldBe(2212577.192m);
            dbfDataReader.GetInt32(30).ShouldBe(401);
        }

        [Fact]
        public void Should_be_able_to_read_all_the_rows()
        {
            using var dbfDataReader = new DbfDataReader(FixturePath);
            var rowCount = 0;
            while (dbfDataReader.Read())
            {
                rowCount++;

                _ = dbfDataReader.GetString(0);
                _ = dbfDataReader.GetDecimal(10);
            }

            rowCount.ShouldBe(14);
        }
        
        [Fact]
        public void Should_be_able_to_read_all_the_rows_using_enumerator()
        {
            const int fieldCount = 31;
            
            using var textReader = File.OpenText(FixtureCsvPath);
            using var csvParser = new CsvParser(textReader, CultureInfo.InvariantCulture);
            csvParser.Read();

            using var dbfDataReader = new DbfDataReader(FixturePath);
            
            var row = 1;
            
            // ReSharper disable once CollectionNeverUpdated.Local
            var values = new List<object>(fieldCount);
            
            // get an enumerator from DbfDataReader
            foreach (DbDataRecord record in dbfDataReader)
            {
                record.FieldCount.ShouldBe(fieldCount);
                csvParser.Read();

                var index = 0;

                record.GetValues(values.ToArray());
                foreach (var value in values)
                {
                    var v = value.ToString();
                    var csvValue = csvParser[index];
                    v.ShouldBe(csvValue, $"Row: {row}, column: {index} ({record.GetName(index)})", StringCompareShould.IgnoreLineEndings);

                    index++;
                }

                row++;
            }
        }

        [Fact]
        public void Should_skip_deleted_rows()
        {
            var options = new DbfDataReaderOptions
            {
                SkipDeletedRecords = true
            };
            
            using var dbfDataReader = new DbfDataReader(FixturePath, options);
            var rowCount = 0;
            while (dbfDataReader.Read())
            {
                rowCount++;

                _ = dbfDataReader.GetString(0);
                _ = dbfDataReader.GetDecimal(10);
            }

            rowCount.ShouldBe(12);
        }

        [Fact]
        public void Should_throw_exception_when_casting_to_wrong_type()
        {
            using var dbfDataReader = new DbfDataReader(FixturePath);
            dbfDataReader.Read();

            var exception = Should.Throw<InvalidCastException>(() => dbfDataReader.GetInt32(0));
            exception.Message.ShouldBe(
                "Unable to cast object of type 'System.String' to type 'System.Int32' at ordinal '0'.");
        }

        [Fact]
        public void Should_support_get_column_schema()
        {
            using var dbfDataReader = new DbfDataReader(FixturePath);
            dbfDataReader.CanGetColumnSchema().ShouldBeTrue();
        }

        [Fact]
        public void Should_get_valid_column_schema()
        {
            using var dbfDataReader = new DbfDataReader(FixturePath);
            var columns = dbfDataReader.GetColumnSchema();
            columns.Count.ShouldBe(31);

            using var dbColumns = columns.GetEnumerator();
            dbColumns.MoveNext();

            foreach (var line in FixtureHelpers.GetFieldLines(FixtureSummaryPath))
            {
                var dbColumn = dbColumns.Current;
                ValidateColumn(dbColumn, line);

                dbColumns.MoveNext();
            }
        }

        [Fact]
        public void Should_get_values()
        {
            using var dbfDataReader = new DbfDataReader(FixturePath);
            dbfDataReader.Read().ShouldBeTrue();

            // ReSharper disable once CollectionNeverUpdated.Local
            
            var values = new object[dbfDataReader.FieldCount];
            dbfDataReader.GetValues(values);
            
            values[0].ShouldBe("0507121");
            values[1].ShouldBe("CMP");
            values[2].ShouldBe("circular");
            values[3].ShouldBe("12");
            values[4].ShouldBe(string.Empty);
            values[5].ShouldBe("no");
            values[6].ShouldBe("Good");
            values[7].ShouldBe(string.Empty);
            values[8].ShouldBe(new DateTime(2005, 7, 12));
            values[9].ShouldBe("10:56:30am");
            values[10].ShouldBe(5.2m);
            values[11].ShouldBe(2.0m);
            values[12].ShouldBe("Postprocessed Code");
            values[13].ShouldBe("GeoXT");
            values[14].ShouldBe(new DateTime(2005, 7, 12));
            values[15].ShouldBe("10:56:52am");
            values[16].ShouldBe("New");
            values[17].ShouldBe("Driveway");
            values[18].ShouldBe("050712TR2819.cor");
            values[19].ShouldBe(2);
            values[20].ShouldBe(2);
            values[21].ShouldBe("MS4");
            values[22].ShouldBe(1331);
            values[23].ShouldBe(226625.000m);
            values[24].ShouldBe(1131.323m);
            values[25].ShouldBe(3.1m);
            values[26].ShouldBe(1.3m);
            values[27].ShouldBe(0.897088m);
            values[28].ShouldBe(557904.898m);
            values[29].ShouldBe(2212577.192m);
            values[30].ShouldBe(401);
        }

        [Fact]
        public void Should_get_typed_values()
        {
            using var dbfDataReader = new DbfDataReader(FixturePath);
            var columns = dbfDataReader.GetColumnSchema();
            columns.Count.ShouldBe(31);

            for (var i = 0; i < dbfDataReader.FieldCount; i++)
            {
                if (dbfDataReader.IsDBNull(i))
                {
                    var value = dbfDataReader.GetValue(i);
                    value.ShouldBeNull();
                    
                    // ReSharper disable once HeuristicUnreachableCode
                    continue;
                }

                var fieldType = dbfDataReader.GetFieldType(i);
                var typeCode = Type.GetTypeCode(fieldType);
                switch (typeCode)
                {
                    case TypeCode.Boolean:
                        _ = dbfDataReader.GetBoolean(i);
                        break;
                    case TypeCode.Int32:
                        _ = dbfDataReader.GetInt32(i);
                        break;
                    case TypeCode.DateTime:
                        _ = dbfDataReader.GetDateTime(i);
                        break;
                    case TypeCode.Single:
                        _ = dbfDataReader.GetFloat(i);
                        break;
                    case TypeCode.Double:
                        _ = dbfDataReader.GetDouble(i);
                        break;
                    case TypeCode.Decimal:
                        _ = dbfDataReader.GetDecimal(i);
                        break;
                    case TypeCode.String:
                        _ = dbfDataReader.GetString(i);
                        break;
                    default:
                        // no cheating
                        throw new NotSupportedException($"Unsupported field type: {fieldType} for column at index: {i}");
                }
            }
        }

        [Fact]
        public void Should_get_valid_schema_table()
        {
            using var dbfDataReader = new DbfDataReader(FixturePath);
            var schemaTable = dbfDataReader.GetSchemaTable();
            schemaTable.ShouldNotBeNull();
            schemaTable.Rows.Count.ShouldBe(31);

            var index = 0;
            var rows = schemaTable.Rows;
            foreach (var line in FixtureHelpers.GetFieldLines(FixtureSummaryPath))
            {
                var row = rows[index++];
                ValidateRow(row, line);
            }
        }
        
        [Fact]
        public void Should_get_valid_data_type_names()
        {
            using var dbfDataReader = new DbfDataReader(FixturePath);
            dbfDataReader.GetDataTypeName(0).ShouldBe("Character");
            dbfDataReader.GetDataTypeName(1).ShouldBe("Character");
            dbfDataReader.GetDataTypeName(2).ShouldBe("Character");
            dbfDataReader.GetDataTypeName(3).ShouldBe("Character");
            dbfDataReader.GetDataTypeName(4).ShouldBe("Character");
            dbfDataReader.GetDataTypeName(5).ShouldBe("Character");
            dbfDataReader.GetDataTypeName(6).ShouldBe("Character");
            dbfDataReader.GetDataTypeName(7).ShouldBe("Character");
            dbfDataReader.GetDataTypeName(8).ShouldBe("Date");
            dbfDataReader.GetDataTypeName(9).ShouldBe("Character");
            dbfDataReader.GetDataTypeName(10).ShouldBe("Number");
            dbfDataReader.GetDataTypeName(11).ShouldBe("Number");
            dbfDataReader.GetDataTypeName(12).ShouldBe("Character");
            dbfDataReader.GetDataTypeName(13).ShouldBe("Character");
            dbfDataReader.GetDataTypeName(14).ShouldBe("Date");
            dbfDataReader.GetDataTypeName(15).ShouldBe("Character");
            dbfDataReader.GetDataTypeName(16).ShouldBe("Character");
            dbfDataReader.GetDataTypeName(17).ShouldBe("Character");
            dbfDataReader.GetDataTypeName(18).ShouldBe("Character");
            dbfDataReader.GetDataTypeName(19).ShouldBe("Number");
            dbfDataReader.GetDataTypeName(20).ShouldBe("Number");
            dbfDataReader.GetDataTypeName(21).ShouldBe("Character");
            dbfDataReader.GetDataTypeName(22).ShouldBe("Number");
            dbfDataReader.GetDataTypeName(23).ShouldBe("Number");
            dbfDataReader.GetDataTypeName(24).ShouldBe("Number");
            dbfDataReader.GetDataTypeName(25).ShouldBe("Number");
            dbfDataReader.GetDataTypeName(26).ShouldBe("Number");
            dbfDataReader.GetDataTypeName(27).ShouldBe("Number");
            dbfDataReader.GetDataTypeName(28).ShouldBe("Number");
            dbfDataReader.GetDataTypeName(29).ShouldBe("Number");
            dbfDataReader.GetDataTypeName(30).ShouldBe("Number");
        }

        private void ValidateRow(DataRow row, string line)
        {
            var expectedName = line.Substring(0, 16).Trim();
            var dbfColumnType = (DbfColumnType)line.Substring(17, 1)[0];
            var length = int.Parse(line.Substring(28, 8).Trim());
            var decimalCount = int.Parse(line.Substring(39, 1));

            var expectedDataType = GetDataType(dbfColumnType, length, decimalCount);

            row[SchemaTableColumn.ColumnName].ShouldBe(expectedName);
            row[SchemaTableColumn.DataType].ShouldBe(expectedDataType);
        }

        private static void ValidateColumn(DbColumn dbColumn, string line)
        {
            var expectedName = line.Substring(0, 16).Trim();
            var dbfColumnType = (DbfColumnType)line.Substring(17, 1)[0];
            var length = int.Parse(line.Substring(28, 8).Trim());
            var decimalCount = int.Parse(line.Substring(39, 1));

            var expectedDataType = GetDataType(dbfColumnType, length, decimalCount);

            dbColumn.ColumnName.ShouldBe(expectedName);
            dbColumn.DataType.ShouldBe(expectedDataType);
        }

        private static Type GetDataType(DbfColumnType dbfColumnType, int length, int decimalCount)
        {
            switch (dbfColumnType)
            {
                case DbfColumnType.Number:
                    if (decimalCount == 0) {
                        if (length < 10) {
                            return typeof(int);
                        }
                        else {
                            return typeof(long);
                        }
                    } else {
                        return typeof(decimal);
                    }
                case DbfColumnType.SignedLong:
                    return typeof(long);
                case DbfColumnType.Float:
                    return typeof(float);
                case DbfColumnType.Currency:
                    return typeof(decimal);
                case DbfColumnType.Date:
                    return typeof(DateTime);
                case DbfColumnType.DateTime:
                    return typeof(DateTime);
                case DbfColumnType.Boolean:
                    return typeof(bool);
                case DbfColumnType.Memo:
                    return typeof(string);
                case DbfColumnType.Double:
                    return typeof(double);
                case DbfColumnType.General:
                case DbfColumnType.Character:
                    return typeof(string);
                default:
                    throw new ArgumentOutOfRangeException(nameof(dbfColumnType), dbfColumnType, null);
            }
        }
    }
}