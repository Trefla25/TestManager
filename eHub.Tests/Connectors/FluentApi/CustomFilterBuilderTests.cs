using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;

namespace eHub.Tests.Connectors.FluentApi;

public class CustomFilterBuilderTests
{
    private readonly CustomFilterBuilder _builder = new();

    [Fact]
    public void AddCustomFilter_WithValidInput_AddsFilter()
    {
        // Arrange
        const string filterKey = "TestFilter";
        const string sqlExpression = "CAST(BinaryData AS INT)";

        // Act
        _builder.AddCustomFilter<int>(filterKey, sqlExpression);
        var result = _builder.GetCustomFilters();
        
        // Assert
        result.Should().ContainKey(filterKey);
        result[filterKey].SqlExpression.Should().Be(sqlExpression);
        result[filterKey].Type.Should().Be<int>();
    }

    [Fact]
    public void AddCustomFilter_WithNullFilterKey_ThrowsArgumentException()
    {
        // Arrange
        const string sqlExpression = "CAST(BinaryData AS INT)";
        
        // Act
        var act = () => _builder.AddCustomFilter<int>(null!, sqlExpression);
        
        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Filter key cannot be null or whitespace.*")
            .And.ParamName.Should().Be("filterKey");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddCustomFilter_WithWhitespaceFilterKey_ThrowsArgumentException(string filterKey)
    {
        // Arrange
        const string sqlExpression = "CAST(BinaryData AS INT)";
        
        // Act
        var act = () => _builder.AddCustomFilter<int>(filterKey, sqlExpression);
        
        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Filter key cannot be null or whitespace.*")
            .And.ParamName.Should().Be("filterKey");
    }

    [Fact]
    public void AddCustomFilter_WithPacketFilterKey_ThrowsArgumentException()
    {
        // Arrange
        const string sqlExpression = "CAST(BinaryData AS INT)";
        
        // Act
        var act = () => _builder.AddCustomFilter<int>("Id", sqlExpression);
        
        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage($"The filter key 'Id' is reserved as it matches an existing packet column.*")
            .And.ParamName.Should().Be("filterKey");
    }

    [Fact]
    public void AddCustomFilter_WithNullSqlExpression_ThrowsArgumentException()
    {
        // Arrange
        const string filterKey = "TestFilter";
        
        // Act
        var act = () => _builder.AddCustomFilter<int>(filterKey, null!);
        
        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("SQL expression cannot be null or whitespace.*")
            .And.ParamName.Should().Be("sqlExpression");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddCustomFilter_WithWhitespaceSqlExpression_ThrowsArgumentException(string sqlExpression)
    {
        // Arrange
        const string filterKey = "TestFilter";
        
        // Act
        Action act = () => _builder.AddCustomFilter<int>(filterKey, sqlExpression);
        
        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("SQL expression cannot be null or whitespace.*")
            .And.ParamName.Should().Be("sqlExpression");
    }

    [Fact]
    public void AddCustomFilter_WithDuplicateKey_ThrowsArgumentException()
    {
        // Arrange
        const string filterKey = "DuplicateFilter";
        const string sqlExpression = "CAST(BinaryData AS INT)";

        // Act
        _builder.AddCustomFilter<int>(filterKey, sqlExpression);
        var act = () => _builder.AddCustomFilter<int>(filterKey, sqlExpression);
        
        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage($"A filter with the key '{filterKey}' already exists.*")
            .And.ParamName.Should().Be("filterKey");
    }

    [Fact]
    public void GetCustomFilters_ReturnsImmutableDictionary_WithAllAddedFilters()
    {
        // Arrange
        _builder
            .AddCustomFilter<int>("Filter1", "CAST(BinaryData AS INT)")
            .AddCustomFilter<string>("Filter2", "SUBSTRING(BinaryData, 1, 5)")
            .AddCustomFilter<DateTime>("Filter3", "CAST(BinaryData AS DATETIME)");
        
        // Act
        var filters = _builder.GetCustomFilters();
        
        // Assert
        filters.Should().HaveCount(3);
        filters.Should().ContainKey("Filter1");
        filters.Should().ContainKey("Filter2");
        filters.Should().ContainKey("Filter3");

        filters["Filter1"].SqlExpression.Should().Be("CAST(BinaryData AS INT)");
        filters["Filter1"].Type.Should().Be<int>();

        filters["Filter2"].SqlExpression.Should().Be("SUBSTRING(BinaryData, 1, 5)");
        filters["Filter2"].Type.Should().Be<string>();

        filters["Filter3"].SqlExpression.Should().Be("CAST(BinaryData AS DATETIME)");
        filters["Filter3"].Type.Should().Be<DateTime>();
    }
}
