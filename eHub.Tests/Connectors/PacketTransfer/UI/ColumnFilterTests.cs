using eHub.Contracts.UIConfig;
using eHub.Contracts;
using eHub.Contracts.Helper;
using FluentAssertions;

namespace eHub.Tests.Connectors.PacketTransfer.UI;

public class ColumnFilterTests
{
    [Fact]
    public void IsNumericFilter_WithNumericColumns_ReturnsTrue()
    {
        new ColumnFilterDto(nameof(PacketDto.Id), ColumnFilterOperator.Equal, null).IsNumericFilter().Should().BeTrue();
        new ColumnFilterDto (nameof(PacketDto.ParentId), ColumnFilterOperator.Equal, null).IsNumericFilter().Should().BeTrue();
        new ColumnFilterDto (nameof(PacketDto.RetryCount), ColumnFilterOperator.Equal, null).IsNumericFilter().Should().BeTrue();
    }

    [Fact]
    public void IsNumericFilter_WithNonNumericColumns_ReturnsFalse()
    {
        new ColumnFilterDto(nameof(PacketDto.Channel), ColumnFilterOperator.Equal, null).IsNumericFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.DateCreated), ColumnFilterOperator.Equal, null).IsNumericFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.DateChanged), ColumnFilterOperator.Equal, null).IsNumericFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.DynamicField), ColumnFilterOperator.Equal, null).IsNumericFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Metadata), ColumnFilterOperator.Equal, null).IsNumericFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Data), ColumnFilterOperator.Equal, null).IsNumericFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Status), ColumnFilterOperator.Equal, null).IsNumericFilter().Should().BeFalse();
    }

    [Fact]
    public void IsDateTimeFilter_WithDateTimeColumns_ReturnsTrue()
    {
        new ColumnFilterDto(nameof(PacketDto.DateCreated), ColumnFilterOperator.Equal, null).IsDateTimeFilter().Should().BeTrue();
        new ColumnFilterDto(nameof(PacketDto.DateChanged), ColumnFilterOperator.Equal, null).IsDateTimeFilter().Should().BeTrue();
    }

    [Fact]
    public void IsDateTimeFilter_WithNonDateTimeColumns_ReturnsFalse()
    {
        new ColumnFilterDto(nameof(PacketDto.Id), ColumnFilterOperator.Equal, null).IsDateTimeFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.ParentId), ColumnFilterOperator.Equal, null).IsDateTimeFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.RetryCount), ColumnFilterOperator.Equal, null).IsDateTimeFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Channel), ColumnFilterOperator.Equal, null).IsDateTimeFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.DynamicField), ColumnFilterOperator.Equal, null).IsDateTimeFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Metadata), ColumnFilterOperator.Equal, null).IsDateTimeFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Data), ColumnFilterOperator.Equal, null).IsDateTimeFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Status), ColumnFilterOperator.Equal, null).IsDateTimeFilter().Should().BeFalse();
    }

    [Fact]
    public void IsStringFilter_WithStringColumns_ReturnsTrue()
    {
        new ColumnFilterDto(nameof(PacketDto.Channel), ColumnFilterOperator.Equal, null).IsStringFilter().Should().BeTrue();
        new ColumnFilterDto(nameof(PacketDto.DynamicField), ColumnFilterOperator.Equal, null).IsStringFilter().Should().BeTrue();
        new ColumnFilterDto(nameof(PacketDto.Metadata), ColumnFilterOperator.Equal, null).IsStringFilter().Should().BeTrue();
    }

    [Fact]
    public void IsStringFilter_WithNonStringColumns_ReturnsFalse()
    {
        new ColumnFilterDto(nameof(PacketDto.Id), ColumnFilterOperator.Equal, null).IsStringFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.ParentId), ColumnFilterOperator.Equal, null).IsStringFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.RetryCount), ColumnFilterOperator.Equal, null).IsStringFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.DateCreated), ColumnFilterOperator.Equal, null).IsStringFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.DateChanged), ColumnFilterOperator.Equal, null).IsStringFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Status), ColumnFilterOperator.Equal, null).IsStringFilter().Should().BeFalse();
    }

    [Fact]
    public void IsStatusFilter_WithStatusColumn_ReturnsTrue()
    {
        new ColumnFilterDto(nameof(PacketDto.Status), ColumnFilterOperator.Equal, null).IsStatusFilter().Should().BeTrue();
    }

    [Fact]
    public void IsStatusFilter_WithNonStatusColumn_ReturnsFalse()
    {
        new ColumnFilterDto(nameof(PacketDto.Id), ColumnFilterOperator.Equal, null).IsStatusFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.ParentId), ColumnFilterOperator.Equal, null).IsStatusFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.RetryCount), ColumnFilterOperator.Equal, null).IsStatusFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.DateCreated), ColumnFilterOperator.Equal, null).IsStatusFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.DateChanged), ColumnFilterOperator.Equal, null).IsStatusFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Channel), ColumnFilterOperator.Equal, null).IsStatusFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.DynamicField), ColumnFilterOperator.Equal, null).IsStatusFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Metadata), ColumnFilterOperator.Equal, null).IsStatusFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Data), ColumnFilterOperator.Equal, null).IsStatusFilter().Should().BeFalse();
    }

    [Fact]
    public void IsDataFilter_WithDataColumn_ReturnsTrue()
    {
        new ColumnFilterDto(nameof(PacketDto.Data), ColumnFilterOperator.Equal, null).IsDataFilter().Should().BeTrue();
    }

    [Fact]
    public void IsDataFilter_WithNonDataColumn_ReturnsFalse()
    {
        new ColumnFilterDto(nameof(PacketDto.Id), ColumnFilterOperator.Equal, null).IsDataFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.ParentId), ColumnFilterOperator.Equal, null).IsDataFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.RetryCount), ColumnFilterOperator.Equal, null).IsDataFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.DateCreated), ColumnFilterOperator.Equal, null).IsDataFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.DateChanged), ColumnFilterOperator.Equal, null).IsDataFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Channel), ColumnFilterOperator.Equal, null).IsDataFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.DynamicField), ColumnFilterOperator.Equal, null).IsDataFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Metadata), ColumnFilterOperator.Equal, null).IsDataFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Status), ColumnFilterOperator.Equal, null).IsDataFilter().Should().BeFalse();
    }

    [Fact]
    public void IsDataFiltered_NoFilters_ReturnsTrue()
    {
        // Arrange
        const string data = "any value";
        var filters = Array.Empty<ColumnFilterDto>();
        
        // Act
        var result = filters.Matches(data);
        
        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsDataFiltered_WhenMatchingEqual_ReturnsTrue()
    {
        // Arrange
        const string data = "match";
        var filters = new ColumnFilterDto[]
        {
            new(nameof(PacketDto.Data), ColumnFilterOperator.Equal, "match" )
        };
        
        // Act
        var result = filters.Matches(data);
        
        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsDataFiltered_WhenNotMatchingEqual_ReturnsFalse()
    {
        // Arrange
        const string data = "no match";
        var filters = new ColumnFilterDto[]
        {
            new(nameof(PacketDto.Data), ColumnFilterOperator.Equal, "match" )
        };
        
        // Act
        var result = filters.Matches(data);
        
        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsDataFiltered_WhenMatchingNotEqual_ReturnsFalse()
    {
        // Arrange
        const string data = "match";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.NotEqual, "match")
        };
        
        // Act
        var result = filters.Matches(data);
        
        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsDataFiltered_WhenNotMatchingNotEqual_ReturnsTrue()
    {
        // Arrange
        const string data = "no match";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.NotEqual, "match")
        };
        
        // Act
        var result = filters.Matches(data);
        
        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsDataFiltered_WhenMatchingContains_ReturnsTrue()
    {
        // Arrange
        const string data = "one match data";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.Contains, "match")
        };
        
        // Act
        var result = filters.Matches(data);
        
        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsDataFiltered_WhenNotMatchingContains_ReturnsFalse()
    {
        // Arrange
        const string data = "no data";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.Contains, "match")
        };
        
        // Act
        var result = filters.Matches(data);
        
        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsDataFiltered_WhenMatchingNotContains_ReturnsTrue()
    {
        // Arrange
        const string data = "no data";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.NotContains, "match")
        };
        
        // Act
        var result = filters.Matches(data);
        
        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsDataFiltered_WhenNotMatchingNotContains_ReturnsFalse()
    {
        // Arrange
        const string data = "one match data";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.NotContains, "match")
        };
        
        // Act
        var result = filters.Matches(data);
        
        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsDataFiltered_WhenMatchingStart_ReturnsTrue()
    {
        // Arrange
        const string data = "match this";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.StartsWith, "match")
        };
        
        // Act
        var result = filters.Matches(data);
        
        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsDataFiltered_WhenNotMatchingStart_ReturnsFalse()
    {
        // Arrange
        const string data = "no match";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.StartsWith, "match")
        };
        
        // Act
        var result = filters.Matches(data);
        
        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsDataFiltered_WhenMatchingEnd_ReturnsTrue()
    {
        // Arrange
        const string data = "this match";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.EndsWith, "match")
        };
        
        // Act
        var result = filters.Matches(data);
        
        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsDataFiltered_WhenNotMatchingEnd_ReturnsFalse()
    {
        // Arrange
        const string data = "match this";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.EndsWith, "match")
        };
        
        // Act
        var result = filters.Matches(data);
        
        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsDataFiltered_WhenMatchingEmpty_ReturnsTrue()
    {
        // Arrange
        const string data = "";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.Empty, null)
        };
        
        // Act
        var result = filters.Matches(data);
        
        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsDataFiltered_WhenNotMatchingEmpty_ReturnsFalse()
    {
        // Arrange
        const string data = "not empty";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.Empty, null)
        };
        
        // Act
        var result = filters.Matches(data);
        
        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsDataFiltered_WhenMatchingNotEmpty_ReturnsTrue()
    {
        // Arrange
        const string data = "match";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.NotEmpty, null)
        };
        
        // Act
        var result = filters.Matches(data);
        
        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsDataFiltered_WhenNotMatchingNotEmpty_ReturnsFalse()
    {
        // Arrange
        const string data = "";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.NotEmpty, null)
        };
        
        // Act
        var result = filters.Matches(data);
        
        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsDataFiltered_MultipleValidFilters_ReturnsTrue()
    {
        // Arrange
        const string data = "hello world!";
        var filters = new ColumnFilterDto[]
        {
            new(nameof(PacketDto.Data), ColumnFilterOperator.Contains, "hello" ),
            new(nameof(PacketDto.Data), ColumnFilterOperator.EndsWith, "world!"),
            new(nameof(PacketDto.Data), ColumnFilterOperator.NotEmpty, null)
        };
        
        // Act
        var result = filters.Matches(data);
        
        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsDataFiltered_AtLeastOneInvalidFilter_ReturnsFalse()
    {
        // Arrange
        const string data = "hello world!";
        var filters = new ColumnFilterDto[]
        {
            new(nameof(PacketDto.Data), ColumnFilterOperator.Contains, "hello" ),
            new(nameof(PacketDto.Data), ColumnFilterOperator.EndsWith, "world!"),
            new(nameof(PacketDto.Data), ColumnFilterOperator.StartsWith, "start")
        };
        
        // Act
        var result = filters.Matches(data);
        
        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsDataFiltered_UnsupportedOperators_ThrowsException()
    {
        // Arrange
        const string data = "some data";
        var greaterThanFilter = new ColumnFilterDto[] { new (nameof(PacketDto.Data), ColumnFilterOperator.GreaterThan, null) };
        var greaterThanOrEqualFilter = new ColumnFilterDto[] { new (nameof(PacketDto.Data), ColumnFilterOperator.GreaterThanOrEqual, null) };
        var lessThanFilter = new ColumnFilterDto[] { new(nameof(PacketDto.Data), ColumnFilterOperator.LessThan, null) };
        var lessThanOrEqualFilter = new ColumnFilterDto[] { new(nameof(PacketDto.Data), ColumnFilterOperator.LessThanOrEqual, null) };

        // Act
        var greaterThanAct = () => greaterThanFilter.Matches(data);
        var greaterThanOrEqualAct = () => greaterThanOrEqualFilter.Matches(data);
        var lessThanAct = () => lessThanFilter.Matches(data);
        var lessThanOrEqualAct = () => lessThanOrEqualFilter.Matches(data);
        // Assert
        greaterThanAct.Should().Throw<InvalidOperationException>().WithMessage("Unsupported operator GreaterThan for filtering data");
        greaterThanOrEqualAct.Should().Throw<InvalidOperationException>().WithMessage("Unsupported operator GreaterThanOrEqual for filtering data");
        lessThanAct.Should().Throw<InvalidOperationException>().WithMessage("Unsupported operator LessThan for filtering data");
        lessThanOrEqualAct.Should().Throw<InvalidOperationException>().WithMessage("Unsupported operator LessThanOrEqual for filtering data");
    }
}
