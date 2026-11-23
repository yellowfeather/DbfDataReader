using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests;

// Descending ORDER BY served from ascending index tags by reversing the entry list;
// duplicate-key runs are re-sorted by record index so the result is identical to a
// stable in-memory descending sort. Every query is compared against a forced full scan.
[Collection("foxprodb")]
public class QueryDescendingOrderTests
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
        var command = connection.CreateCommand();
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

    private static string Explain(string commandText)
    {
        using var connection = OpenConnection(useIndexes: true);
        var command = (DbfDbCommand)connection.CreateCommand();
        command.CommandText = commandText;

        return command.ExplainPlan();
    }

    [Fact]
    public void Should_serve_descending_order_from_an_ascending_index()
    {
        var commandText = "select KEY_NAME, VALUE from setup.dbf order by KEY_NAME desc";

        var plan = Explain(commandText);
        plan.ShouldContain("index order scan (descending) on tag 'KEY_NAME'");
        plan.ShouldNotContain("in-memory sort");

        var indexed = QueryRows(useIndexes: true, commandText);
        indexed.ShouldBe(QueryRows(useIndexes: false, commandText));
        indexed.Count.ShouldBe(3);
    }

    [Fact]
    public void Should_keep_duplicate_keys_in_stable_order_when_descending()
    {
        // CONTACT_ID has runs of duplicate keys; a stable descending sort keeps each
        // run in ascending record order, and the reversed index must match exactly
        var commandText = "select CALL_ID, CONTACT_ID from calls.dbf order by CONTACT_ID desc";

        Explain(commandText).ShouldContain("index order scan (descending) on tag 'CONTACT_ID'");

        QueryRows(useIndexes: true, commandText).ShouldBe(QueryRows(useIndexes: false, commandText));
    }

    [Fact]
    public void Should_satisfy_descending_order_after_an_index_range()
    {
        var commandText = "select CALL_ID from calls.dbf where CALL_ID >= 5 order by CALL_ID desc";

        var plan = Explain(commandText);
        plan.ShouldContain("index range scan on tag 'CALL_ID'");
        plan.ShouldNotContain("in-memory sort");

        QueryRows(useIndexes: true, commandText).ShouldBe(QueryRows(useIndexes: false, commandText));
    }

    [Fact]
    public void Should_satisfy_descending_order_after_an_equality_seek()
    {
        var commandText = "select CALL_ID from calls.dbf where CONTACT_ID = 1 order by CONTACT_ID desc";

        var plan = Explain(commandText);
        plan.ShouldContain("index seek (=) on tag 'CONTACT_ID'");
        plan.ShouldNotContain("in-memory sort");

        QueryRows(useIndexes: true, commandText).ShouldBe(QueryRows(useIndexes: false, commandText));
    }

    [Fact]
    public void Should_apply_top_after_the_descending_index_order()
    {
        var commandText = "select CALL_ID from calls.dbf order by CALL_ID desc limit 3";

        Explain(commandText).ShouldContain("index order scan (descending)");

        var indexed = QueryRows(useIndexes: true, commandText);
        indexed.ShouldBe(QueryRows(useIndexes: false, commandText));
        indexed.ShouldBe(new List<string> { "16", "15", "14" });
    }

    [Fact]
    public void Should_still_sort_in_memory_when_the_order_column_has_no_tag()
    {
        var commandText = "select CALL_ID from calls.dbf order by SUBJECT desc";

        var plan = Explain(commandText);
        plan.ShouldStartWith("full scan");
        plan.ShouldContain("in-memory sort");

        QueryRows(useIndexes: true, commandText).ShouldBe(QueryRows(useIndexes: false, commandText));
    }

    public class CallRow
    {
        public long? CALL_ID { get; set; }
        public long? CONTACT_ID { get; set; }
    }

    [Fact]
    public void Builder_should_serve_descending_order_from_the_index()
    {
        using var dbfTable = new DbfTable($"{FolderPath}/calls.dbf");

        var query = dbfTable.Query<CallRow>().OrderByDescending(c => c.CONTACT_ID);
        var plan = query.ExplainPlan();
        plan.ShouldContain("index order scan (descending) on tag 'CONTACT_ID'");
        plan.ShouldNotContain("in-memory sort");

        var indexed = query.ToList().Select(c => $"{c.CALL_ID}|{c.CONTACT_ID}").ToList();
        var scanned = dbfTable.Query<CallRow>().OrderByDescending(c => c.CONTACT_ID).WithoutIndexes()
            .ToList().Select(c => $"{c.CALL_ID}|{c.CONTACT_ID}").ToList();

        indexed.ShouldBe(scanned);
    }

    [Fact]
    public void Builder_should_satisfy_descending_order_with_a_filter()
    {
        using var dbfTable = new DbfTable($"{FolderPath}/calls.dbf");

        var indexed = dbfTable.Query<CallRow>()
            .Where(c => c.CONTACT_ID == 1)
            .OrderByDescending(c => c.CONTACT_ID)
            .ToList().Select(c => c.CALL_ID).ToList();

        var scanned = dbfTable.Query<CallRow>()
            .Where(c => c.CONTACT_ID == 1)
            .OrderByDescending(c => c.CONTACT_ID)
            .WithoutIndexes()
            .ToList().Select(c => c.CALL_ID).ToList();

        indexed.ShouldBe(scanned);
        indexed.Count.ShouldBe(5);
    }
}
