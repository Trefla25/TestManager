using eHub.Contracts;
using eHub.PlugIn;
using eHub.UI.Util;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor;

namespace eHub.UI.Tests.Utils;

[TestClass]
public class FilterOperatorUtilTests
{
    [TestMethod]
    public void GetNumericOperators_WhenCalled_ReturnsExpectedOperators()
    {
        var expected = typeof(FilterOperator.Number).GetFields()
            .Select(f => f.GetRawConstantValue()?.ToString())
            .ToArray();

        var result = FilterOperatorUtil.GetNumericOperators();

        result.Should().BeEquivalentTo(expected);
    }

    [TestMethod]
    public void GetStringOperators_WhenCalled_ReturnsExpectedOperators()
    {
        var expected = typeof(FilterOperator.String).GetFields()
            .Select(f => f.GetRawConstantValue()?.ToString())
            .ToArray();

        var result = FilterOperatorUtil.GetStringOperators();

        result.Should().BeEquivalentTo(expected);
    }

    [TestMethod]
    public void GetDateTimeOperators_WhenCalled_ReturnsExpectedOperators()
    {
        var expected = typeof(FilterOperator.DateTime).GetFields()
            .Select(f => f.GetRawConstantValue()?.ToString())
            .ToArray();

        var result = FilterOperatorUtil.GetDateTimeOperators();

        result.Should().BeEquivalentTo(expected);
    }

    [TestMethod]
    public void GetEnumOperators_WhenCalled_ReturnsExpectedOperators()
    {
        var expected = typeof(FilterOperator.Enum).GetFields()
            .Select(f => f.GetRawConstantValue()?.ToString())
            .ToArray();

        var result = FilterOperatorUtil.GetEnumOperators();

        result.Should().BeEquivalentTo(expected);
    }

    [DataTestMethod]
    [DataRow(typeof(long), typeof(FilterOperator.Number))]
    [DataRow(typeof(string), typeof(FilterOperator.String))]
    [DataRow(typeof(DateTime), typeof(FilterOperator.DateTime))]
    [DataRow(typeof(PacketStatus), typeof(FilterOperator.Enum))]
    public void GetOperators_WhenGivenFieldType_ReturnsCorrectOperators(Type type, Type operatorConstantsType)
    {
        var fieldType = FieldType.Identify(type);
        var expectedOperators = operatorConstantsType.GetFields()
            .Select(f => f.GetRawConstantValue()?.ToString())
            .ToArray();

        var operators = FilterOperatorUtil.GetOperators(fieldType);

        operators.Should().BeEquivalentTo(expectedOperators);
    }

    [DataTestMethod]
    [DataRow(FilterOperator.Number.Equal, ColumnFilterOperator.Equal)]
    [DataRow(FilterOperator.Number.NotEqual, ColumnFilterOperator.NotEqual)]
    [DataRow(FilterOperator.Number.GreaterThan, ColumnFilterOperator.GreaterThan)]
    [DataRow(FilterOperator.Number.GreaterThanOrEqual, ColumnFilterOperator.GreaterThanOrEqual)]
    [DataRow(FilterOperator.Number.LessThan, ColumnFilterOperator.LessThan)]
    [DataRow(FilterOperator.Number.LessThanOrEqual, ColumnFilterOperator.LessThanOrEqual)]
    [DataRow(FilterOperator.Number.Empty, ColumnFilterOperator.Empty)]
    [DataRow(FilterOperator.Number.NotEmpty, ColumnFilterOperator.NotEmpty)]
    [DataRow(FilterOperator.String.Contains, ColumnFilterOperator.Contains)]
    [DataRow(FilterOperator.String.NotContains, ColumnFilterOperator.NotContains)]
    [DataRow(FilterOperator.String.Equal, ColumnFilterOperator.Equal)]
    [DataRow(FilterOperator.String.NotEqual, ColumnFilterOperator.NotEqual)]
    [DataRow(FilterOperator.String.StartsWith, ColumnFilterOperator.StartsWith)]
    [DataRow(FilterOperator.String.EndsWith, ColumnFilterOperator.EndsWith)]
    [DataRow(FilterOperator.DateTime.Is, ColumnFilterOperator.Equal)]
    [DataRow(FilterOperator.DateTime.IsNot, ColumnFilterOperator.NotEqual)]
    [DataRow(FilterOperator.DateTime.After, ColumnFilterOperator.GreaterThan)]
    [DataRow(FilterOperator.DateTime.OnOrAfter, ColumnFilterOperator.GreaterThanOrEqual)]
    [DataRow(FilterOperator.DateTime.Before, ColumnFilterOperator.LessThan)]
    [DataRow(FilterOperator.DateTime.OnOrBefore, ColumnFilterOperator.LessThanOrEqual)]
    public void GetColumnFilterOperator_WithValidConstant_MapsCorrectly(string given, ColumnFilterOperator expected)
    {
        FilterOperatorUtil.GetColumnFilterOperator(given).Should().Be(expected);
    }


    [TestMethod]
    public void GetColumnFilterOperator_WithInvalidOperator_ThrowsException()
    {
        var act = () => FilterOperatorUtil.GetColumnFilterOperator("invalid");

        act.Should().Throw<Exception>()
            .WithMessage("Invalid Operator: invalid");
    }
}
