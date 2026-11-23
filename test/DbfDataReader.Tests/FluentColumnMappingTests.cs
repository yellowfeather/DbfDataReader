using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests;

// a model with no mapping attributes, configured fluently
public class PlainCoverage
{
    public int Coverage { get; set; }
    public string Name { get; set; }
    public string Display { get; set; }
    public string Label => $"{Name}: {Coverage}";
}

[Collection("dbase_03")]
public class FluentColumnMappingTests
{
    private const string Dbase03FixturePath = "../../../../fixtures/dbase_03.dbf";

    private static void MapCoverage(DbfModelBuilder<PlainCoverage> map)
    {
        map.Property(r => r.Coverage).HasColumnName("Coverage %");
        map.Property(r => r.Name).HasColumnName("Name (x)");
        map.Ignore(r => r.Display);
    }

    private class AttributedPoint
    {
        [Column("No_Such")]
        public string Id { get; set; }

        [NotMapped]
        public decimal? Pdop { get; set; }

        public string Extra { get; set; }
    }

    private class TwoNames
    {
        public string First { get; set; }
        public string Second { get; set; }
    }

    [Fact]
    public void Should_map_configured_column_names()
    {
        using var dbfTable = new DbfTable(DbfTableBuilder.CreateCoverageTable());

        var rows = dbfTable.Query<PlainCoverage>(MapCoverage).ToList();

        rows.Select(r => r.Coverage).ShouldBe(new[] { 10, 75, 40, 100 });
        rows.Select(r => r.Name).ShouldBe(new[] { "alpha", "bravo", "charlie", "delta" });
        rows.ShouldAllBe(r => r.Display == null);
    }

    [Fact]
    public void Should_filter_order_and_count_on_configured_properties()
    {
        using var dbfTable = new DbfTable(DbfTableBuilder.CreateCoverageTable());

        var rows = dbfTable.Query<PlainCoverage>(MapCoverage)
            .Where(r => r.Coverage >= 40 && r.Name != "delta")
            .OrderByDescending(r => r.Coverage)
            .ToList();

        rows.Select(r => r.Name).ShouldBe(new[] { "bravo", "charlie" });

        dbfTable.Query<PlainCoverage>(MapCoverage).Where(r => r.Coverage > 10).Count().ShouldBe(3);
    }

    [Fact]
    public void Should_take_precedence_over_attributes()
    {
        using var dbfTable = new DbfTable(Dbase03FixturePath);

        var expected = dbfTable.Query<GpsPoint>().OrderBy(p => p.Point_ID).ToList();

        // Id: fluent name beats [Column]; Pdop: Property() maps a [NotMapped] property;
        // Extra: Ignore() removes a property that would otherwise need a column
        var actual = dbfTable.Query<AttributedPoint>(map =>
            {
                map.Property(p => p.Id).HasColumnName("Point_ID");
                map.Property(p => p.Pdop).HasColumnName("Max_PDOP");
                map.Ignore(p => p.Extra);
            })
            .OrderBy(p => p.Id)
            .ToList();

        expected.Count.ShouldBeGreaterThan(0);
        actual.Select(p => (p.Id, p.Pdop)).ShouldBe(expected.Select(p => (p.Point_ID, p.Max_PDOP)));
    }

    [Fact]
    public void Should_keep_attribute_names_for_properties_that_are_not_configured()
    {
        using var dbfTable = new DbfTable(DbfTableBuilder.CreateCoverageTable());

        // CoverageRow maps both columns with [Column]; only Display is configured here
        var rows = dbfTable.Query<CoverageRow>(map => map.Property(r => r.Display).HasColumnName("name (x)"))
            .ToList();

        rows.Select(r => r.Coverage).ShouldBe(new[] { 10, 75, 40, 100 });
        rows.Select(r => r.Display).ShouldBe(rows.Select(r => r.Name));
    }

    [Fact]
    public void Should_let_the_last_configuration_of_a_property_win()
    {
        using var dbfTable = new DbfTable(DbfTableBuilder.CreateCoverageTable());

        var rows = dbfTable.Query<PlainCoverage>(map =>
        {
            map.Ignore(r => r.Name);
            map.Property(r => r.Name).HasColumnName("No_Such").HasColumnName("Name (x)");
            map.Property(r => r.Coverage).HasColumnName("Coverage %");
            map.Property(r => r.Coverage); // selecting again keeps the configured name
            map.Property(r => r.Display).HasColumnName("Name (x)");
            map.Ignore(r => r.Display);
            map.Ignore(r => r.Label); // ignoring a read-only property is harmless
        }).ToList();

        rows.Select(r => r.Name).ShouldBe(new[] { "alpha", "bravo", "charlie", "delta" });
        rows.Select(r => r.Coverage).ShouldBe(new[] { 10, 75, 40, 100 });
        rows.ShouldAllBe(r => r.Display == null);
    }

    [Fact]
    public void Should_not_share_materializers_between_different_mappings()
    {
        using var dbfTable = new DbfTable(DbfTableBuilder.CreateCoverageTable());

        var first = dbfTable.Query<TwoNames>(map =>
        {
            map.Property(r => r.First).HasColumnName("Name (x)");
            map.Ignore(r => r.Second);
        }).ToList();

        var second = dbfTable.Query<TwoNames>(map =>
        {
            map.Property(r => r.Second).HasColumnName("Name (x)");
            map.Ignore(r => r.First);
        }).ToList();

        first.ShouldAllBe(r => r.First != null && r.Second == null);
        second.ShouldAllBe(r => r.Second != null && r.First == null);
    }

    [Fact]
    public void Should_throw_for_a_lambda_on_an_ignored_property()
    {
        using var dbfTable = new DbfTable(DbfTableBuilder.CreateCoverageTable());

        Should.Throw<InvalidOperationException>(() =>
                dbfTable.Query<PlainCoverage>(MapCoverage).Where(r => r.Display == "x").ToList())
            .Message.ShouldContain("not mapped to a column");
    }

    [Fact]
    public void Should_throw_when_the_configured_column_is_unknown()
    {
        using var dbfTable = new DbfTable(DbfTableBuilder.CreateCoverageTable());

        var message = Should.Throw<InvalidOperationException>(() =>
            dbfTable.Query<PlainCoverage>(map =>
            {
                MapCoverage(map);
                map.Property(r => r.Name).HasColumnName("No_Such");
            }).ToList()).Message;

        message.ShouldContain("'No_Such'");
        message.ShouldContain("PlainCoverage.Name");
    }

    [Fact]
    public void Should_reject_invalid_configuration()
    {
        using var dbfTable = new DbfTable(DbfTableBuilder.CreateCoverageTable());

        Should.Throw<ArgumentNullException>(() => dbfTable.Query<PlainCoverage>(null));

        // not a property of the row type
        Should.Throw<ArgumentException>(() =>
            dbfTable.Query<PlainCoverage>(map => map.Property(r => r.Name.Length)));
        Should.Throw<ArgumentException>(() =>
            dbfTable.Query<PlainCoverage>(map => map.Ignore(r => r.Name.ToUpper())));

        // read-only properties cannot be mapped
        Should.Throw<ArgumentException>(() =>
            dbfTable.Query<PlainCoverage>(map => map.Property(r => r.Label)));

        Should.Throw<ArgumentException>(() =>
            dbfTable.Query<PlainCoverage>(map => map.Property(r => r.Name).HasColumnName("")));
        Should.Throw<ArgumentException>(() =>
            dbfTable.Query<PlainCoverage>(map => map.Property(r => r.Name).HasColumnName(null)));
    }
}

[Collection("foxprodb")]
public class FluentColumnMappingIndexTests
{
    private const string FolderPath = "../../../../fixtures/foxprodb";

    private class PlainCall
    {
        public long? Id { get; set; }
        public long? ContactId { get; set; }
    }

    private static void MapCall(DbfModelBuilder<PlainCall> map)
    {
        map.Property(c => c.Id).HasColumnName("CALL_ID");
        map.Property(c => c.ContactId).HasColumnName("CONTACT_ID");
    }

    [Fact]
    public void Should_still_use_the_index_through_a_configured_property()
    {
        using var dbfTable = new DbfTable($"{FolderPath}/calls.dbf");

        var query = dbfTable.Query<PlainCall>(MapCall).Where(c => c.ContactId == 1);
        query.ExplainPlan().ShouldContain("index seek (=) on tag 'CONTACT_ID'");

        var indexed = query.ToList();
        var scanned = dbfTable.Query<PlainCall>(MapCall).Where(c => c.ContactId == 1).WithoutIndexes().ToList();

        indexed.Count.ShouldBeGreaterThan(0);
        indexed.Select(c => (c.Id, c.ContactId)).ShouldBe(scanned.Select(c => (c.Id, c.ContactId)));
    }
}
