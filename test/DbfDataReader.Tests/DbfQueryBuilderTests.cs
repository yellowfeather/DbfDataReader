using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests;

public class GpsPoint
{
    public string Point_ID { get; set; }
    public decimal? Max_PDOP { get; set; }
    public DateTime? Date_Visit { get; set; }
}

[Collection("dbase_03")]
public class DbfQueryBuilderTests
{
    private const string Dbase03FixturePath = "../../../../fixtures/dbase_03.dbf";

    private sealed record BaselineRow(string PointId, decimal MaxPdop, DateTime? DateVisit, bool IsDeleted);

    private static readonly IComparer<string> DialectStringComparer =
        Comparer<string>.Create((x, y) => string.CompareOrdinal(x?.TrimEnd(' '), y?.TrimEnd(' ')));

    private static List<BaselineRow> ReadBaseline()
    {
        var rows = new List<BaselineRow>();

        using var dbfTable = new DbfTable(Dbase03FixturePath);
        var dbfRecord = new DbfRecord(dbfTable);

        while (dbfTable.Read(dbfRecord))
        {
            rows.Add(new BaselineRow(
                dbfRecord.GetValue<string>(0),
                dbfRecord.GetValue<decimal>(10),
                (DateTime?)dbfRecord.GetValue(8),
                dbfRecord.IsDeleted));
        }

        return rows;
    }

    // the builder skips deleted records by default
    private static List<BaselineRow> ActiveBaseline() =>
        ReadBaseline().Where(r => !r.IsDeleted).ToList();

    [Fact]
    public void Should_read_all_active_rows()
    {
        var expected = ActiveBaseline().Select(r => r.PointId).ToList();

        using var dbfTable = new DbfTable(Dbase03FixturePath);
        var actual = dbfTable.Query<GpsPoint>().ToList();

        actual.Select(p => p.Point_ID).ShouldBe(expected);
    }

    [Fact]
    public void Should_include_deleted_rows_on_request()
    {
        using var dbfTable = new DbfTable(Dbase03FixturePath);

        dbfTable.Query<GpsPoint>().IncludeDeleted().Count().ShouldBe(ReadBaseline().Count);
        dbfTable.Query<GpsPoint>().Count().ShouldBe(ActiveBaseline().Count);
    }

    [Fact]
    public void Should_filter_with_comparisons_and_captured_variables()
    {
        var baseline = ActiveBaseline();
        var threshold = baseline[3].MaxPdop;
        var expected = baseline.Where(r => r.MaxPdop >= threshold).Select(r => r.PointId).ToList();

        using var dbfTable = new DbfTable(Dbase03FixturePath);
        var actual = dbfTable.Query<GpsPoint>()
            .Where(p => p.Max_PDOP >= threshold)
            .ToList();

        actual.Select(p => p.Point_ID).ShouldBe(expected);
    }

    [Fact]
    public void Should_combine_multiple_where_calls_with_and()
    {
        var baseline = ActiveBaseline();
        var threshold = baseline[3].MaxPdop;
        var first = baseline[0].PointId;
        var expected = baseline
            .Where(r => r.MaxPdop >= threshold)
            .Where(r => r.PointId != first)
            .Select(r => r.PointId).ToList();

        using var dbfTable = new DbfTable(Dbase03FixturePath);
        var actual = dbfTable.Query<GpsPoint>()
            .Where(p => p.Max_PDOP >= threshold)
            .Where(p => p.Point_ID != first)
            .ToList();

        actual.Select(p => p.Point_ID).ShouldBe(expected);
    }

    [Fact]
    public void Should_translate_starts_with_to_like()
    {
        var baseline = ActiveBaseline();
        var prefix = baseline[0].PointId.Substring(0, 2);
        var expected = baseline
            .Where(r => r.PointId != null && r.PointId.StartsWith(prefix, StringComparison.Ordinal))
            .Select(r => r.PointId).ToList();

        using var dbfTable = new DbfTable(Dbase03FixturePath);
        var actual = dbfTable.Query<GpsPoint>()
            .Where(p => p.Point_ID.StartsWith(prefix))
            .ToList();

        actual.Select(p => p.Point_ID).ShouldBe(expected);
    }

    [Fact]
    public void Should_translate_collection_contains_to_in()
    {
        var baseline = ActiveBaseline();
        var wanted = new[] { baseline[0].PointId, baseline[^1].PointId };
        var expected = baseline.Where(r => wanted.Contains(r.PointId)).Select(r => r.PointId).ToList();

        using var dbfTable = new DbfTable(Dbase03FixturePath);
        var actual = dbfTable.Query<GpsPoint>()
            .Where(p => wanted.Contains(p.Point_ID))
            .ToList();

        actual.Select(p => p.Point_ID).ShouldBe(expected);
    }

    [Fact]
    public void Should_translate_null_comparisons_to_is_null()
    {
        var baseline = ActiveBaseline();
        var expectedNull = baseline.Count(r => r.DateVisit == null);
        var expectedNotNull = baseline.Count(r => r.DateVisit != null);

        using var dbfTable = new DbfTable(Dbase03FixturePath);

        dbfTable.Query<GpsPoint>().Where(p => p.Date_Visit == null).Count().ShouldBe(expectedNull);
        dbfTable.Query<GpsPoint>().Where(p => p.Date_Visit != null).Count().ShouldBe(expectedNotNull);
    }

    [Fact]
    public void Should_order_with_multiple_keys_and_take()
    {
        var baseline = ActiveBaseline();
        var expected = baseline
            .OrderByDescending(r => r.MaxPdop)
            .ThenBy(r => r.PointId, DialectStringComparer)
            .Take(5)
            .Select(r => r.PointId).ToList();

        using var dbfTable = new DbfTable(Dbase03FixturePath);
        var actual = dbfTable.Query<GpsPoint>()
            .OrderByDescending(p => p.Max_PDOP)
            .OrderBy(p => p.Point_ID)
            .Take(5)
            .ToList();

        actual.Select(p => p.Point_ID).ShouldBe(expected);
    }

    [Fact]
    public void Should_take_without_ordering()
    {
        var expected = ActiveBaseline().Take(3).Select(r => r.PointId).ToList();

        using var dbfTable = new DbfTable(Dbase03FixturePath);
        var actual = dbfTable.Query<GpsPoint>().Take(3).ToList();

        actual.Select(p => p.Point_ID).ShouldBe(expected);
    }

    [Fact]
    public void Should_support_first_and_first_or_default()
    {
        var baseline = ActiveBaseline();

        using var dbfTable = new DbfTable(Dbase03FixturePath);

        dbfTable.Query<GpsPoint>().First().Point_ID.ShouldBe(baseline[0].PointId);
        dbfTable.Query<GpsPoint>().Where(p => p.Point_ID == "no-such-value").FirstOrDefault().ShouldBeNull();
        Should.Throw<InvalidOperationException>(() =>
            dbfTable.Query<GpsPoint>().Where(p => p.Point_ID == "no-such-value").First());
    }

    [Fact]
    public void Should_materialize_values_correctly()
    {
        var baseline = ActiveBaseline();

        using var dbfTable = new DbfTable(Dbase03FixturePath);
        var first = dbfTable.Query<GpsPoint>().First();

        first.Point_ID.ShouldBe(baseline[0].PointId);
        first.Max_PDOP.ShouldBe(baseline[0].MaxPdop);
        first.Date_Visit.ShouldBe(baseline[0].DateVisit);
    }

    [Fact]
    public async Task Should_execute_asynchronously()
    {
        var baseline = ActiveBaseline();
        var threshold = baseline[3].MaxPdop;
        var expected = baseline
            .Where(r => r.MaxPdop >= threshold)
            .OrderBy(r => r.MaxPdop)
            .Select(r => r.PointId).ToList();

        using var dbfTable = new DbfTable(Dbase03FixturePath);

        var fromToListAsync = await dbfTable.Query<GpsPoint>()
            .Where(p => p.Max_PDOP >= threshold)
            .OrderBy(p => p.Max_PDOP)
            .ToListAsync();
        fromToListAsync.Select(p => p.Point_ID).ShouldBe(expected);

        var fromAsyncEnumerable = new List<string>();
        await foreach (var point in dbfTable.Query<GpsPoint>()
                           .Where(p => p.Max_PDOP >= threshold)
                           .OrderBy(p => p.Max_PDOP)
                           .AsAsyncEnumerable())
        {
            fromAsyncEnumerable.Add(point.Point_ID);
        }

        fromAsyncEnumerable.ShouldBe(expected);
    }

    [Fact]
    public void Should_be_repeatable_across_enumerations()
    {
        using var dbfTable = new DbfTable(Dbase03FixturePath);
        var query = dbfTable.Query<GpsPoint>();

        var first = query.ToList();
        var second = query.ToList();

        second.Select(p => p.Point_ID).ShouldBe(first.Select(p => p.Point_ID));
    }

    [Fact]
    public void Should_throw_for_untranslatable_expressions()
    {
        using var dbfTable = new DbfTable(Dbase03FixturePath);

        Should.Throw<NotSupportedException>(() =>
                dbfTable.Query<GpsPoint>().Where(p => p.Point_ID.Trim() == "x").ToList())
            .Message.ShouldContain("cannot be translated");

        Should.Throw<NotSupportedException>(() =>
            dbfTable.Query<GpsPoint>().Where(p => p.Point_ID.StartsWith("100%")).ToList());
    }

    private class Unmappable
    {
        public string NoSuchColumn { get; set; }
    }

    [Fact]
    public void Should_throw_for_unmapped_properties()
    {
        using var dbfTable = new DbfTable(Dbase03FixturePath);

        Should.Throw<InvalidOperationException>(() => dbfTable.Query<Unmappable>().ToList())
            .Message.ShouldContain("NoSuchColumn");
    }
}
