using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using DbfDataReader.Query;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests;

[Collection("dbase_03")]
public class DbfQueryDataReaderTests
{
    private const string FolderPath = "../../../../fixtures";

    private static DbfDbConnection OpenConnection()
    {
        var connection = new DbfDbConnection();
        connection.ConnectionString = $"Folder={FolderPath};SkipDeletedRecords=false";
        connection.Open();
        return connection;
    }

    private static DbDataReader ExecuteReader(DbfDbConnection connection, string commandText)
    {
        var command = connection.CreateCommand();
        command.CommandText = commandText;
        return command.ExecuteReader();
    }

    [Fact]
    public void Should_project_columns()
    {
        var expected = ReadBaseline();

        using var connection = OpenConnection();
        using var reader = ExecuteReader(connection, "select Point_ID, Max_PDOP from dbase_03.dbf");

        reader.FieldCount.ShouldBe(2);
        reader.GetName(0).ShouldBe("Point_ID");
        reader.GetName(1).ShouldBe("Max_PDOP");

        var row = 0;
        while (reader.Read())
        {
            reader.GetString(0).ShouldBe(expected[row].PointId);
            reader.GetDecimal(1).ShouldBe(expected[row].MaxPdop);
            row++;
        }

        row.ShouldBe(14);
    }

    [Fact]
    public void Should_reorder_columns()
    {
        var expected = ReadBaseline();

        using var connection = OpenConnection();
        using var reader = ExecuteReader(connection, "select Max_PDOP, Point_ID from dbase_03.dbf");

        reader.Read().ShouldBeTrue();
        reader.GetDecimal(0).ShouldBe(expected[0].MaxPdop);
        reader.GetString(1).ShouldBe(expected[0].PointId);
    }

    [Fact]
    public void Should_apply_aliases()
    {
        using var connection = OpenConnection();
        using var reader = ExecuteReader(connection, "select Point_ID as id from dbase_03.dbf");

        reader.GetName(0).ShouldBe("id");
        reader.GetOrdinal("id").ShouldBe(0);
        reader.GetOrdinal("ID").ShouldBe(0); // case-insensitive fallback

        // the underlying name is replaced by the alias
        Should.Throw<IndexOutOfRangeException>(() => reader.GetOrdinal("Point_ID"));
    }

    [Fact]
    public void Should_allow_selecting_a_column_twice()
    {
        using var connection = OpenConnection();
        using var reader = ExecuteReader(connection, "select Point_ID, Point_ID as p2 from dbase_03.dbf");

        reader.Read().ShouldBeTrue();
        reader.GetString(1).ShouldBe(reader.GetString(0));
    }

    [Theory]
    [InlineData("select top 5 * from dbase_03.dbf")]
    [InlineData("select * from dbase_03.dbf limit 5")]
    public void Should_limit_rows(string commandText)
    {
        using var connection = OpenConnection();
        using var reader = ExecuteReader(connection, commandText);

        reader.FieldCount.ShouldBe(31);

        var rowCount = 0;
        while (reader.Read()) rowCount++;

        rowCount.ShouldBe(5);
    }

    [Fact]
    public void Should_return_all_rows_when_top_exceeds_the_record_count()
    {
        using var connection = OpenConnection();
        using var reader = ExecuteReader(connection, "select top 100 * from dbase_03.dbf");

        var rowCount = 0;
        while (reader.Read()) rowCount++;

        rowCount.ShouldBe(14);
    }

    [Fact]
    public void Should_return_no_rows_for_top_zero()
    {
        using var connection = OpenConnection();
        using var reader = ExecuteReader(connection, "select top 0 * from dbase_03.dbf");

        reader.HasRows.ShouldBeFalse();
        reader.Read().ShouldBeFalse();
    }

    [Fact]
    public void Should_combine_projection_and_top()
    {
        using var connection = OpenConnection();
        using var reader = ExecuteReader(connection, "select top 3 Point_ID from dbase_03.dbf");

        reader.FieldCount.ShouldBe(1);

        var rowCount = 0;
        while (reader.Read()) rowCount++;

        rowCount.ShouldBe(3);
    }

    [Fact]
    public async Task Should_read_projected_rows_asynchronously()
    {
        var expected = ReadBaseline();

        using var connection = OpenConnection();
        using var reader = ExecuteReader(connection, "select top 4 Point_ID from dbase_03.dbf");

        var values = new List<string>();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        values.Count.ShouldBe(4);
        values[3].ShouldBe(expected[3].PointId);
    }

    [Fact]
    public void Should_expose_the_projected_schema()
    {
        using var connection = OpenConnection();
        using var reader = ExecuteReader(connection, "select Max_PDOP as pdop, Point_ID from dbase_03.dbf");

        var schema = ((System.Data.Common.IDbColumnSchemaGenerator)reader).GetColumnSchema();
        schema.Count.ShouldBe(2);
        schema[0].ColumnName.ShouldBe("pdop");
        schema[0].ColumnOrdinal.ShouldBe(0);
        schema[0].DataType.ShouldBe(typeof(decimal));
        schema[0].BaseColumnName.ShouldBe("Max_PDOP");
        schema[1].ColumnName.ShouldBe("Point_ID");
        schema[1].DataType.ShouldBe(typeof(string));

        var schemaTable = reader.GetSchemaTable();
        schemaTable.ShouldNotBeNull();
        schemaTable.Rows.Count.ShouldBe(2);
        schemaTable.Rows[0]["ColumnName"].ShouldBe("pdop");

        reader.GetDataTypeName(0).ShouldBe("Number");
        reader.GetDataTypeName(1).ShouldBe("Character");
        reader.GetFieldType(1).ShouldBe(typeof(string));
    }

    [Fact]
    public void Should_keep_plain_select_all_on_the_raw_reader()
    {
        using var connection = OpenConnection();

        using var plainReader = ExecuteReader(connection, "select * from dbase_03.dbf");
        plainReader.ShouldBeOfType<DbfDataReader>();

        using var limitedReader = ExecuteReader(connection, "select top 5 * from dbase_03.dbf");
        limitedReader.ShouldNotBeOfType<DbfDataReader>();
    }

    [Fact]
    public void Should_throw_for_unknown_projected_columns()
    {
        using var connection = OpenConnection();

        var exception = Should.Throw<SqlParseException>(() =>
            ExecuteReader(connection, "select nope from dbase_03.dbf"));

        exception.Message.ShouldContain("Unknown column 'nope'");
    }

    [Fact]
    public void Should_get_values_for_projected_columns()
    {
        using var connection = OpenConnection();
        using var reader = ExecuteReader(connection, "select Point_ID, Max_PDOP from dbase_03.dbf");

        reader.Read().ShouldBeTrue();

        var values = new object[2];
        reader.GetValues(values).ShouldBe(2);
        values[0].ShouldBeOfType<string>();
        values[1].ShouldBeOfType<decimal>();
        reader["Point_ID"].ShouldBe(values[0]);
        reader[1].ShouldBe(values[1]);
    }

    private static List<(string PointId, decimal MaxPdop)> ReadBaseline()
    {
        var rows = new List<(string, decimal)>();

        using var dbfTable = new DbfTable($"{FolderPath}/dbase_03.dbf");
        var dbfRecord = new DbfRecord(dbfTable);

        while (dbfTable.Read(dbfRecord))
        {
            rows.Add((dbfRecord.GetValue<string>(0), dbfRecord.GetValue<decimal>(10)));
        }

        return rows;
    }
}
