using System.Collections.Generic;
using System.Linq;
using DbfDataReader.Cdx;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests;

// Differential harness for integer-keyed index tags, against calls.dbf/calls.CDX:
// CALL_ID (unique values 1..16) and CONTACT_ID (duplicated foreign keys). Every query
// runs through the index path and a forced full scan and must return identical rows.
[Collection("foxprodb")]
public class QueryNumericIndexTests
{
    private const string FolderPath = "../../../../fixtures/foxprodb";

    private static DbfDbConnection OpenConnection(bool useIndexes)
    {
        var connection = new DbfDbConnection();
        connection.ConnectionString = $"Folder={FolderPath};SkipDeletedRecords=false;UseIndexes={useIndexes}";
        connection.Open();
        return connection;
    }

    private static List<string> QueryRows(bool useIndexes, string commandText,
        (string Name, object Value)? parameter = null)
    {
        using var connection = OpenConnection(useIndexes);
        var command = (DbfDbCommand)connection.CreateCommand();
        command.CommandText = commandText;
        if (parameter != null) command.Parameters.AddWithValue(parameter.Value.Name, parameter.Value.Value);

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

    private static string Explain(string commandText, (string Name, object Value)? parameter = null)
    {
        using var connection = OpenConnection(useIndexes: true);
        var command = (DbfDbCommand)connection.CreateCommand();
        command.CommandText = commandText;
        if (parameter != null) command.Parameters.AddWithValue(parameter.Value.Name, parameter.Value.Value);

        return command.ExplainPlan();
    }

    private static void ShouldMatchScan(string commandText, string expectedPlanFragment, int? expectedRows = null,
        (string Name, object Value)? parameter = null)
    {
        Explain(commandText, parameter).ShouldContain(expectedPlanFragment);

        var indexed = QueryRows(useIndexes: true, commandText, parameter);
        var scanned = QueryRows(useIndexes: false, commandText, parameter);

        indexed.ShouldBe(scanned);
        if (expectedRows != null) indexed.Count.ShouldBe(expectedRows.Value);
    }

    [Fact]
    public void Fixture_keys_should_round_trip_through_the_integer_transform()
    {
        // decode every key in both integer tags and compare with the column value of
        // the record it points at - direct proof the transform matches what VFP wrote
        using var dbfTable = new DbfTable($"{FolderPath}/calls.dbf");
        var rows = new List<(long? CallId, long? ContactId)>();
        var record = new DbfRecord(dbfTable);
        while (dbfTable.Read(record)) rows.Add(((long?)record.GetValue(0), (long?)record.GetValue(1)));

        using var cdxFile = new CdxFile($"{FolderPath}/calls.CDX", dbfTable.CurrentEncoding);
        var verified = 0;

        foreach (var (tagName, select) in new (string, System.Func<(long? CallId, long? ContactId), long?>)[]
                 {
                     ("CALL_ID", row => row.CallId),
                     ("CONTACT_ID", row => row.ContactId)
                 })
        {
            foreach (var entry in cdxFile.GetIndex(tagName).EnumerateEntries())
            {
                entry.KeyBytes.Length.ShouldBe(4);
                var decoded = CdxKeyEncoder.DecodeInteger(entry.KeyBytes);
                decoded.ShouldBe((int)select(rows[entry.RecordIndex]).Value, $"tag {tagName}");
                verified++;
            }
        }

        verified.ShouldBe(32); // 16 records in each tag
    }

    [Theory]
    [InlineData("select CALL_ID, SUBJECT from calls.dbf where CALL_ID = 7", 1)]
    [InlineData("select CALL_ID from calls.dbf where CONTACT_ID = 1", 5)] // duplicate key run
    [InlineData("select CALL_ID from calls.dbf where CONTACT_ID = 99", 0)]
    [InlineData("select CALL_ID from calls.dbf where CALL_ID = 1.5", 0)] // non-integral: provably empty
    [InlineData("select CALL_ID from calls.dbf where CALL_ID = 5000000000", 0)] // beyond int range
    public void Should_seek_integer_equality_through_the_index(string commandText, int expectedRows)
    {
        ShouldMatchScan(commandText, "index seek (=)", expectedRows);
    }

    [Theory]
    [InlineData("select CALL_ID from calls.dbf where CALL_ID >= 10")]
    [InlineData("select CALL_ID from calls.dbf where CALL_ID > 10")]
    [InlineData("select CALL_ID from calls.dbf where CALL_ID <= 5")]
    [InlineData("select CALL_ID from calls.dbf where CALL_ID < 5")]
    [InlineData("select CALL_ID from calls.dbf where CALL_ID >= 9.5")] // non-integral bound adjusts
    [InlineData("select CALL_ID from calls.dbf where CALL_ID < 5.5")]
    [InlineData("select CALL_ID from calls.dbf where 10 <= CALL_ID")] // flipped operands
    public void Should_range_scan_integers_through_the_index(string commandText)
    {
        ShouldMatchScan(commandText, "index range scan");
    }

    [Fact]
    public void Should_range_scan_integer_between_through_the_index()
    {
        ShouldMatchScan("select CALL_ID from calls.dbf where CALL_ID between 4 and 9",
            "index range scan (between)", 6);
        ShouldMatchScan("select CALL_ID from calls.dbf where CALL_ID between 8.5 and 8.9",
            "index range scan (between)", 0); // no integer fits
    }

    [Fact]
    public void Should_seek_with_integer_parameters()
    {
        ShouldMatchScan("select CALL_ID from calls.dbf where CONTACT_ID = @id",
            "index seek (=)", parameter: ("@id", 2));
    }

    [Fact]
    public void Should_apply_residual_predicates_after_the_integer_index()
    {
        ShouldMatchScan("select CALL_ID from calls.dbf where CONTACT_ID = 1 and CALL_ID > 2",
            "index seek (=)");
    }

    [Fact]
    public void Should_satisfy_order_by_from_the_integer_index()
    {
        var commandText = "select CALL_ID from calls.dbf where CALL_ID >= 5 order by CALL_ID";

        var plan = Explain(commandText);
        plan.ShouldContain("index range scan");
        plan.ShouldNotContain("in-memory sort");

        QueryRows(useIndexes: true, commandText).ShouldBe(QueryRows(useIndexes: false, commandText));
    }

    [Fact]
    public void Should_order_by_a_duplicated_integer_key_stably()
    {
        // CONTACT_ID has duplicate runs; index order must equal the stable sort order
        var commandText = "select CALL_ID, CONTACT_ID from calls.dbf order by CONTACT_ID";

        Explain(commandText).ShouldContain("index order scan on tag 'CONTACT_ID'");

        QueryRows(useIndexes: true, commandText).ShouldBe(QueryRows(useIndexes: false, commandText));
    }

    public class CallRow
    {
        public long? CALL_ID { get; set; }
        public long? CONTACT_ID { get; set; }
    }

    [Fact]
    public void Should_use_the_integer_index_from_the_query_builder()
    {
        using var dbfTable = new DbfTable($"{FolderPath}/calls.dbf");

        var query = dbfTable.Query<CallRow>().Where(c => c.CONTACT_ID == 1);
        query.ExplainPlan().ShouldContain("index seek (=) on tag 'CONTACT_ID'");

        var indexed = query.ToList();
        var scanned = dbfTable.Query<CallRow>().Where(c => c.CONTACT_ID == 1).WithoutIndexes().ToList();

        indexed.Select(c => $"{c.CALL_ID}|{c.CONTACT_ID}")
            .ShouldBe(scanned.Select(c => $"{c.CALL_ID}|{c.CONTACT_ID}"));
        indexed.Count.ShouldBe(5);
    }
}
