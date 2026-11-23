using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests;

[Collection("dbase_03")]
public class DbfDbCommandOrderByTests
{
    private const string FolderPath = "../../../../fixtures";

    private sealed record BaselineRow(string PointId, string Type, decimal MaxPdop, DateTime? DateVisit);

    // the ordinal-ignoring-trailing-spaces comparer the dialect specifies, for LINQ oracles
    private static readonly IComparer<string> DialectStringComparer =
        Comparer<string>.Create((x, y) => string.CompareOrdinal(x?.TrimEnd(' '), y?.TrimEnd(' ')));

    private static List<BaselineRow> ReadBaseline()
    {
        var rows = new List<BaselineRow>();

        using var dbfTable = new DbfTable($"{FolderPath}/dbase_03.dbf");
        var dbfRecord = new DbfRecord(dbfTable);

        while (dbfTable.Read(dbfRecord))
        {
            rows.Add(new BaselineRow(
                dbfRecord.GetValue<string>(0),
                dbfRecord.GetValue<string>(1),
                dbfRecord.GetValue<decimal>(10),
                (DateTime?)dbfRecord.GetValue(8)));
        }

        return rows;
    }

    private static DbfDbConnection OpenConnection()
    {
        var connection = new DbfDbConnection();
        connection.ConnectionString = $"Folder={FolderPath};SkipDeletedRecords=false";
        connection.Open();
        return connection;
    }

    private static List<string> QueryFirstColumn(string commandText)
    {
        using var connection = OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = commandText;

        var results = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(reader.GetString(0));
        }

        return results;
    }

    [Fact]
    public void Should_order_by_a_string_column_ascending()
    {
        var expected = ReadBaseline().OrderBy(r => r.PointId, DialectStringComparer)
            .Select(r => r.PointId).ToList();

        var actual = QueryFirstColumn("select Point_ID from dbase_03.dbf order by Point_ID");

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Should_order_by_a_string_column_descending()
    {
        var expected = ReadBaseline().OrderByDescending(r => r.PointId, DialectStringComparer)
            .Select(r => r.PointId).ToList();

        var actual = QueryFirstColumn("select Point_ID from dbase_03.dbf order by Point_ID desc");

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Should_order_by_a_numeric_column()
    {
        var expected = ReadBaseline().OrderBy(r => r.MaxPdop).Select(r => r.PointId).ToList();

        var actual = QueryFirstColumn("select Point_ID from dbase_03.dbf order by Max_PDOP");

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Should_order_by_a_date_column()
    {
        // LINQ's OrderBy is stable and DateTime? sorts nulls first, matching the dialect
        var expected = ReadBaseline().OrderBy(r => r.DateVisit).Select(r => r.PointId).ToList();

        var actual = QueryFirstColumn("select Point_ID from dbase_03.dbf order by Date_Visit");

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Should_order_by_multiple_keys_stably()
    {
        // Type has duplicate values, so this exercises both the second key and stability
        var expected = ReadBaseline()
            .OrderBy(r => r.Type, DialectStringComparer)
            .ThenByDescending(r => r.MaxPdop)
            .Select(r => r.PointId).ToList();

        var actual = QueryFirstColumn("select Point_ID from dbase_03.dbf order by Type, Max_PDOP desc");

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Should_order_by_a_column_that_is_not_projected()
    {
        var expected = ReadBaseline().OrderBy(r => r.MaxPdop).Select(r => r.PointId).ToList();

        var actual = QueryFirstColumn("select Point_ID from dbase_03.dbf order by Max_PDOP asc");

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Should_order_by_a_select_list_alias()
    {
        var expected = ReadBaseline().OrderBy(r => r.PointId, DialectStringComparer)
            .Select(r => r.PointId).ToList();

        var actual = QueryFirstColumn("select Point_ID as id from dbase_03.dbf order by id");

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Should_apply_where_then_order_then_top()
    {
        var baseline = ReadBaseline();
        var threshold = baseline[7].MaxPdop;
        var expected = baseline
            .Where(r => r.MaxPdop >= threshold)
            .OrderByDescending(r => r.MaxPdop)
            .Take(3)
            .Select(r => r.PointId).ToList();

        var actual = QueryFirstColumn(
            "select top 3 Point_ID from dbase_03.dbf where Max_PDOP >= " +
            threshold.ToString(CultureInfo.InvariantCulture) + " order by Max_PDOP desc");

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Should_order_select_star_queries()
    {
        var expected = ReadBaseline().OrderByDescending(r => r.PointId, DialectStringComparer)
            .Select(r => r.PointId).ToList();

        using var connection = OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = "select * from dbase_03.dbf order by Point_ID desc";

        var actual = new List<string>();
        using var reader = command.ExecuteReader();
        reader.FieldCount.ShouldBe(31);
        while (reader.Read())
        {
            actual.Add(reader.GetString(0));
        }

        actual.ShouldBe(expected);
    }

    [Fact]
    public async Task Should_order_rows_asynchronously()
    {
        var expected = ReadBaseline().OrderBy(r => r.MaxPdop).Select(r => r.PointId).ToList();

        using var connection = OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = "select Point_ID, Max_PDOP from dbase_03.dbf order by Max_PDOP";

        var actual = new List<string>();
        var reader = await command.ExecuteReaderAsync();
        await using (reader.ConfigureAwait(false))
        {
            while (await reader.ReadAsync())
            {
                actual.Add(reader.GetString(0));
            }
        }

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Should_execute_scalar_on_ordered_queries()
    {
        var expected = ReadBaseline().OrderByDescending(r => r.MaxPdop).First().PointId;

        using var connection = OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = "select Point_ID from dbase_03.dbf order by Max_PDOP desc";

        command.ExecuteScalar().ShouldBe(expected);
    }

    [Fact]
    public void Should_read_typed_values_from_the_sort_buffer()
    {
        using var connection = OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = "select Point_ID, Max_PDOP, Date_Visit from dbase_03.dbf order by Max_PDOP desc";

        using var reader = command.ExecuteReader();
        reader.Read().ShouldBeTrue();

        var expected = ReadBaseline().OrderByDescending(r => r.MaxPdop).First();
        reader.GetString(0).ShouldBe(expected.PointId);
        reader.GetDecimal(1).ShouldBe(expected.MaxPdop);
        reader.IsDBNull(2).ShouldBe(expected.DateVisit == null);
        reader.GetValues(new object[3]).ShouldBe(3);
        reader["Max_PDOP"].ShouldBe(expected.MaxPdop);
    }
}
