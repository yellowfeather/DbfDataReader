using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests;

[Collection("dbase_03")]
public class DbfDbConnectionTests
{
    private const string FolderPath = "../../../../fixtures";

    [Fact]
    public async Task Should_read_all_rows()
    {
        var dbConnection = new DbfDbConnection();
        dbConnection.ConnectionString = $"Folder={FolderPath};Encoding=ascii;SkipDeletedRecords=false";
        dbConnection.Open();
        
        var dbCommand = dbConnection.CreateCommand();
        dbCommand.CommandText = "select * from dbase_03.dbf;";

        var rowCount = 0;
        var reader = await dbCommand.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rowCount++;

            _ = reader.GetString(0);
            _ = reader.GetDecimal(10);
        }

        rowCount.ShouldBe(14);
    }
    
    [Fact]
    public async Task Should_skip_deleted_rows()
    {
        var dbConnection = new DbfDbConnection();
        dbConnection.ConnectionString = $"Folder={FolderPath};Encoding=ascii;SkipDeletedRecords=true";
        dbConnection.Open();
        
        var dbCommand = dbConnection.CreateCommand();
        dbCommand.CommandText = "select * from dbase_03.dbf;";

        var rowCount = 0;
        var reader = await dbCommand.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rowCount++;

            _ = reader.GetString(0);
            _ = reader.GetDecimal(10);
        }

        rowCount.ShouldBe(12);
    }    
}