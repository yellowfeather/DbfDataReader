using System;
using DbfDataReader.Query;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests;

[Collection("dbase_03")]
public class DbfDbCommandTests
{
    private const string FolderPath = "../../../../fixtures";

    private static DbfDbConnection OpenConnection()
    {
        var connection = new DbfDbConnection();
        connection.ConnectionString = $"Folder={FolderPath};SkipDeletedRecords=false";
        connection.Open();
        return connection;
    }

    [Fact]
    public void Should_execute_select_all()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "select * from dbase_03.dbf";

        using var reader = command.ExecuteReader();
        var rowCount = 0;
        while (reader.Read()) rowCount++;

        rowCount.ShouldBe(14);
    }

    [Theory]
    [InlineData("select * from dbase_03.dbf order by Point_ID", "ORDER BY")]
    public void Should_throw_not_supported_for_parsed_but_unimplemented_features(string commandText,
        string expectedFragment)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = commandText;

        var exception = Should.Throw<NotSupportedException>(() => command.ExecuteReader());

        exception.Message.ShouldContain(expectedFragment);
    }

    [Fact]
    public void Should_throw_argument_exception_for_invalid_command_text()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "this is not sql";

        // SqlParseException derives from ArgumentException for compatibility with the
        // previous parser's contract
        var exception = Should.Throw<ArgumentException>(() => command.ExecuteReader());
        exception.ShouldBeOfType<SqlParseException>();
    }
}
