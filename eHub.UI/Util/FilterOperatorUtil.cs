using eHub.Contracts;
using MudBlazor;

namespace eHub.UI.Util;

public static class FilterOperatorUtil
{
    public static string[] GetNumericOperators() => [
        FilterOperator.Number.Equal,
        FilterOperator.Number.NotEqual,
        FilterOperator.Number.GreaterThan,
        FilterOperator.Number.GreaterThanOrEqual,
        FilterOperator.Number.LessThan,
        FilterOperator.Number.LessThanOrEqual,
        FilterOperator.Number.Empty,
        FilterOperator.Number.NotEmpty
    ];

    public static string[] GetStringOperators() => [
        FilterOperator.String.Contains,
        FilterOperator.String.NotContains,
        FilterOperator.String.Equal,
        FilterOperator.String.NotEqual,
        FilterOperator.String.StartsWith,
        FilterOperator.String.EndsWith,
        FilterOperator.String.Empty,
        FilterOperator.String.NotEmpty
    ];

    public static string[] GetDateTimeOperators() => [
        FilterOperator.DateTime.After,
        FilterOperator.DateTime.OnOrAfter,
        FilterOperator.DateTime.Before,
        FilterOperator.DateTime.OnOrBefore,
        FilterOperator.DateTime.Empty,
        FilterOperator.DateTime.NotEmpty,
        FilterOperator.DateTime.Is,
        FilterOperator.DateTime.IsNot
    ];

    public static string[] GetEnumOperators() => [
        FilterOperator.Enum.Is,
        FilterOperator.Enum.IsNot
    ];

    public static string[] GetBooleanOperators() => [
        FilterOperator.Boolean.Is
    ];

    public static string[] GetOperators(FieldType fieldType)
    {
        if (fieldType.IsString)
        {
            return GetStringOperators();
        }
        if (fieldType.IsNumber)
        {
            return GetNumericOperators();
        }
        if (fieldType.IsDateTime)
        {
            return GetDateTimeOperators();
        }
        if (fieldType.IsEnum)
        {
            return GetEnumOperators();
        }
        if (fieldType.IsBoolean)
        {
            return GetBooleanOperators();
        }
        return [];
    }

    public static ColumnFilterOperator GetColumnFilterOperator(string? op)
        => op switch
        {
            FilterOperator.Number.Equal => ColumnFilterOperator.Equal,
            FilterOperator.Number.NotEqual => ColumnFilterOperator.NotEqual,
            FilterOperator.Number.GreaterThan => ColumnFilterOperator.GreaterThan,
            FilterOperator.Number.GreaterThanOrEqual => ColumnFilterOperator.GreaterThanOrEqual,
            FilterOperator.Number.LessThan => ColumnFilterOperator.LessThan,
            FilterOperator.Number.LessThanOrEqual => ColumnFilterOperator.LessThanOrEqual,
            FilterOperator.Number.Empty => ColumnFilterOperator.Empty,
            FilterOperator.Number.NotEmpty => ColumnFilterOperator.NotEmpty,

            FilterOperator.String.Contains => ColumnFilterOperator.Contains,
            FilterOperator.String.NotContains => ColumnFilterOperator.NotContains,
            FilterOperator.String.Equal => ColumnFilterOperator.Equal,
            FilterOperator.String.NotEqual => ColumnFilterOperator.NotEqual,
            FilterOperator.String.StartsWith => ColumnFilterOperator.StartsWith,
            FilterOperator.String.EndsWith => ColumnFilterOperator.EndsWith,

            FilterOperator.DateTime.Is => ColumnFilterOperator.Equal,
            FilterOperator.DateTime.IsNot => ColumnFilterOperator.NotEqual,
            FilterOperator.DateTime.After => ColumnFilterOperator.GreaterThan,
            FilterOperator.DateTime.OnOrAfter => ColumnFilterOperator.GreaterThanOrEqual,
            FilterOperator.DateTime.Before => ColumnFilterOperator.LessThan,
            FilterOperator.DateTime.OnOrBefore => ColumnFilterOperator.LessThanOrEqual,
            _ => throw new Exception($"Invalid Operator: {op}")
        };
}
