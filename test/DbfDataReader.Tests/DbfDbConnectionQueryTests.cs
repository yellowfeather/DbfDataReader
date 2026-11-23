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

    // a folder holding coverage.dbf, whose column names are not valid identifiers
    private sealed class CoverageFolder : IDisposable
    {
        public CoverageFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
            System.IO.Directory.CreateDirectory(Path);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(Path, "coverage.dbf"),
                DbfTableBuilder.Build(DbfTableBuilder.CoverageColumns, DbfTableBuilder.CoverageRows));
        }

        public string Path { get; }

        public DbfDbConnection Open()
        {
            var connection = new DbfDbConnection();
            connection.ConnectionString = $"Folder={Path}";
            connection.Open();
            return connection;
        }

        public void Dispose() => System.IO.Directory.Delete(Path, true);
    }

    [Fact]
    public void Should_map_select_star_through_column_attributes()
    {
        using var folder = new CoverageFolder();
        using var connection = folder.Open();

        var rows = connection.Query<CoverageRow>("select * from coverage.dbf");

        rows.Select(r => r.Coverage).ShouldBe(new[] { 10, 75, 40, 100 });
        rows.Select(r => r.Name).ShouldBe(new[] { "alpha", "bravo", "charlie", "delta" });
    }

    [Fact]
    public void Should_map_quoted_columns_through_column_attributes()
    {
        using var folder = new CoverageFolder();
        using var connection = folder.Open();

        var rows = connection.Query<CoverageRow>(
            "select [Coverage %], \"Name (x)\" from coverage.dbf where [Coverage %] >= @min order by [Coverage %]",
            new { min = 40 });

        rows.Select(r => r.Coverage).ShouldBe(new[] { 40, 75, 100 });
        rows.Select(r => r.Name).ShouldBe(new[] { "charlie", "bravo", "delta" });

        var first = connection.QueryFirstOrDefault<CoverageRow>(
            "select [Coverage %], [Name (x)] from coverage.dbf where [Name (x)] = 'delta'");
        first.Coverage.ShouldBe(100);
    }

    [Fact]
    public void Should_fall_back_to_the_property_name_for_aliased_columns()
    {
        using var folder = new CoverageFolder();
        using var connection = folder.Open();

        var rows = connection.Query<CoverageRow>(
            "select [Coverage %] as Coverage, [Name (x)] as Name from coverage.dbf");

        rows.Select(r => r.Coverage).ShouldBe(new[] { 10, 75, 40, 100 });
        rows.Select(r => r.Name).ShouldBe(new[] { "alpha", "bravo", "charlie", "delta" });
    }

    [Fact]
    public async Task Should_map_column_attributes_asynchronously()
    {
        using var folder = new CoverageFolder();
        using var connection = folder.Open();

        var rows = await connection.QueryAsync<CoverageRow>("select * from coverage.dbf");
        rows.Select(r => r.Coverage).ShouldBe(new[] { 10, 75, 40, 100 });

        var aliased = await connection.QueryAsync<CoverageRow>(
            "select [Coverage %] as Coverage, [Name (x)] as Name from coverage.dbf");
        aliased.Select(r => r.Name).ShouldBe(new[] { "alpha", "bravo", "charlie", "delta" });
    }

    [Fact]
    public void Should_name_the_column_and_property_when_an_attribute_column_is_missing()
    {
        using var folder = new CoverageFolder();
        using var connection = folder.Open();

        var message = Should.Throw<InvalidOperationException>(() =>
            connection.Query<CoverageRow>("select [Name (x)] from coverage.dbf")).Message;

        message.ShouldContain("'Coverage %'");
        message.ShouldContain("CoverageRow.Coverage");
    }

    private static void MapCoverage(DbfModelBuilder<PlainCoverage> map)
    {
        map.Property(r => r.Coverage).HasColumnName("Coverage %");
        map.Property(r => r.Name).HasColumnName("Name (x)");
        map.Ignore(r => r.Display);
    }

    [Fact]
    public void Should_map_with_a_registered_configuration()
    {
        using var folder = new CoverageFolder();
        using var connection = folder.Open();
        connection.Map<PlainCoverage>(MapCoverage);

        var rows = connection.Query<PlainCoverage>("select * from coverage.dbf");

        rows.Select(r => r.Coverage).ShouldBe(new[] { 10, 75, 40, 100 });
        rows.Select(r => r.Name).ShouldBe(new[] { "alpha", "bravo", "charlie", "delta" });
    }

    [Fact]
    public void Should_map_with_parameters_and_a_registered_configuration()
    {
        using var folder = new CoverageFolder();
        using var connection = folder.Open().Map<PlainCoverage>(MapCoverage);

        var rows = connection.Query<PlainCoverage>(
            "select * from coverage.dbf where [Coverage %] >= @min order by [Coverage %]",
            new { min = 40 });
        rows.Select(r => r.Name).ShouldBe(new[] { "charlie", "bravo", "delta" });

        var first = connection.QueryFirstOrDefault<PlainCoverage>(
            "select * from coverage.dbf where [Name (x)] = @name", new { name = "delta" });
        first.Coverage.ShouldBe(100);
    }

    [Fact]
    public void Should_fall_back_to_the_property_name_with_a_registered_configuration()
    {
        using var folder = new CoverageFolder();
        using var connection = folder.Open().Map<PlainCoverage>(MapCoverage);

        var rows = connection.Query<PlainCoverage>(
            "select [Coverage %] as Coverage, [Name (x)] as Name from coverage.dbf");

        rows.Select(r => r.Coverage).ShouldBe(new[] { 10, 75, 40, 100 });
        rows.Select(r => r.Name).ShouldBe(new[] { "alpha", "bravo", "charlie", "delta" });
    }

    [Fact]
    public async Task Should_map_with_a_registered_configuration_asynchronously()
    {
        using var folder = new CoverageFolder();
        using var connection = folder.Open().Map<PlainCoverage>(MapCoverage);

        var rows = await connection.QueryAsync<PlainCoverage>("select * from coverage.dbf");
        rows.Select(r => r.Coverage).ShouldBe(new[] { 10, 75, 40, 100 });

        var filtered = await connection.QueryAsync<PlainCoverage>(
            "select * from coverage.dbf where [Coverage %] > @min", new { min = 40 });
        filtered.Select(r => r.Name).ShouldBe(new[] { "bravo", "delta" });
    }

    [Fact]
    public void Should_replace_a_registered_configuration()
    {
        using var folder = new CoverageFolder();
        using var connection = folder.Open();

        connection.Map<PlainCoverage>(MapCoverage);
        connection.Query<PlainCoverage>("select * from coverage.dbf")[0].Display.ShouldBeNull();

        connection.Map<PlainCoverage>(map =>
        {
            MapCoverage(map);
            map.Property(r => r.Display).HasColumnName("Name (x)");
        });
        connection.Query<PlainCoverage>("select * from coverage.dbf")[0].Display.ShouldBe("alpha");
    }

    [Fact]
    public void Should_scope_a_registered_configuration_to_its_connection()
    {
        using var folder = new CoverageFolder();
        using var mapped = folder.Open().Map<PlainCoverage>(MapCoverage);
        using var unmapped = folder.Open();

        mapped.Query<PlainCoverage>("select * from coverage.dbf").Count.ShouldBe(4);

        // without the registration, PlainCoverage's property names match no column
        Should.Throw<InvalidOperationException>(() =>
            unmapped.Query<PlainCoverage>("select * from coverage.dbf"));
    }

    [Fact]
    public void Should_reject_a_null_configuration()
    {
        using var connection = new DbfDbConnection();

        Should.Throw<ArgumentNullException>(() => connection.Map<PlainCoverage>(null));
    }

    [Fact]
    public void Should_reject_a_configuration_for_scalar_types()
    {
        using var connection = new DbfDbConnection();

        Should.Throw<InvalidOperationException>(() => connection.Map<string>(map => { }))
            .Message.ShouldContain("scalar");
    }

    private class AliasedPoint
    {
        public string Id { get; set; }
        public decimal? Pdop { get; set; }
    }
}
