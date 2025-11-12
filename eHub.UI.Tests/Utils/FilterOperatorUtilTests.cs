using eHub.Contracts;
using eHub.PlugIn;
using eHub.UI.Util;
using FluentAssertions;
using MudBlazor;

namespace eHub.UI.Tests.Utils;

public class FilterOperatorUtilTests
{
    [Fact]
    public void GetNumericOperators_WhenCalled_ReturnsExpectedOperators()
    {
        // Arrange
        var expected = typeof(FilterOperator.Number).GetFields()
            .Select(f => f.GetRawConstantValue()?.ToString())
            .ToArray();

        // Act
        var result = FilterOperatorUtil.GetNumericOperators();

        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void GetStringOperators_WhenCalled_ReturnsExpectedOperators()
    {
        // Arrange
        var expected = typeof(FilterOperator.String).GetFields()
            .Select(f => f.GetRawConstantValue()?.ToString())
            .ToArray();

        // Act
        var result = FilterOperatorUtil.GetStringOperators();

        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void GetDateTimeOperators_WhenCalled_ReturnsExpectedOperators()
    {
        // Arrange
        var expected = typeof(FilterOperator.DateTime).GetFields()
            .Select(f => f.GetRawConstantValue()?.ToString())
            .ToArray();

        // Act
        var result = FilterOperatorUtil.GetDateTimeOperators();

        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void GetEnumOperators_WhenCalled_ReturnsExpectedOperators()
    {
        // Arrange
        var expected = typeof(FilterOperator.Enum).GetFields()
            .Select(f => f.GetRawConstantValue()?.ToString())
            .ToArray();

        // Act
        var result = FilterOperatorUtil.GetEnumOperators();

        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData(typeof(long), typeof(FilterOperator.Number))]
    [InlineData(typeof(string), typeof(FilterOperator.String))]
    [InlineData(typeof(DateTime), typeof(FilterOperator.DateTime))]
    [InlineData(typeof(PacketStatus), typeof(FilterOperator.Enum))]
    public void GetOperators_WhenGivenFieldType_ReturnsCorrectOperators(Type type, Type operatorConstantsType)
    {
        // Arrange
        var fieldType = FieldType.Identify(type);
        var expectedOperators = operatorConstantsType.GetFields()
            .Select(f => f.GetRawConstantValue()?.ToString())
            .ToArray();

        // Act
        var operators = FilterOperatorUtil.GetOperators(fieldType);

        // Assert
        operators.Should().BeEquivalentTo(expectedOperators);
    }

    [Theory]
    [InlineData(FilterOperator.Number.Equal, ColumnFilterOperator.Equal)]
    [InlineData(FilterOperator.Number.NotEqual, ColumnFilterOperator.NotEqual)]
    [InlineData(FilterOperator.Number.GreaterThan, ColumnFilterOperator.GreaterThan)]
    [InlineData(FilterOperator.Number.GreaterThanOrEqual, ColumnFilterOperator.GreaterThanOrEqual)]
    [InlineData(FilterOperator.Number.LessThan, ColumnFilterOperator.LessThan)]
    [InlineData(FilterOperator.Number.LessThanOrEqual, ColumnFilterOperator.LessThanOrEqual)]
    [InlineData(FilterOperator.Number.Empty, ColumnFilterOperator.Empty)]
    [InlineData(FilterOperator.Number.NotEmpty, ColumnFilterOperator.NotEmpty)]
    [InlineData(FilterOperator.String.Contains, ColumnFilterOperator.Contains)]
    [InlineData(FilterOperator.String.NotContains, ColumnFilterOperator.NotContains)]
    [InlineData(FilterOperator.String.Equal, ColumnFilterOperator.Equal)]
    [InlineData(FilterOperator.String.NotEqual, ColumnFilterOperator.NotEqual)]
    [InlineData(FilterOperator.String.StartsWith, ColumnFilterOperator.StartsWith)]
    [InlineData(FilterOperator.String.EndsWith, ColumnFilterOperator.EndsWith)]
    [InlineData(FilterOperator.DateTime.Is, ColumnFilterOperator.Equal)]
    [InlineData(FilterOperator.DateTime.IsNot, ColumnFilterOperator.NotEqual)]
    [InlineData(FilterOperator.DateTime.After, ColumnFilterOperator.GreaterThan)]
    [InlineData(FilterOperator.DateTime.OnOrAfter, ColumnFilterOperator.GreaterThanOrEqual)]
    [InlineData(FilterOperator.DateTime.Before, ColumnFilterOperator.LessThan)]
    [InlineData(FilterOperator.DateTime.OnOrBefore, ColumnFilterOperator.LessThanOrEqual)]
    public void GetColumnFilterOperator_WithValidConstant_MapsCorrectly(string given, ColumnFilterOperator expected)
    {
        FilterOperatorUtil.GetColumnFilterOperator(given).Should().Be(expected);
    }

    [Fact]
    public void GetColumnFilterOperator_WithInvalidOperator_ThrowsException()
    {
        // Act
        var act = () => FilterOperatorUtil.GetColumnFilterOperator("invalid");

        // Assert
        act.Should().Throw<Exception>()
            .WithMessage("Invalid Operator: invalid");
    }
}