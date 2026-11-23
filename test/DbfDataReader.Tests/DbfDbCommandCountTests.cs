using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DbfDataReader.Query;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests;

// COUNT(*) strategy matrix: status-only scans without a WHERE, index-only counts when
// the index covers the WHERE exactly, status checks when deleted rows must be skipped,
// and row reading when a residual filter is required. Counts are checked against
// full-scan oracles throughout.
[Collection("foxprodb")]
public class DbfDbCommandCountTests
{
    private const string FixturesPath = "../../../../fixtures";
    private const string FoxproPath = "../../../../fixtures/foxprodb";

    private static DbfDbConnection OpenConnection(string folder, bool skipDeleted, bool useIndexes = true)
    {
        var connection = new DbfDbConnection();
        connection.ConnectionString = $"Folder={folder};SkipDeletedRecords={skipDeleted};UseIndexes={useIndexes}";
        connection.Open();
        return connection;
    }

    private static (object Scalar, string Plan) CountAndExplain(string folder, string commandText,
        bool skipDeleted = false, bool useIndexes = true)
    {
        using var connection = OpenConnection(folder, skipDeleted, useIndexes);
        var command = (DbfDbCommand)connection.CreateCommand();
        command.CommandText = commandText;

        return (command.ExecuteScalar(), command.ExplainPlan());
    }

    [Fact]
    public void Should_parse_count_star()
    {
        var statement = SqlParser.Parse("select count(*) from t.dbf");
        statement.IsCountAll.ShouldBeTrue();
        statement.Columns.ShouldBeEmpty();

        SqlParser.Parse("select top 5 count ( * ) from t.dbf where a = 1").IsCountAll.ShouldBeTrue();

        // COUNT is not reserved: a column named count still works
        var columnStatement = SqlParser.Parse("select count from t.dbf");
        columnStatement.IsCountAll.ShouldBeFalse();
        columnStatement.Columns[0].ColumnName.ShouldBe("count");
    }

    [Theory]
    [InlineData("select count(x) from t.dbf", "expected '*'")]
    [InlineData("select count(*), a from t.dbf", "COUNT(*) must be the only item")]
    [InlineData("select count(*) from t.dbf order by a", "ORDER BY cannot be used with COUNT(*)")]
    public void Should_reject_unsupported_count_shapes(string commandText, string expectedFragment)
    {
        Should.Throw<SqlParseException>(() => SqlParser.Parse(commandText))
            .Message.ShouldContain(expectedFragment);
    }

    [Fact]
    public void Should_count_all_records_via_a_status_scan()
    {
        var (all, planAll) = CountAndExplain(FixturesPath, "select count(*) from dbase_03.dbf");
        all.ShouldBe(14);
        planAll.ShouldBe("count via record status scan");

        var (active, _) = CountAndExplain(FixturesPath, "select count(*) from dbase_03.dbf", skipDeleted: true);
        active.ShouldBe(12);
    }

    [Fact]
    public void Should_count_filtered_rows_by_reading_them_when_no_index_applies()
    {
        var oracle = CountOracle(r => r.MaxPdop >= 3.5m);

        var (count, plan) = CountAndExplain(FixturesPath,
            "select count(*) from dbase_03.dbf where Max_PDOP >= 3.5");

        count.ShouldBe(oracle);
        plan.ShouldContain("(count by reading rows)");
        plan.ShouldContain("full scan");
    }

    [Fact]
    public void Should_count_from_the_index_alone_when_it_covers_the_where_exactly()
    {
        var (count, plan) = CountAndExplain(FoxproPath, "select count(*) from calls.dbf where CONTACT_ID = 1");

        count.ShouldBe(5);
        plan.ShouldContain("index seek (=) on tag 'CONTACT_ID'");
        plan.ShouldContain("(count from index only)");
    }

    [Fact]
    public void Should_check_record_statuses_when_deleted_rows_are_skipped()
    {
        var (count, plan) = CountAndExplain(FoxproPath, "select count(*) from calls.dbf where CONTACT_ID = 1",
            skipDeleted: true);

        count.ShouldBe(5); // calls.dbf has no deleted rows; the branch still runs
        plan.ShouldContain("(count with record status checks)");
    }

    [Fact]
    public void Should_count_zero_for_provably_empty_seeks()
    {
        var (count, plan) = CountAndExplain(FoxproPath, "select count(*) from calls.dbf where CALL_ID = 1.5");

        count.ShouldBe(0);
        plan.ShouldContain("(count from index only)");
    }

    [Fact]
    public void Should_read_rows_when_a_residual_filter_remains()
    {
        var (count, plan) = CountAndExplain(FoxproPath,
            "select count(*) from calls.dbf where CONTACT_ID = 1 and CALL_ID > 2");

        count.ShouldBe(3); // records 3, 4, 5 of the five CONTACT_ID = 1 rows
        plan.ShouldContain("index seek (=)");
        plan.ShouldContain("(count by reading rows)");
    }

    [Fact]
    public void Should_read_rows_for_inexact_index_results()
    {
        var (count, plan) = CountAndExplain(FoxproPath,
            "select count(*) from setup.dbf where KEY_NAME like 'CONT%'");

        count.ShouldBe(2);
        plan.ShouldContain("index prefix scan");
        plan.ShouldContain("(count by reading rows)");
    }

    [Fact]
    public void Should_count_character_equality_from_the_index_alone()
    {
        var (count, plan) = CountAndExplain(FoxproPath,
            "select count(*) from setup.dbf where KEY_NAME = 'CONTACTS'");

        count.ShouldBe(1);
        plan.ShouldContain("(count from index only)");
    }

    [Fact]
    public void Should_count_identically_with_indexes_disabled()
    {
        var (indexed, _) = CountAndExplain(FoxproPath, "select count(*) from calls.dbf where CONTACT_ID = 1");
        var (scanned, plan) = CountAndExplain(FoxproPath, "select count(*) from calls.dbf where CONTACT_ID = 1",
            useIndexes: false);

        scanned.ShouldBe(indexed);
        plan.ShouldContain("full scan (indexes disabled)");
    }

    [Fact]
    public void Should_expose_the_count_through_a_single_row_reader()
    {
        using var connection = OpenConnection(FixturesPath, skipDeleted: false);
        var command = connection.CreateCommand();
        command.CommandText = "select count(*) from dbase_03.dbf";

        using var reader = command.ExecuteReader();
        reader.FieldCount.ShouldBe(1);
        reader.GetName(0).ShouldBe("count");
        reader.HasRows.ShouldBeTrue();

        reader.Read().ShouldBeTrue();
        reader.GetInt32(0).ShouldBe(14);
        reader.GetInt64(0).ShouldBe(14L);
        reader["count"].ShouldBe(14);
        reader.GetFieldType(0).ShouldBe(typeof(int));

        var schemaTable = reader.GetSchemaTable();
        schemaTable.Rows.Count.ShouldBe(1);
        schemaTable.Rows[0]["ColumnName"].ShouldBe("count");

        reader.Read().ShouldBeFalse();
    }

    [Fact]
    public void Should_honour_top_on_the_result_row()
    {
        using var connection = OpenConnection(FixturesPath, skipDeleted: false);

        var command = (DbfDbCommand)connection.CreateCommand();
        command.CommandText = "select top 0 count(*) from dbase_03.dbf";
        using (var reader = command.ExecuteReader())
        {
            reader.Read().ShouldBeFalse();
        }

        command.CommandText = "select top 3 count(*) from dbase_03.dbf";
        command.ExecuteScalar().ShouldBe(14);
    }

    [Fact]
    public void Should_count_with_parameters()
    {
        using var connection = OpenConnection(FoxproPath, skipDeleted: false);
        var command = (DbfDbCommand)connection.CreateCommand();
        command.CommandText = "select count(*) from calls.dbf where CONTACT_ID = @id";
        command.Parameters.AddWithValue("@id", 1);

        command.ExecuteScalar().ShouldBe(5);
        command.ExplainPlan().ShouldContain("(count from index only)");
    }

    public class CallRow
    {
        public long? CALL_ID { get; set; }
        public long? CONTACT_ID { get; set; }
    }

    [Fact]
    public void Builder_should_count_through_the_fast_paths()
    {
        using var dbfTable = new DbfTable($"{FoxproPath}/calls.dbf");

        // no filter: status scan
        dbfTable.Query<CallRow>().Count().ShouldBe(16);

        // exact index cover, skip-deleted default: status checks
        dbfTable.Query<CallRow>().Where(c => c.CONTACT_ID == 1).Count().ShouldBe(5);

        // exact index cover, deleted included: pure index count
        dbfTable.Query<CallRow>().Where(c => c.CONTACT_ID == 1).IncludeDeleted().Count().ShouldBe(5);

        // residual filter: rows are read
        dbfTable.Query<CallRow>().Where(c => c.CONTACT_ID == 1).Where(c => c.CALL_ID > 2).Count().ShouldBe(3);

        // Take caps the count
        dbfTable.Query<CallRow>().Where(c => c.CONTACT_ID == 1).Take(3).Count().ShouldBe(3);

        // forced scans agree
        dbfTable.Query<CallRow>().Where(c => c.CONTACT_ID == 1).WithoutIndexes().Count().ShouldBe(5);
    }

    private sealed record BaselineRow(decimal MaxPdop);

    private static int CountOracle(System.Func<BaselineRow, bool> predicate)
    {
        var rows = new List<BaselineRow>();

        using var dbfTable = new DbfTable($"{FixturesPath}/dbase_03.dbf");
        var dbfRecord = new DbfRecord(dbfTable);

        while (dbfTable.Read(dbfRecord))
        {
            rows.Add(new BaselineRow(dbfRecord.GetValue<decimal>(10)));
        }

        return rows.Count(predicate);
    }
}
