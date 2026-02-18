using System;
using Bogus;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests;

public class QueryParserTests
{
    [Fact]
    public void Should_parse_lowercase_query()
    {
        // Arrange
        var faker = new Faker();
        var fileName = $"{faker.System.FileName()}.dbf";
        var query = $"select * from {fileName}";
        
        // Act
        var result = QueryParser.Parse(query);
        
        // Assert
        result.ShouldBe(fileName);
    }
    
    [Fact]
    public void Should_parse_uppercase_query()
    {
        // Arrange
        var faker = new Faker();
        var fileName = $"{faker.System.FileName()}.dbf".ToUpper();
        var query = $"SELECT * FROM {fileName}";
        
        // Act
        var result = QueryParser.Parse(query);
        
        // Assert
        result.ShouldBe(fileName);
    }
    
    [Fact]
    public void Should_parse_mixed_case_query()
    {
        // Arrange
        var faker = new Faker();
        var fileName = $"{faker.System.FileName()}.dbf";
        var query = $"SELECT * from {fileName}";
        
        // Act
        var result = QueryParser.Parse(query);
        
        // Assert
        result.ShouldBe(fileName);
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
