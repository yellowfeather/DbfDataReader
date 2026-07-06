using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests;

// Differential harness for automatic index use: every query runs through the index
// path and through a forced full scan, and both must return identical row sequences.
// ExplainPlan assertions prove the intended path was actually taken. The fixture is
// setup.dbf/setup.CDX, whose KEY_NAME tag is an ascending candidate key on a
// character column (values CALLS, CONTACTS, CONTACT_TYPES).
[Collection("foxprodb")]
public class QueryIndexTests
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

    private static void ShouldMatchScan(string commandText, string expectedPlanFragment,
        (string Name, object Value)? parameter = null)
    {
        Explain(commandText, parameter).ShouldContain(expectedPlanFragment);

        var indexed = QueryRows(useIndexes: true, commandText, parameter);
        var scanned = QueryRows(useIndexes: false, commandText, parameter);

        indexed.ShouldBe(scanned);
    }

    [Theory]
    [InlineData("select * from setup.dbf where KEY_NAME = 'CONTACTS'", 1)]
    [InlineData("select * from setup.dbf where KEY_NAME = 'CONTACTS   '", 1)] // trailing spaces ignored
    [InlineData("select * from setup.dbf where KEY_NAME = 'contacts'", 0)] // case-sensitive
    [InlineData("select * from setup.dbf where KEY_NAME = 'NO-SUCH-KEY'", 0)]
    public void Should_seek_equality_through_the_index(string commandText, int expectedRows)
    {
        Explain(commandText).ShouldContain("index seek (=) on tag 'KEY_NAME'");

        var indexed = QueryRows(useIndexes: true, commandText);
        indexed.Count.ShouldBe(expectedRows);
        indexed.ShouldBe(QueryRows(useIndexes: false, commandText));
    }

    [Theory]
    [InlineData("select KEY_NAME from setup.dbf where KEY_NAME >= 'CO'")]
    [InlineData("select KEY_NAME from setup.dbf where KEY_NAME > 'CALLS'")]
    [InlineData("select KEY_NAME from setup.dbf where KEY_NAME <= 'CONTACTS'")]
    [InlineData("select KEY_NAME from setup.dbf where KEY_NAME < 'CONTACTS'")]
    public void Should_range_scan_through_the_index(string commandText)
    {
        ShouldMatchScan(commandText, "index range scan on tag 'KEY_NAME'");
    }

    [Fact]
    public void Should_range_scan_between_through_the_index()
    {
        ShouldMatchScan("select KEY_NAME from setup.dbf where KEY_NAME between 'CA' and 'CONTACTS'",
            "index range scan (between) on tag 'KEY_NAME'");
    }

    [Theory]
    [InlineData("select KEY_NAME from setup.dbf where KEY_NAME like 'CONT%'")]
    [InlineData("select KEY_NAME from setup.dbf where KEY_NAME like 'C%S'")] // prefix range + residual
    [InlineData("select KEY_NAME from setup.dbf where KEY_NAME like 'C_LLS'")]
    public void Should_prefix_scan_likes_through_the_index(string commandText)
    {
        ShouldMatchScan(commandText, "index prefix scan (like) on tag 'KEY_NAME'");
    }

    [Fact]
    public void Should_apply_residual_predicates_after_the_index()
    {
        ShouldMatchScan("select * from setup.dbf where KEY_NAME >= 'CA' and VALUE > 1",
            "index range scan on tag 'KEY_NAME'");
    }

    [Fact]
    public void Should_seek_with_parameters()
    {
        ShouldMatchScan("select * from setup.dbf where KEY_NAME = @name",
            "index seek (=) on tag 'KEY_NAME'", ("@name", "CONTACT_TYPES"));
    }

    [Fact]
    public void Should_satisfy_order_by_from_the_index()
    {
        var commandText = "select KEY_NAME from setup.dbf where KEY_NAME >= 'CA' order by KEY_NAME";

        var plan = Explain(commandText);
        plan.ShouldContain("index range scan");
        plan.ShouldNotContain("in-memory sort");

        QueryRows(useIndexes: true, commandText).ShouldBe(QueryRows(useIndexes: false, commandText));
    }

    [Fact]
    public void Should_order_without_a_filter_through_an_index_scan()
    {
        var commandText = "select KEY_NAME, VALUE from setup.dbf order by KEY_NAME";

        var plan = Explain(commandText);
        plan.ShouldContain("index order scan on tag 'KEY_NAME'");
        plan.ShouldNotContain("in-memory sort");

        QueryRows(useIndexes: true, commandText).ShouldBe(QueryRows(useIndexes: false, commandText));
    }

    [Fact]
    public void Should_sort_in_memory_when_the_index_cannot_satisfy_the_order()
    {
        var commandText = "select KEY_NAME from setup.dbf order by KEY_NAME desc";

        Explain(commandText).ShouldStartWith("full scan");

        QueryRows(useIndexes: true, commandText).ShouldBe(QueryRows(useIndexes: false, commandText));
    }

    [Theory]
    [InlineData("select * from setup.dbf where VALUE = 1", "no matching index tag")] // integer-keyed tags unsupported
    [InlineData("select * from calls.dbf where CONTACT_ID = 1", "no usable index tags")]
    [InlineData("select * from setup.dbf where KEY_NAME <> 'CALLS'", "no matching index tag")]
    [InlineData("select * from setup.dbf where KEY_NAME like '%S'", "no matching index tag")] // no usable prefix
    public void Should_fall_back_to_a_scan_for_unindexable_predicates(string commandText, string expectedReason)
    {
        Explain(commandText).ShouldContain(expectedReason);

        QueryRows(useIndexes: true, commandText).ShouldBe(QueryRows(useIndexes: false, commandText));
    }

    [Fact]
    public void Should_fall_back_to_a_scan_for_non_ascii_values()
    {
        var commandText = "select * from setup.dbf where KEY_NAME = 'CAFÉ'";

        Explain(commandText).ShouldStartWith("full scan");
        QueryRows(useIndexes: true, commandText).ShouldBeEmpty();
    }

    [Fact]
    public void Should_return_nothing_for_over_long_equality_keys()
    {
        var overLong = new string('X', 60); // KEY_NAME keys are 50 bytes
        var commandText = $"select * from setup.dbf where KEY_NAME = '{overLong}'";

        Explain(commandText).ShouldContain("index seek");
        QueryRows(useIndexes: true, commandText).ShouldBeEmpty();
        QueryRows(useIndexes: false, commandText).ShouldBeEmpty();
    }

    [Fact]
    public void Should_respect_the_use_indexes_option()
    {
        using var connection = OpenConnection(useIndexes: false);
        var command = (DbfDbCommand)connection.CreateCommand();
        command.CommandText = "select * from setup.dbf where KEY_NAME = 'CONTACTS'";

        command.ExplainPlan().ShouldBe("full scan (indexes disabled)");
    }

    [Fact]
    public void Should_report_a_scan_when_there_is_no_index_file()
    {
        var connection = new DbfDbConnection();
        connection.ConnectionString = "Folder=../../../../fixtures;SkipDeletedRecords=false";
        connection.Open();

        using (connection)
        {
            var command = (DbfDbCommand)connection.CreateCommand();
            command.CommandText = "select * from dbase_03.dbf where Point_ID = 'x'";

            command.ExplainPlan().ShouldBe("full scan (no index file)");
        }
    }

    public class SetupRow
    {
        public string KEY_NAME { get; set; }
        public long? VALUE { get; set; }
    }

    [Fact]
    public void Should_use_the_index_from_the_query_builder()
    {
        using var dbfTable = new DbfTable($"{FolderPath}/setup.dbf");

        var indexedQuery = dbfTable.Query<SetupRow>().Where(r => r.KEY_NAME == "CONTACTS");
        indexedQuery.ExplainPlan().ShouldContain("index seek (=) on tag 'KEY_NAME'");
        var indexed = indexedQuery.ToList();

        var scanned = dbfTable.Query<SetupRow>().Where(r => r.KEY_NAME == "CONTACTS").WithoutIndexes().ToList();

        indexed.Select(r => $"{r.KEY_NAME}|{r.VALUE}").ShouldBe(scanned.Select(r => $"{r.KEY_NAME}|{r.VALUE}"));
        indexed.Count.ShouldBe(1);
    }

    [Fact]
    public void Should_use_the_index_for_builder_prefix_and_order()
    {
        using var dbfTable = new DbfTable($"{FolderPath}/setup.dbf");

        var query = dbfTable.Query<SetupRow>()
            .Where(r => r.KEY_NAME.StartsWith("CONT"))
            .OrderBy(r => r.KEY_NAME);

        var plan = query.ExplainPlan();
        plan.ShouldContain("index prefix scan");
        plan.ShouldNotContain("in-memory sort");

        var indexed = query.ToList().Select(r => r.KEY_NAME).ToList();
        var scanned = dbfTable.Query<SetupRow>()
            .Where(r => r.KEY_NAME.StartsWith("CONT"))
            .OrderBy(r => r.KEY_NAME)
            .WithoutIndexes()
            .ToList().Select(r => r.KEY_NAME).ToList();

        indexed.ShouldBe(scanned);
        indexed.ShouldBe(new List<string> { "CONTACTS", "CONTACT_TYPES" });
    }

    [Fact]
    public void Should_count_through_the_index_from_the_builder()
    {
        using var dbfTable = new DbfTable($"{FolderPath}/setup.dbf");

        var query = dbfTable.Query<SetupRow>().Where(r => r.KEY_NAME.StartsWith("CONT"));

        query.ExplainPlan().ShouldContain("index prefix scan");
        query.Count().ShouldBe(2);
    }
}
