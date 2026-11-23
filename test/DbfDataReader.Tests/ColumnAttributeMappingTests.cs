using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests;

public class CoverageRow
{
    [Column("Coverage %")]
    public int Coverage { get; set; }

    [Column("Name (x)")]
    public string Name { get; set; }

    [NotMapped]
    public string Display { get; set; }
}

[Collection("dbase_03")]
public class ColumnAttributeMappingTests
{
    private const string Dbase03FixturePath = "../../../../fixtures/dbase_03.dbf";

    private class MappedPoint
    {
        [Column("Point_ID")]
        public string Id { get; set; }

        [Column("Max_PDOP")]
        public decimal? Pdop { get; set; }
    }

    private class CaseInsensitivePoint
    {
        [Column("point_id")]
        public string Id { get; set; }
    }

    private class UnnamedColumnPoint
    {
        [Column(Order = 1, TypeName = "varchar")]
        public string Point_ID { get; set; }
    }

    private class UnknownColumnPoint
    {
        [Column("No_Such")]
        public string Id { get; set; }
    }

    private class BasePoint
    {
        [Column("Point_ID")]
        public virtual string Id { get; set; }

        [NotMapped]
        public virtual string Ignored { get; set; }
    }

    private class DerivedPoint : BasePoint
    {
        public override string Id { get; set; }
        public override string Ignored { get; set; }
    }

    [Fact]
    public void Should_map_columns_whose_names_are_not_identifiers()
    {
        using var dbfTable = new DbfTable(DbfTableBuilder.CreateCoverageTable());

        var rows = dbfTable.Query<CoverageRow>().ToList();

        rows.Select(r => r.Coverage).ShouldBe(new[] { 10, 75, 40, 100 });
        rows.Select(r => r.Name).ShouldBe(new[] { "alpha", "bravo", "charlie", "delta" });
        rows.ShouldAllBe(r => r.Display == null);
    }

    [Fact]
    public void Should_filter_on_an_attribute_mapped_property()
    {
        using var dbfTable = new DbfTable(DbfTableBuilder.CreateCoverageTable());

        var rows = dbfTable.Query<CoverageRow>().Where(r => r.Coverage >= 40 && r.Name != "delta").ToList();

        rows.Select(r => r.Name).ShouldBe(new[] { "bravo", "charlie" });
    }

    [Fact]
    public void Should_order_by_an_attribute_mapped_property()
    {
        using var dbfTable = new DbfTable(DbfTableBuilder.CreateCoverageTable());

        var rows = dbfTable.Query<CoverageRow>().OrderByDescending(r => r.Coverage).ToList();

        rows.Select(r => r.Coverage).ShouldBe(new[] { 100, 75, 40, 10 });
    }

    [Fact]
    public void Should_count_with_an_attribute_mapped_filter()
    {
        using var dbfTable = new DbfTable(DbfTableBuilder.CreateCoverageTable());

        dbfTable.Query<CoverageRow>().Where(r => r.Coverage > 10).Count().ShouldBe(3);
    }

    [Fact]
    public void Should_match_the_property_name_mapping_on_a_fixture()
    {
        using var dbfTable = new DbfTable(Dbase03FixturePath);

        var expected = dbfTable.Query<GpsPoint>()
            .Where(p => p.Max_PDOP >= 2m)
            .OrderBy(p => p.Point_ID)
            .ToList();

        var actual = dbfTable.Query<MappedPoint>()
            .Where(p => p.Pdop >= 2m)
            .OrderBy(p => p.Id)
            .ToList();

        expected.Count.ShouldBeGreaterThan(0);
        actual.Select(p => (p.Id, p.Pdop)).ShouldBe(expected.Select(p => (p.Point_ID, p.Max_PDOP)));
    }

    [Fact]
    public void Should_match_the_attribute_name_case_insensitively()
    {
        using var dbfTable = new DbfTable(Dbase03FixturePath);

        var expected = dbfTable.Query<GpsPoint>().ToList().Select(p => p.Point_ID);

        dbfTable.Query<CaseInsensitivePoint>().ToList().Select(p => p.Id).ShouldBe(expected);
    }

    [Fact]
    public void Should_ignore_a_column_attribute_without_a_name()
    {
        using var dbfTable = new DbfTable(Dbase03FixturePath);

        var expected = dbfTable.Query<GpsPoint>().ToList().Select(p => p.Point_ID);

        dbfTable.Query<UnnamedColumnPoint>().ToList().Select(p => p.Point_ID).ShouldBe(expected);
    }

    [Fact]
    public void Should_read_inherited_attributes()
    {
        using var dbfTable = new DbfTable(Dbase03FixturePath);

        var expected = dbfTable.Query<GpsPoint>().ToList().Select(p => p.Point_ID);

        dbfTable.Query<DerivedPoint>().ToList().Select(p => p.Id).ShouldBe(expected);
    }

    [Fact]
    public void Should_throw_for_a_lambda_on_a_not_mapped_property()
    {
        using var dbfTable = new DbfTable(DbfTableBuilder.CreateCoverageTable());

        Should.Throw<InvalidOperationException>(() =>
                dbfTable.Query<CoverageRow>().Where(r => r.Display == "x").ToList())
            .Message.ShouldContain("not mapped to a column");
    }

    [Fact]
    public void Should_throw_when_the_attribute_names_an_unknown_column()
    {
        using var dbfTable = new DbfTable(Dbase03FixturePath);

        var message = Should.Throw<InvalidOperationException>(() =>
            dbfTable.Query<UnknownColumnPoint>().ToList()).Message;

        message.ShouldContain("'No_Such'");
        message.ShouldContain("UnknownColumnPoint.Id");
    }
}

[Collection("foxprodb")]
public class ColumnAttributeIndexTests
{
    private const string FolderPath = "../../../../fixtures/foxprodb";

    private class MappedCall
    {
        [Column("CALL_ID")]
        public long? Id { get; set; }

        [Column("CONTACT_ID")]
        public long? ContactId { get; set; }
    }

    [Fact]
    public void Should_still_use_the_index_through_an_attribute_mapped_property()
    {
        using var dbfTable = new DbfTable($"{FolderPath}/calls.dbf");

        var query = dbfTable.Query<MappedCall>().Where(c => c.ContactId == 1);
        query.ExplainPlan().ShouldContain("index seek (=) on tag 'CONTACT_ID'");

        var indexed = query.ToList();
        var scanned = dbfTable.Query<MappedCall>().Where(c => c.ContactId == 1).WithoutIndexes().ToList();

        indexed.Count.ShouldBeGreaterThan(0);
        indexed.Select(c => (c.Id, c.ContactId)).ShouldBe(scanned.Select(c => (c.Id, c.ContactId)));
    }
}
