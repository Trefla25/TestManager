using eHub.Contracts;
using eHub.UI.Models;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor;

namespace eHub.UI.Tests.Models;

[TestClass]
public class PacketColumnFilterTests
{
    [TestMethod]
    public void OperatorEnabled_WhenColumnNotNull_ReturnsTrue()
    {
        var filter = new PacketColumnFilter { ColumnName = "TestColumn" };

        filter.OperatorEnabled.Should().BeTrue();
    }

    [TestMethod]
    public void OperatorEnabled_WhenColumnNull_ReturnsFalse()
    {
        var filter = new PacketColumnFilter();

        filter.OperatorEnabled.Should().BeFalse();
    }

    [TestMethod]
    public void ValueEnabled_WhenColumnNotNullAndOperatorNotEmpty_ReturnsTrue()
    {
        var filter = new PacketColumnFilter
        {
            ColumnName = "TestColumn",
            Operator = "Equals"
        };

        filter.ValueEnabled.Should().BeTrue();
    }

    [TestMethod]
    public void ValueEnabled_WhenColumnNull_ReturnsFalse()
    {
        var filter = new PacketColumnFilter();

        filter.ValueEnabled.Should().BeFalse();
    }

    [TestMethod]
    public void ValueEnabled_WhenOperatorNull_ReturnsFalse()
    {
        var filter = new PacketColumnFilter
        {
            ColumnName = "TestColumn",
            Operator = null
        };

        filter.ValueEnabled.Should().BeFalse();
    }

    [TestMethod]
    public void ValueEnabled_WhenOperatorEmptyOrNotEmpty_ReturnsFalse()
    {
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

        filterEmpty.ValueEnabled.Should().BeFalse();
        filterNotEmpty.ValueEnabled.Should().BeFalse();
    }

    [TestMethod]
    public void IsValidFilter_WhenValidInput_ReturnsTrue()
    {
        var filter = new PacketColumnFilter { ColumnName = "TestColumn", Operator = "Equals", Value = "123" };

        filter.IsValidFilter().Should().BeTrue();
    }

    [TestMethod]
    public void IsValidFilter_WhenOperatorEmptyOrNotEmpty_ReturnsTrue()
    {
        var filter1 = new PacketColumnFilter { ColumnName = "TestColumn", Operator = FilterOperator.Number.NotEmpty };
        var filter2 = new PacketColumnFilter { ColumnName = "TestColumn", Operator = FilterOperator.Number.Empty };

        filter1.IsValidFilter().Should().BeTrue();
        filter2.IsValidFilter().Should().BeTrue();
    }

    [TestMethod]
    public void IsValidFilter_WhenOneNull_ReturnsFalse()
    {
        var filter1 = new PacketColumnFilter { Operator = FilterOperator.Number.Equal, Value = "123" };
        var filter2 = new PacketColumnFilter { ColumnName = "TestColumn", Value = "123" };
        var filter3 = new PacketColumnFilter { ColumnName = "TestColumn", Operator = "Equals" };

        filter1.IsValidFilter().Should().BeFalse();
        filter2.IsValidFilter().Should().BeFalse();
        filter3.IsValidFilter().Should().BeFalse();
    }


    [TestMethod]
    public void ToColumnFilterDto_ForValidInput_ReturnsDto()
    {
        var filter = new PacketColumnFilter { ColumnName = "TestColumn", Operator = FilterOperator.Number.Equal, Value = "123" };

        var dto = filter.ToColumnFilterDto();

        dto.Should().NotBeNull();
        dto.ColumnName.Should().Be("TestColumn");
        dto.Operator.Should().Be(ColumnFilterOperator.Equal);
        dto.Value.Should().Be("123");
    }


    [TestMethod]
    public void ToColumnFilterDto_WhenColumnNull_ThrowsException()
    {
        var filter = new PacketColumnFilter { Operator = "Equals", Value = "123" };

        Action act = () => filter.ToColumnFilterDto();
        act.Should().Throw<Exception>().WithMessage("Column Name is null");
    }

    [TestMethod]
    public void ToColumnFilterDto_WhenOperatorNull_ThrowsException()
    {
        var filter = new PacketColumnFilter { ColumnName = "TestColumn", Value = "123" };

        Action act = () => filter.ToColumnFilterDto();
        act.Should().Throw<Exception>().WithMessage("Operator is null");
    }

}
