using System;
using System.Data;
using System.Data.Common;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests
{
    [Collection("dbase_03")]
    public class DbfDataReaderTests
    {
        private const string FixturePath = "../../../../fixtures/dbase_03.dbf";
        private const string FixtureSummaryPath = "../../../../fixtures/dbase_03_summary.txt";

        [Fact]
        public void Should_resolve_names_to_ordinals()
        {
            // Test if GetOrdinal resolves names according to IDataRecord.GetOrdinal()
            // see https://learn.microsoft.com/en-us/dotnet/api/system.data.idatarecord.getordinal?view=net-7.0#system-data-idatarecord-getordinal(system-string)
            using (var dbfDataReader = new DbfDataReader(FixturePath))
            {
                dbfDataReader.GetOrdinal("Point_ID").ShouldBe(0);
                dbfDataReader.GetOrdinal("POINT_ID").ShouldBe(0);
                dbfDataReader.GetOrdinal("point_id").ShouldBe(0);
                dbfDataReader.GetOrdinal("Std_Dev").ShouldBe(27);
                dbfDataReader.GetOrdinal("STD_DEV").ShouldBe(27);
                dbfDataReader.GetOrdinal("std_dev").ShouldBe(27);
                Assert.Throws<IndexOutOfRangeException>(() => dbfDataReader.GetOrdinal("NO_SUCH_FIELD"));
            }
        }
        [Fact]
        public void Should_have_valid_first_row_values()
        {
            using (var dbfDataReader = new DbfDataReader(FixturePath))
            {
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
        }

        [Fact]
        public void Should_be_able_to_read_all_the_rows()
        {
            using (var dbfDataReader = new DbfDataReader(FixturePath))
            {
                var rowCount = 0;
                while (dbfDataReader.Read())
                {
                    rowCount++;

                    dbfDataReader.GetString(0);
                    dbfDataReader.GetDecimal(10);
                }

                rowCount.ShouldBe(14);
            }
        }

        [Fact]
        public void Should_skip_deleted_rows()
        {
            var options = new DbfDataReaderOptions
            {
                SkipDeletedRecords = true
            };
            using (var dbfDataReader = new DbfDataReader(FixturePath, options))
            {
                var rowCount = 0;
                while (dbfDataReader.Read())
                {
                    rowCount++;

                    dbfDataReader.GetString(0);
                    dbfDataReader.GetDecimal(10);
                }

                rowCount.ShouldBe(12);
            }
        }

        [Fact]
        public void Should_throw_exception_when_casting_to_wrong_type()
        {
            using (var dbfDataReader = new DbfDataReader(FixturePath))
            {
                dbfDataReader.Read();

                var exception = Should.Throw<InvalidCastException>(() => dbfDataReader.GetInt32(0));
                exception.Message.ShouldBe(
                    "Unable to cast object of type 'System.String' to type 'System.Int32' at ordinal '0'.");
            }
        }

        [Fact]
        public void Should_support_get_column_schema()
        {
            using (var dbfDataReader = new DbfDataReader(FixturePath))
            {
                dbfDataReader.CanGetColumnSchema().ShouldBeTrue();
            }
        }

        [Fact]
        public void Should_get_valid_column_schema()
        {
            using (var dbfDataReader = new DbfDataReader(FixturePath))
            {
                var columns = dbfDataReader.GetColumnSchema();
                columns.Count.ShouldBe(31);

                using (var dbColumns = columns.GetEnumerator())
                {
                    dbColumns.MoveNext();

                    foreach (var line in FixtureHelpers.GetFieldLines(FixtureSummaryPath))
                    {
                        var dbColumn = dbColumns.Current;
                        ValidateColumn(dbColumn, line);

                        dbColumns.MoveNext();
                    }
                }
            }
        }

        [Fact]
        public void Should_get_typed_values()
        {
            using (var dbfDataReader = new DbfDataReader(FixturePath))
            {
                var columns = dbfDataReader.GetColumnSchema();
                columns.Count.ShouldBe(31);

                for (var i = 0; i < dbfDataReader.FieldCount; i++)
                {
                    if (dbfDataReader.IsDBNull(i))
                    {
                        var value = dbfDataReader.GetValue(i);
                        value.ShouldBeNull();
                        continue;
                    }

                    var fieldType = dbfDataReader.GetFieldType(i);
                    var typeCode = Type.GetTypeCode(fieldType);
                    switch (typeCode)
                    {
                        case TypeCode.Boolean:
                            dbfDataReader.GetBoolean(i);
                            break;
                        case TypeCode.Int32:
                            dbfDataReader.GetInt32(i);
                            break;
                        case TypeCode.DateTime:
                            dbfDataReader.GetDateTime(i);
                            break;
                        case TypeCode.Single:
                            dbfDataReader.GetFloat(i);
                            break;
                        case TypeCode.Double:
                            dbfDataReader.GetDouble(i);
                            break;
                        case TypeCode.Decimal:
                            dbfDataReader.GetDecimal(i);
                            break;
                        case TypeCode.String:
                            dbfDataReader.GetString(i);
                            break;
                        default:
                            // no cheating
                            throw new NotSupportedException($"Unsupported field type: {fieldType} for column at index: {i}");
                    }
                }
            }
        }

        [Fact]
        public void Should_get_valid_schema_table()
        {
            using (var dbfDataReader = new DbfDataReader(FixturePath))
            {
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
        }

        private void ValidateRow(DataRow row, string line)
        {
            var expectedName = line.Substring(0, 16).Trim();
            var dbfColumnType = (DbfColumnType)line.Substring(17, 1)[0];
            var decimalLength = int.Parse(line.Substring(39, 1));

            var expectedDataType = GetDataType(dbfColumnType, decimalLength);

            row[SchemaTableColumn.ColumnName].ShouldBe(expectedName);
            row[SchemaTableColumn.DataType].ShouldBe(expectedDataType);
        }

        private static void ValidateColumn(DbColumn dbColumn, string line)
        {
            var expectedName = line.Substring(0, 16).Trim();
            var dbfColumnType = (DbfColumnType)line.Substring(17, 1)[0];
            var decimalLength = int.Parse(line.Substring(39, 1));

            var expectedDataType = GetDataType(dbfColumnType, decimalLength);

            dbColumn.ColumnName.ShouldBe(expectedName);
            dbColumn.DataType.ShouldBe(expectedDataType);
        }

        private static Type GetDataType(DbfColumnType dbfColumnType, int decimalLength)
        {
            switch (dbfColumnType)
            {
                case DbfColumnType.Number:
                    return decimalLength == 0 ? typeof(int) : typeof(decimal);
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