using System;
using System.IO;
using Bogus;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests;

public class QueryParserTests
{
    private readonly string _fileName;
    public QueryParserTests()
    {
        var faker = new Faker();
        var fileName = faker.System.FileName("dbf");
        _fileName = fileName.Replace("&", "_and_");
    }
    
    [Fact]
    public void Should_parse_filename_without_extension()
    {
        // Arrange
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(_fileName);
        var query = $"select * from {fileNameWithoutExtension}";
        
        // Act
        var result = QueryParser.Parse(query);
        
        // Assert
        result.ShouldBe(fileNameWithoutExtension);
    }

    [Fact]
    public void Should_parse_lowercase_query()
    {
        // Arrange
        var query = $"select * from {_fileName}";
        
        // Act
        var result = QueryParser.Parse(query);
        
        // Assert
        result.ShouldBe(_fileName);
    }
    
    [Fact]
    public void Should_parse_uppercase_query()
    {
        // Arrange
        var query = $"SELECT * FROM {_fileName}";
        
        // Act
        var result = QueryParser.Parse(query);
        
        // Assert
        result.ShouldBe(_fileName);
    }
    
    [Fact]
    public void Should_parse_mixed_case_query()
    {
        // Arrange
        var query = $"SELECT * from {_fileName}";
        
        // Act
        var result = QueryParser.Parse(query);
        
        // Assert
        result.ShouldBe(_fileName);
    }
    
    [Fact]
    public void Should_throw_with_invalid_query()
    {
        // Arrange
        var faker = new Faker();
        var query = faker.Lorem.Sentence();
        
        // Act & Assert
        Should.Throw<ArgumentException>(() => QueryParser.Parse(query));
    }
}
