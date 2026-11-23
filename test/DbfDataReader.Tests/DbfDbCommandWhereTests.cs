using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests;

[Collection("dbase_03")]
public class DbfDbCommandWhereTests
{
    private const string FolderPath = "../../../../fixtures";

    // every SQL result is checked against an oracle: the same predicate applied in C#
    // (with the dialect's semantics) over a full scan of the fixture
    private sealed record BaselineRow(string PointId, decimal MaxPdop, DateTime? DateVisit);

    private static List<BaselineRow> ReadBaseline()
    {
        var rows = new List<BaselineRow>();

        using var dbfTable = new DbfTable($"{FolderPath}/dbase_03.dbf");
        var dbfRecord = new DbfRecord(dbfTable);

        while (dbfTable.Read(dbfRecord))
        {
            rows.Add(new BaselineRow(
                dbfRecord.GetValue<string>(0),
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

    private static List<string> QueryPointIds(string commandText, Action<DbfDbCommand> configure = null)
    {
        using var connection = OpenConnection();
        var command = (DbfDbCommand)connection.CreateCommand();
        command.CommandText = commandText;
        configure?.Invoke(command);

        var results = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(reader.GetString(0));
        }

        return results;
    }

    [Fact]
    public void Should_filter_by_string_equality()
    {
        var baseline = ReadBaseline();
        var target = baseline[3].PointId;
        var expected = baseline.Where(r => r.PointId == target).Select(r => r.PointId).ToList();

        var actual = QueryPointIds($"select Point_ID from dbase_03.dbf where Point_ID = '{target}'");

        actual.ShouldBe(expected);
        actual.ShouldNotBeEmpty();
    }

    [Fact]
    public void Should_ignore_trailing_spaces_in_string_equality()
    {
        var baseline = ReadBaseline();
        var target = baseline[3].PointId;

        var actual = QueryPointIds($"select Point_ID from dbase_03.dbf where Point_ID = '{target}   '");

        actual.ShouldContain(target);
    }

    [Fact]
    public void Should_filter_with_numeric_comparisons()
    {
        var baseline = ReadBaseline();
        var threshold = baseline[7].MaxPdop;
        var expected = baseline.Where(r => r.MaxPdop >= threshold).Select(r => r.PointId).ToList();

        var actual = QueryPointIds(
            $"select Point_ID from dbase_03.dbf where Max_PDOP >= {threshold.ToString(CultureInfo.InvariantCulture)}");

        actual.ShouldBe(expected);
        actual.Count.ShouldBeInRange(1, 13); // a threshold from the data always splits the rows
    }

    [Fact]
    public void Should_filter_with_between()
    {
        var baseline = ReadBaseline();
        var low = baseline.Min(r => r.MaxPdop);
        var high = baseline[7].MaxPdop;
        var expected = baseline.Where(r => r.MaxPdop >= low && r.MaxPdop <= high).Select(r => r.PointId).ToList();

        var actual = QueryPointIds(
            "select Point_ID from dbase_03.dbf where Max_PDOP between " +
            $"{low.ToString(CultureInfo.InvariantCulture)} and {high.ToString(CultureInfo.InvariantCulture)}");

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Should_filter_with_in_lists()
    {
        var baseline = ReadBaseline();
        var first = baseline.First().PointId;
        var last = baseline.Last().PointId;
        var expected = baseline
            .Where(r => r.PointId == first || r.PointId == last)
            .Select(r => r.PointId).ToList();

        var actual = QueryPointIds(
            $"select Point_ID from dbase_03.dbf where Point_ID in ('{first}', '{last}', 'no-such-value')");

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Should_filter_with_like_prefixes()
    {
        var baseline = ReadBaseline();
        var prefix = baseline[0].PointId.Substring(0, 2);
        var expected = baseline
            .Where(r => r.PointId != null && r.PointId.StartsWith(prefix, StringComparison.Ordinal))
            .Select(r => r.PointId).ToList();

        var actual = QueryPointIds($"select Point_ID from dbase_03.dbf where Point_ID like '{prefix}%'");

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Should_filter_by_date_comparison()
    {
        var baseline = ReadBaseline();
        var pivot = baseline.Where(r => r.DateVisit != null).Select(r => r.DateVisit.Value).OrderBy(d => d)
            .ElementAt(baseline.Count / 2);
        var expected = baseline
            .Where(r => r.DateVisit != null && r.DateVisit.Value >= pivot)
            .Select(r => r.PointId).ToList();

        var actual = QueryPointIds(
            $"select Point_ID from dbase_03.dbf where Date_Visit >= '{pivot:yyyy-MM-dd}'");

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Should_combine_predicates_with_and_or_not()
    {
        var baseline = ReadBaseline();
        var threshold = baseline[7].MaxPdop;
        var first = baseline.First().PointId;
        var expected = baseline
            .Where(r => (r.PointId == first || r.MaxPdop > threshold) && !(r.PointId == null))
            .Select(r => r.PointId).ToList();

        var actual = QueryPointIds(
            $"select Point_ID from dbase_03.dbf where (Point_ID = '{first}' or Max_PDOP > " +
            $"{threshold.ToString(CultureInfo.InvariantCulture)}) and not (Point_ID is null)");

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Should_partition_rows_between_is_null_and_is_not_null()
    {
        var nullCount = QueryPointIds("select Point_ID from dbase_03.dbf where Point_ID is null").Count;
        var notNullCount = QueryPointIds("select Point_ID from dbase_03.dbf where Point_ID is not null").Count;

        (nullCount + notNullCount).ShouldBe(14);
    }

    [Fact]
    public void Should_return_no_rows_when_comparing_with_null()
    {
        QueryPointIds("select Point_ID from dbase_03.dbf where Point_ID = null").ShouldBeEmpty();
        QueryPointIds("select Point_ID from dbase_03.dbf where not (Point_ID = null)").ShouldBeEmpty();
    }

    [Fact]
    public void Should_filter_select_star_queries()
    {
        using var connection = OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = "select * from dbase_03.dbf where Point_ID is null";

        using var reader = command.ExecuteReader();
        reader.FieldCount.ShouldBe(31);
        reader.Read().ShouldBeFalse();
    }

    [Fact]
    public void Should_apply_top_after_the_filter()
    {
        var baseline = ReadBaseline();
        var threshold = baseline[7].MaxPdop;
        var expected = baseline.Where(r => r.MaxPdop >= threshold).Take(2).Select(r => r.PointId).ToList();

        var actual = QueryPointIds(
            "select top 2 Point_ID from dbase_03.dbf where Max_PDOP >= " +
            threshold.ToString(CultureInfo.InvariantCulture));

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Should_filter_with_named_parameters()
    {
        var baseline = ReadBaseline();
        var target = baseline[5].PointId;
        var expected = baseline.Where(r => r.PointId == target).Select(r => r.PointId).ToList();

        var actual = QueryPointIds("select Point_ID from dbase_03.dbf where Point_ID = @id",
            command => command.Parameters.AddWithValue("@id", target));

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Should_filter_with_positional_parameters()
    {
        var baseline = ReadBaseline();
        var threshold = baseline[7].MaxPdop;
        var expected = baseline.Where(r => r.MaxPdop >= threshold).Select(r => r.PointId).ToList();

        var actual = QueryPointIds("select Point_ID from dbase_03.dbf where Max_PDOP >= ?",
            command => command.Parameters.AddWithValue("threshold", threshold));

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Should_throw_for_missing_parameters_when_reading()
    {
        using var connection = OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = "select Point_ID from dbase_03.dbf where Point_ID = @id";

        using var reader = command.ExecuteReader();
        var exception = Should.Throw<InvalidOperationException>(() => reader.Read());

        exception.Message.ShouldContain("Parameter '@id' was not supplied");
    }

    [Fact]
    public void Should_throw_for_type_mismatches_when_reading()
    {
        using var connection = OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = "select Point_ID from dbase_03.dbf where Point_ID > 5";

        using var reader = command.ExecuteReader();
        var exception = Should.Throw<InvalidOperationException>(() => reader.Read());

        exception.Message.ShouldContain("Cannot compare");
    }

    [Fact]
    public async Task Should_filter_asynchronously()
    {
        var baseline = ReadBaseline();
        var threshold = baseline[7].MaxPdop;
        var expected = baseline.Where(r => r.MaxPdop >= threshold).Select(r => r.PointId).ToList();

        using var connection = OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = "select Point_ID from dbase_03.dbf where Max_PDOP >= " +
                              threshold.ToString(CultureInfo.InvariantCulture);

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
    public void Should_execute_scalar()
    {
        var baseline = ReadBaseline();
        var target = baseline[2].PointId;

        using var connection = OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = $"select Point_ID from dbase_03.dbf where Point_ID = '{target}'";

        command.ExecuteScalar().ShouldBe(target);
    }

    [Fact]
    public void Should_execute_scalar_returning_null_for_no_rows()
    {
        using var connection = OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = "select Point_ID from dbase_03.dbf where Point_ID = 'no-such-value'";

        command.ExecuteScalar().ShouldBeNull();
    }
}
