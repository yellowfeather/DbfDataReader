using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests;

// Column-subset parsing (issue #296): queries parse only the columns they reference,
// in two phases (filter columns for every candidate row, the rest only for matching
// rows). These tests pin the guard against exposing stale values and prove, against
// full-parse oracles, that every query path returns identical results.
public class QueryColumnSubsetTests
{
    private const string FixtureFolder = "../../../../fixtures";
    private const string Dbase03Path = FixtureFolder + "/dbase_03.dbf";

    private const int PointIdOrdinal = 0; // Point_ID C(12)
    private const int TypeOrdinal = 1; // Type C(20)
    private const int DateVisitOrdinal = 8; // Date_Visit D
    private const int MaxPdopOrdinal = 10; // Max_PDOP N

    // --- record-level guards ---------------------------------------------------

    [Fact]
    public void Unparsed_ordinals_throw_instead_of_exposing_stale_values()
    {
        using var table = new DbfTable(Dbase03Path);
        var record = new DbfRecord(table);
        record.EnableSubsetParsing();

        table.ReadRaw(record).ShouldBeTrue();
        record.TryParseValues(new[] { PointIdOrdinal, MaxPdopOrdinal }).ShouldBeTrue();

        record.GetValue(PointIdOrdinal).ShouldNotBeNull();
        record.GetValue(MaxPdopOrdinal).ShouldNotBeNull();

        Should.Throw<InvalidOperationException>(() => record.GetValue(TypeOrdinal));
        Should.Throw<InvalidOperationException>(() => record.IsNull(TypeOrdinal));
        Should.Throw<InvalidOperationException>(() => record.GetStringValue(TypeOrdinal));
        Should.Throw<InvalidOperationException>(() => record.GetValue<string>(TypeOrdinal));
    }

    [Fact]
    public void Parsing_is_additive_within_a_row()
    {
        using var table = new DbfTable(Dbase03Path);
        var record = new DbfRecord(table);
        record.EnableSubsetParsing();

        table.ReadRaw(record).ShouldBeTrue();

        record.TryParseValues(new[] { PointIdOrdinal }).ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => record.GetValue(TypeOrdinal));

        record.TryParseValues(new[] { TypeOrdinal }).ShouldBeTrue();
        record.GetValue(PointIdOrdinal).ShouldNotBeNull();
        record.GetValue(TypeOrdinal).ShouldNotBeNull();
    }

    [Fact]
    public void Values_parsed_for_a_previous_row_are_guarded_after_advancing()
    {
        using var table = new DbfTable(Dbase03Path);
        var record = new DbfRecord(table);
        record.EnableSubsetParsing();

        table.ReadRaw(record).ShouldBeTrue();
        record.TryParseValues(new[] { PointIdOrdinal }).ShouldBeTrue();
        var firstRowValue = record.GetValue(PointIdOrdinal);

        table.ReadRaw(record).ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => record.GetValue(PointIdOrdinal));

        record.TryParseValues(new[] { PointIdOrdinal }).ShouldBeTrue();
        record.GetValue(PointIdOrdinal).ShouldNotBe(firstRowValue);
    }

    [Fact]
    public void A_full_read_marks_every_column_parsed()
    {
        using var table = new DbfTable(Dbase03Path);
        var record = new DbfRecord(table);
        record.EnableSubsetParsing();

        table.Read(record).ShouldBeTrue();

        for (var ordinal = 0; ordinal < table.Columns.Count; ordinal++)
        {
            record.GetValue(ordinal); // must not throw
        }
    }

    [Fact]
    public void Subset_parsing_requires_enabling_first()
    {
        using var table = new DbfTable(Dbase03Path);
        var record = new DbfRecord(table);

        table.ReadRaw(record).ShouldBeTrue();

        Should.Throw<InvalidOperationException>(() => record.TryParseValues(new[] { PointIdOrdinal }));
    }

    // --- SQL paths against a full-parse oracle ----------------------------------

    private sealed record OracleRow(object[] Values, bool IsDeleted);

    private static List<OracleRow> ReadOracle(string path)
    {
        var rows = new List<OracleRow>();

        using var table = new DbfTable(path);
        var record = new DbfRecord(table);
        while (table.Read(record))
        {
            var values = new object[table.Columns.Count];
            for (var ordinal = 0; ordinal < values.Length; ordinal++)
            {
                values[ordinal] = record.GetValue(ordinal);
            }

            rows.Add(new OracleRow(values, record.IsDeleted));
        }

        return rows;
    }

    // the median Max_PDOP guarantees the filter both matches and rejects rows,
    // so both parse phases are exercised
    private static decimal SelectiveThreshold(List<OracleRow> oracle)
    {
        var values = oracle.Select(r => (decimal)r.Values[MaxPdopOrdinal]).OrderBy(v => v).ToList();
        return values[values.Count / 2];
    }

    private static DbfDbConnection OpenConnection()
    {
        var connection = new DbfDbConnection();
        connection.ConnectionString = $"Folder={FixtureFolder};SkipDeletedRecords=false";
        connection.Open();
        return connection;
    }

    private static DbfDbCommand CreateCommand(DbfDbConnection connection, string commandText,
        params (string Name, object Value)[] parameters)
    {
        var command = (DbfDbCommand)connection.CreateCommand();
        command.CommandText = commandText;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return command;
    }

    [Fact]
    public void Projection_and_filter_on_disjoint_columns_match_the_oracle()
    {
        var oracle = ReadOracle(Dbase03Path);
        var threshold = SelectiveThreshold(oracle);
        var expected = oracle
            .Where(r => (decimal)r.Values[MaxPdopOrdinal] > threshold)
            .Select(r => (string)r.Values[PointIdOrdinal])
            .ToList();
        expected.ShouldNotBeEmpty();
        expected.Count.ShouldBeLessThan(oracle.Count);

        using var connection = OpenConnection();
        var command = CreateCommand(connection,
            "select Point_ID from dbase_03 where Max_PDOP > @pdop", ("pdop", threshold));

        var actual = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            actual.Add(reader.GetString(0));
        }

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Select_star_with_filter_parses_every_projected_column()
    {
        var oracle = ReadOracle(Dbase03Path);
        var threshold = SelectiveThreshold(oracle);
        var expected = oracle
            .Where(r => (decimal)r.Values[MaxPdopOrdinal] > threshold)
            .Select(r => r.Values)
            .ToList();

        using var connection = OpenConnection();
        var command = CreateCommand(connection,
            "select * from dbase_03 where Max_PDOP > @pdop", ("pdop", threshold));

        var actual = new List<object[]>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            actual.Add(values);
        }

        actual.Count.ShouldBe(expected.Count);
        for (var row = 0; row < actual.Count; row++)
        {
            actual[row].ShouldBe(expected[row]);
        }
    }

    [Fact]
    public void Ordering_by_a_column_outside_the_projection_matches_the_oracle()
    {
        var oracle = ReadOracle(Dbase03Path);
        var expected = oracle
            .OrderByDescending(r => (decimal)r.Values[MaxPdopOrdinal])
            .Select(r => (string)r.Values[PointIdOrdinal])
            .ToList();

        using var connection = OpenConnection();
        var command = CreateCommand(connection, "select Point_ID from dbase_03 order by Max_PDOP desc");

        var actual = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            actual.Add(reader.GetString(0));
        }

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Filter_ordering_and_limit_combine_with_subset_parsing()
    {
        var oracle = ReadOracle(Dbase03Path);
        var threshold = SelectiveThreshold(oracle);
        var expected = oracle
            .Where(r => (decimal)r.Values[MaxPdopOrdinal] >= threshold)
            .OrderBy(r => (DateTime?)r.Values[DateVisitOrdinal])
            .Select(r => (string)r.Values[PointIdOrdinal])
            .Take(3)
            .ToList();

        using var connection = OpenConnection();
        var command = CreateCommand(connection,
            "select Point_ID from dbase_03 where Max_PDOP >= @pdop order by Date_Visit limit 3",
            ("pdop", threshold));

        var actual = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            actual.Add(reader.GetString(0));
        }

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Count_with_filter_parses_only_the_filter_columns()
    {
        var oracle = ReadOracle(Dbase03Path);
        var threshold = SelectiveThreshold(oracle);
        var expected = oracle.Count(r => (decimal)r.Values[MaxPdopOrdinal] > threshold);

        using var connection = OpenConnection();
        var command = CreateCommand(connection,
            "select count(*) from dbase_03 where Max_PDOP > @pdop", ("pdop", threshold));

        command.ExecuteScalar().ShouldBe(expected);
    }

    [Fact]
    public async Task Async_reads_return_the_same_rows()
    {
        var oracle = ReadOracle(Dbase03Path);
        var threshold = SelectiveThreshold(oracle);
        var expected = oracle
            .Where(r => (decimal)r.Values[MaxPdopOrdinal] > threshold)
            .Select(r => (string)r.Values[PointIdOrdinal])
            .ToList();

        using var connection = OpenConnection();
        var command = CreateCommand(connection,
            "select Point_ID from dbase_03 where Max_PDOP > @pdop", ("pdop", threshold));

        var actual = new List<string>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            actual.Add(reader.GetString(0));
        }

        actual.ShouldBe(expected);
    }

    [Fact]
    public async Task Async_sorted_reads_return_the_same_rows()
    {
        var oracle = ReadOracle(Dbase03Path);
        var expected = oracle
            .OrderByDescending(r => (decimal)r.Values[MaxPdopOrdinal])
            .Select(r => (string)r.Values[PointIdOrdinal])
            .ToList();

        using var connection = OpenConnection();
        var command = CreateCommand(connection, "select Point_ID from dbase_03 order by Max_PDOP desc");

        var actual = new List<string>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            actual.Add(reader.GetString(0));
        }

        actual.ShouldBe(expected);
    }

    // --- memo tables: unreferenced memo columns are never parsed ----------------

    [Fact]
    public void Queries_on_memo_tables_skip_the_memo_column()
    {
        var oracle = ReadOracle(FixtureFolder + "/dbase_8b.dbf");
        var expected = oracle
            .Where(r => r.Values[1] is decimal value && value > 0m) // NUMERICAL
            .Select(r => (string)r.Values[0]) // CHARACTER
            .ToList();
        expected.ShouldNotBeEmpty();

        using var connection = OpenConnection();
        var command = CreateCommand(connection,
            "select CHARACTER from dbase_8b where NUMERICAL > 0");

        var actual = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            actual.Add(reader.GetString(0));
        }

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Select_star_on_memo_tables_still_returns_memo_content()
    {
        var oracle = ReadOracle(FixtureFolder + "/dbase_8b.dbf");
        var expected = oracle
            .Where(r => r.Values[1] is decimal value && value > 0m)
            .Select(r => r.Values)
            .ToList();

        using var connection = OpenConnection();
        var command = CreateCommand(connection, "select * from dbase_8b where NUMERICAL > 0");

        var actual = new List<object[]>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            actual.Add(values);
        }

        actual.Count.ShouldBe(expected.Count);
        for (var row = 0; row < actual.Count; row++)
        {
            actual[row].ShouldBe(expected[row]);
        }
    }

    // --- typed query builder -----------------------------------------------------

    private sealed class PdopPoint
    {
        public string Point_ID { get; set; }

        public decimal? Max_PDOP { get; set; }
    }

    [Fact]
    public void Typed_queries_parse_only_the_mapped_columns()
    {
        var oracle = ReadOracle(Dbase03Path).Where(r => !r.IsDeleted).ToList();
        var threshold = SelectiveThreshold(oracle);
        var expected = oracle
            .Where(r => (decimal)r.Values[MaxPdopOrdinal] > threshold)
            .Select(r => (string)r.Values[PointIdOrdinal])
            .ToList();
        expected.ShouldNotBeEmpty();

        using var table = new DbfTable(Dbase03Path);
        var actual = table.Query<PdopPoint>()
            .Where(p => p.Max_PDOP > threshold)
            .ToList();

        actual.Select(p => p.Point_ID).ShouldBe(expected);
        actual.All(p => p.Max_PDOP > threshold).ShouldBeTrue();
    }

    [Fact]
    public void Typed_queries_sort_and_limit_with_subset_parsing()
    {
        var oracle = ReadOracle(Dbase03Path).Where(r => !r.IsDeleted).ToList();
        var expected = oracle
            .OrderByDescending(r => (decimal)r.Values[MaxPdopOrdinal])
            .Select(r => (string)r.Values[PointIdOrdinal])
            .Take(5)
            .ToList();

        using var table = new DbfTable(Dbase03Path);
        var actual = table.Query<PdopPoint>()
            .OrderByDescending(p => p.Max_PDOP)
            .Take(5)
            .ToList();

        actual.Select(p => p.Point_ID).ShouldBe(expected);
    }

    [Fact]
    public async Task Typed_async_queries_return_the_same_rows()
    {
        var oracle = ReadOracle(Dbase03Path).Where(r => !r.IsDeleted).ToList();
        var threshold = SelectiveThreshold(oracle);
        var expected = oracle
            .Where(r => (decimal)r.Values[MaxPdopOrdinal] > threshold)
            .Select(r => (string)r.Values[PointIdOrdinal])
            .ToList();

        using var table = new DbfTable(Dbase03Path);
        var actual = await table.Query<PdopPoint>()
            .Where(p => p.Max_PDOP > threshold)
            .ToListAsync();

        actual.Select(p => p.Point_ID).ShouldBe(expected);
    }

    [Fact]
    public void Typed_count_parses_only_the_filter_columns()
    {
        var oracle = ReadOracle(Dbase03Path).Where(r => !r.IsDeleted).ToList();
        var threshold = SelectiveThreshold(oracle);
        var expected = oracle.Count(r => (decimal)r.Values[MaxPdopOrdinal] > threshold);

        using var table = new DbfTable(Dbase03Path);
        table.Query<PdopPoint>().Where(p => p.Max_PDOP > threshold).Count().ShouldBe(expected);
    }
}

// the foxprodb CDX fixtures are shared with the other index test suites, which
// serialize access through this collection
[Collection("foxprodb")]
public class QueryColumnSubsetIndexTests
{
    private const string FolderPath = "../../../../fixtures/foxprodb";

    private static DbfDbConnection OpenConnection(bool useIndexes)
    {
        var connection = new DbfDbConnection();
        connection.ConnectionString = $"Folder={FolderPath};SkipDeletedRecords=false;UseIndexes={useIndexes}";
        connection.Open();
        return connection;
    }

    private static List<string> QueryRows(bool useIndexes, string commandText)
    {
        using var connection = OpenConnection(useIndexes);
        var command = (DbfDbCommand)connection.CreateCommand();
        command.CommandText = commandText;

        var rows = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            rows.Add(string.Join("|", values));
        }

        return rows;
    }

    [Theory]
    [InlineData("select KEY_NAME from setup where KEY_NAME = 'CONTACTS'")]
    [InlineData("select * from setup where KEY_NAME = 'CONTACTS'")]
    [InlineData("select * from setup where KEY_NAME between 'CALLS' and 'CONTACTS'")]
    public void Index_and_scan_paths_return_identical_rows_with_subset_parsing(string commandText)
    {
        using var connection = OpenConnection(useIndexes: true);
        var command = (DbfDbCommand)connection.CreateCommand();
        command.CommandText = commandText;
        command.ExplainPlan().ShouldContain("index");

        var indexed = QueryRows(useIndexes: true, commandText);
        var scanned = QueryRows(useIndexes: false, commandText);

        indexed.ShouldBe(scanned);
        indexed.ShouldNotBeEmpty();
    }
}
