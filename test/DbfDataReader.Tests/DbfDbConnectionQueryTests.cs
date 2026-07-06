using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests;

[Collection("dbase_03")]
public class DbfDbConnectionQueryTests
{
    private const string FolderPath = "../../../../fixtures";

    private static DbfDbConnection OpenConnection()
    {
        var connection = new DbfDbConnection();
        connection.ConnectionString = $"Folder={FolderPath};SkipDeletedRecords=false";
        connection.Open();
        return connection;
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

    [Fact]
    public void Should_query_typed_rows()
    {
        var baseline = ReadBaseline();

        using var connection = OpenConnection();
        var points = connection.Query<GpsPoint>(
            "select Point_ID, Max_PDOP, Date_Visit from dbase_03.dbf");

        points.Count.ShouldBe(baseline.Count);
        points[0].Point_ID.ShouldBe(baseline[0].PointId);
        points[0].Max_PDOP.ShouldBe(baseline[0].MaxPdop);
    }

    [Fact]
    public void Should_query_with_anonymous_object_parameters()
    {
        var baseline = ReadBaseline();
        var target = baseline[4].PointId;
        var expected = baseline.Where(r => r.PointId == target).Select(r => r.PointId).ToList();

        using var connection = OpenConnection();
        var points = connection.Query<GpsPoint>(
            "select Point_ID, Max_PDOP, Date_Visit from dbase_03.dbf where Point_ID = @id",
            new { id = target });

        points.Select(p => p.Point_ID).ShouldBe(expected);
    }

    [Fact]
    public void Should_query_scalar_types()
    {
        var baseline = ReadBaseline();

        using var connection = OpenConnection();

        var ids = connection.Query<string>("select Point_ID from dbase_03.dbf");
        ids.ShouldBe(baseline.Select(r => r.PointId).ToList());

        var pdops = connection.Query<decimal>("select Max_PDOP from dbase_03.dbf");
        pdops.ShouldBe(baseline.Select(r => r.MaxPdop).ToList());
    }

    [Fact]
    public void Should_map_aliases_to_properties()
    {
        using var connection = OpenConnection();

        var rows = connection.Query<AliasedPoint>(
            "select Point_ID as Id, Max_PDOP as Pdop from dbase_03.dbf order by Pdop desc limit 1");

        rows.Count.ShouldBe(1);
        rows[0].Id.ShouldNotBeNullOrEmpty();
        rows[0].Pdop.ShouldBe(ReadBaseline().Max(r => r.MaxPdop));
    }

    [Fact]
    public void Should_query_first_or_default()
    {
        var baseline = ReadBaseline();

        using var connection = OpenConnection();

        var found = connection.QueryFirstOrDefault<GpsPoint>(
            "select Point_ID, Max_PDOP, Date_Visit from dbase_03.dbf where Point_ID = @id",
            new { id = baseline[2].PointId });
        found.ShouldNotBeNull();
        found.Point_ID.ShouldBe(baseline[2].PointId);

        var missing = connection.QueryFirstOrDefault<GpsPoint>(
            "select Point_ID, Max_PDOP, Date_Visit from dbase_03.dbf where Point_ID = 'no-such-value'");
        missing.ShouldBeNull();
    }

    [Fact]
    public async Task Should_query_asynchronously()
    {
        var baseline = ReadBaseline();
        var threshold = baseline[7].MaxPdop;
        var expected = baseline.Where(r => r.MaxPdop >= threshold).Select(r => r.PointId).ToList();

        using var connection = OpenConnection();
        var points = await connection.QueryAsync<GpsPoint>(
            "select Point_ID, Max_PDOP, Date_Visit from dbase_03.dbf where Max_PDOP >= @threshold",
            new { threshold });

        points.Select(p => p.Point_ID).ShouldBe(expected);
    }

    [Fact]
    public void Should_throw_when_a_property_has_no_matching_column()
    {
        using var connection = OpenConnection();

        Should.Throw<InvalidOperationException>(() =>
                connection.Query<GpsPoint>("select Point_ID from dbase_03.dbf"))
            .Message.ShouldContain("Max_PDOP");
    }

    private class AliasedPoint
    {
        public string Id { get; set; }
        public decimal? Pdop { get; set; }
    }
}
