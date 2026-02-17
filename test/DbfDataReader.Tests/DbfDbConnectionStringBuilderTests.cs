using System.Text;
using Bogus;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests;

public class DbfDbConnectionStringBuilderTests
{
    [Fact]
    public void Should_parse_folder() {
        // Arrange
        var faker = new Faker();
        var folder = faker.System.DirectoryPath();
        var connectionString = $"Folder={folder}";

        // Act
        var builder = new DbfDbConnectionStringBuilder(connectionString);
        
        // Assert
        builder.Folder.ShouldBe(folder);
    }
    
    [Fact]
    public void Should_parse_encoding() {
        // Arrange
        var encoding = "ASCII";
        var connectionString = $"Encoding={encoding}";

        // Act
        var builder = new DbfDbConnectionStringBuilder(connectionString);
        
        // Assert
        builder.Encoding.ShouldBe(Encoding.GetEncoding(encoding));
    }

    [Fact]
    public void Should_parse_read_floats_as_decimals() {
        // Arrange
        var faker = new Faker();
        var readFloatsAsDecimals = faker.Random.Bool();
        var connectionString = $"ReadFloatsAsDecimals={readFloatsAsDecimals}";

        // Act
        var builder = new DbfDbConnectionStringBuilder(connectionString);
        
        // Assert
        builder.ReadFloatsAsDecimals.ShouldBe(readFloatsAsDecimals);
    }
    
    [Fact]
    public void Should_parse_skip_deleted_records() {
        // Arrange
        var faker = new Faker();
        var skipDeletedRecords = faker.Random.Bool();
        var connectionString = $"SkipDeletedRecords={skipDeletedRecords}";

        // Act
        var builder = new DbfDbConnectionStringBuilder(connectionString);
        
        // Assert
        builder.SkipDeletedRecords.ShouldBe(skipDeletedRecords);
    }
    
    [Fact]
    public void Should_parse_string_trimming() {
        // Arrange
        var faker = new Faker();
        var stringTrimming = faker.PickRandom<StringTrimmingOption>();
        var connectionString = $"StringTrimming={stringTrimming}";

        // Act
        var builder = new DbfDbConnectionStringBuilder(connectionString);
        
        // Assert
        builder.StringTrimming.ShouldBe(stringTrimming);
    }
    
    [Fact]
    public void Should_parse_example1() {
        // Arrange
        var faker = new Faker();
        var folder = faker.System.DirectoryPath();
        var skipDeletedRecords = faker.Random.Bool();
        var connectionString = $"Folder={folder};SkipDeletedRecords={skipDeletedRecords}";

        // Act
        var builder = new DbfDbConnectionStringBuilder(connectionString);
        
        // Assert
        builder.Folder.ShouldBe(folder);
        builder.SkipDeletedRecords.ShouldBe(skipDeletedRecords);
    }
    
    [Fact]
    public void Should_parse_example2() {
        // Arrange
        var faker = new Faker();
        var folder = faker.System.DirectoryPath();
        var skipDeletedRecords = faker.Random.Bool();
        var stringTrimming = faker.PickRandom<StringTrimmingOption>();
        var connectionString = $"Folder={folder};SkipDeletedRecords={skipDeletedRecords};StringTrimming={stringTrimming}";

        // Act
        var builder = new DbfDbConnectionStringBuilder(connectionString);
        
        // Assert
        builder.Folder.ShouldBe(folder);
        builder.SkipDeletedRecords.ShouldBe(skipDeletedRecords);
        builder.StringTrimming.ShouldBe(stringTrimming);
    }
}