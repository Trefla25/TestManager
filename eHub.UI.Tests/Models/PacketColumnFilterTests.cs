using eHub.Contracts;
using eHub.UI.Models;
using FluentAssertions;
using MudBlazor;

namespace eHub.UI.Tests.Models;

public class PacketColumnFilterTests
{
    [Fact]
    public void OperatorEnabled_WhenColumnNotNull_ReturnsTrue()
    {
        // Arrange
        var filter = new PacketColumnFilter { ColumnName = "TestColumn" };
        
        // Assert
        filter.OperatorEnabled.Should().BeTrue();
    }

    [Fact]
    public void OperatorEnabled_WhenColumnNull_ReturnsFalse()
    {
        // Arrange
        var filter = new PacketColumnFilter();
        
        // Assert
        filter.OperatorEnabled.Should().BeFalse();
    }

    [Fact]
    public void ValueEnabled_WhenColumnNotNullAndOperatorNotEmpty_ReturnsTrue()
    {
        // Arrange
        var filter = new PacketColumnFilter
        {
            ColumnName = "TestColumn",
            Operator = "Equals"
        };
        
        // Assert
        filter.ValueEnabled.Should().BeTrue();
    }

    [Fact]
    public void ValueEnabled_WhenColumnNull_ReturnsFalse()
    {
        // Arrange
        var filter = new PacketColumnFilter();
        
        // Assert
        filter.ValueEnabled.Should().BeFalse();
    }

    [Fact]
    public void ValueEnabled_WhenOperatorNull_ReturnsFalse()
    {
        // Arrange
        var filter = new PacketColumnFilter
        {
            ColumnName = "TestColumn",
            Operator = null
        };
        
        // Assert
        filter.ValueEnabled.Should().BeFalse();
    }

    [Fact]
    public void ValueEnabled_WhenOperatorEmptyOrNotEmpty_ReturnsFalse()
    {
        // Arrange
        var filterEmpty = new PacketColumnFilter
        {
            ColumnName = "TestColumn",
            Operator = FilterOperator.Number.Empty
        };
        
        var filterNotEmpty = new PacketColumnFilter
        {
            ColumnName = "TestColumn",
            Operator = FilterOperator.Number.NotEmpty
        };
        
        // Assert
        filterEmpty.ValueEnabled.Should().BeFalse();
        filterNotEmpty.ValueEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsValidFilter_WhenValidInput_ReturnsTrue()
    {
        // Arrange
        var filter = new PacketColumnFilter { ColumnName = "TestColumn", Operator = "Equals", Value = "123" };
        
        // Assert
        filter.IsValidFilter().Should().BeTrue();
    }

    [Fact]
    public void IsValidFilter_WhenOperatorEmptyOrNotEmpty_ReturnsTrue()
    {
        // Arrange
        var filter1 = new PacketColumnFilter { ColumnName = "TestColumn", Operator = FilterOperator.Number.NotEmpty };
        var filter2 = new PacketColumnFilter { ColumnName = "TestColumn", Operator = FilterOperator.Number.Empty };
        
        // Assert
        filter1.IsValidFilter().Should().BeTrue();
        filter2.IsValidFilter().Should().BeTrue();
    }

    [Fact]
    public void IsValidFilter_WhenOneNull_ReturnsFalse()
    {
        // Arrange
        var filter1 = new PacketColumnFilter { Operator = FilterOperator.Number.Equal, Value = "123" };
        var filter2 = new PacketColumnFilter { ColumnName = "TestColumn", Value = "123" };
        var filter3 = new PacketColumnFilter { ColumnName = "TestColumn", Operator = "Equals" };
        
        // Assert
        filter1.IsValidFilter().Should().BeFalse();
        filter2.IsValidFilter().Should().BeFalse();
        filter3.IsValidFilter().Should().BeFalse();
    }
    
    [Fact]
    public void ToColumnFilterDto_ForValidInput_ReturnsDto()
    {
        // Arrange
        var filter = new PacketColumnFilter { ColumnName = "TestColumn", Operator = FilterOperator.Number.Equal, Value = "123" };
        
        // Act
        var dto = filter.ToColumnFilterDto();
        
        // Assert
        dto.Should().NotBeNull();
        dto.ColumnName.Should().Be("TestColumn");
        dto.Operator.Should().Be(ColumnFilterOperator.Equal);
        dto.Value.Should().Be("123");
    }


    [Fact]
    public void ToColumnFilterDto_WhenColumnNull_ThrowsException()
    {
        // Arrange
        var filter = new PacketColumnFilter { Operator = "Equals", Value = "123" };
        
        // Act
        Action act = () => filter.ToColumnFilterDto();
        
        // Assert
        act.Should().Throw<Exception>().WithMessage("Column Name is null");
    }

    [Fact]
    public void ToColumnFilterDto_WhenOperatorNull_ThrowsException()
    {
        // Arrange
        var filter = new PacketColumnFilter { ColumnName = "TestColumn", Value = "123" };
        
        // Act
        Action act = () => filter.ToColumnFilterDto();
        
        // Assert
        act.Should().Throw<Exception>().WithMessage("Operator is null");
    }
}
