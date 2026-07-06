using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests;

// the ADO.NET provider should work with real Dapper, not just the built-in mapper;
// calls go through IDbConnection so they resolve to Dapper's extension methods rather
// than DbfDbConnection's own Query<T> instance methods
[Collection("dbase_03")]
public class DapperCompatibilityTests
{
    private const string FolderPath = "../../../../fixtures";

    private static DbfDbConnection OpenConnection()
    {
        var connection = new DbfDbConnection();
        connection.ConnectionString = $"Folder={FolderPath};SkipDeletedRecords=false";
        connection.Open();
        return connection;
    }

    private class DapperPoint
    {
        public string Point_ID { get; set; }
        public decimal? Max_PDOP { get; set; }
        public DateTime? Date_Visit { get; set; }
    }

    [Fact]
    public void Dapper_should_query_typed_rows()
    {
        using IDbConnection db = OpenConnection();

        var points = db.Query<DapperPoint>(
            "select Point_ID, Max_PDOP, Date_Visit from dbase_03.dbf").ToList();

        points.Count.ShouldBe(14);
        points[0].Point_ID.ShouldNotBeNullOrEmpty();
        points.ShouldContain(p => p.Max_PDOP != null);
    }

    [Fact]
    public void Dapper_should_bind_parameters()
    {
        using IDbConnection db = OpenConnection();

        var target = db.QueryFirst<string>("select Point_ID from dbase_03.dbf");
        var points = db.Query<DapperPoint>(
            "select Point_ID, Max_PDOP, Date_Visit from dbase_03.dbf where Point_ID = @id",
            new { id = target }).ToList();

        points.ShouldNotBeEmpty();
        points.ShouldAllBe(p => p.Point_ID == target);
    }

    [Fact]
    public void Dapper_should_execute_scalars_and_ordered_queries()
    {
        using IDbConnection db = OpenConnection();

        var maxPdop = db.ExecuteScalar<decimal>(
            "select Max_PDOP from dbase_03.dbf order by Max_PDOP desc");
        maxPdop.ShouldBeGreaterThan(0m);

        var first = db.QueryFirstOrDefault<DapperPoint>(
            "select Point_ID, Max_PDOP, Date_Visit from dbase_03.dbf where Point_ID = 'no-such-value'");
        first.ShouldBeNull();
    }

    [Fact]
    public async Task Dapper_should_query_asynchronously()
    {
        using IDbConnection db = OpenConnection();

        var points = (await db.QueryAsync<DapperPoint>(
            "select top 5 Point_ID, Max_PDOP, Date_Visit from dbase_03.dbf order by Max_PDOP desc")).ToList();

        points.Count.ShouldBe(5);
        points.Select(p => p.Max_PDOP).ShouldBe(
            points.Select(p => p.Max_PDOP).OrderByDescending(v => v).ToList());
    }
}
