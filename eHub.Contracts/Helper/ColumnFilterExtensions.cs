using eHub.Contracts.UIConfig;

namespace eHub.Contracts.Helper;

public static class ColumnFilterExtensions
{
    public static bool Matches(this IReadOnlyCollection<ColumnFilterDto> dataFilters, string? data)
    {
        foreach (var dataFilter in dataFilters)
        {
            var isFiltered = dataFilter.Operator switch
            {
                ColumnFilterOperator.Equal => data == dataFilter.Value,
                ColumnFilterOperator.NotEqual => data != dataFilter.Value,
                ColumnFilterOperator.Contains => dataFilter.Value != null && data != null && data.Contains(dataFilter.Value),
                ColumnFilterOperator.NotContains => dataFilter.Value != null && data != null && !data.Contains(dataFilter.Value),
                ColumnFilterOperator.StartsWith => dataFilter.Value != null && data != null && data.StartsWith(dataFilter.Value),
                ColumnFilterOperator.EndsWith => dataFilter.Value != null && data != null && data.EndsWith(dataFilter.Value),
                ColumnFilterOperator.Empty => string.IsNullOrEmpty(data),
                ColumnFilterOperator.NotEmpty => !string.IsNullOrEmpty(data),
                _ => throw new InvalidOperationException($"Unsupported operator {dataFilter.Operator} for filtering data")
            };

            if (!isFiltered)
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsNumericFilter(this ColumnFilterDto columnFilter) =>
        columnFilter.ColumnName == nameof(PacketDto.Id) ||
        columnFilter.ColumnName == nameof(PacketDto.ParentId) ||
        columnFilter.ColumnName == nameof(PacketDto.RetryCount);

    public static bool IsDateTimeFilter(this ColumnFilterDto columnFilter) =>
        columnFilter.ColumnName == nameof(PacketDto.DateCreated) ||
        columnFilter.ColumnName == nameof(PacketDto.DateChanged);

    public static bool IsStringFilter(this ColumnFilterDto columnFilter) =>
        columnFilter.ColumnName == nameof(PacketDto.Channel) ||
        columnFilter.ColumnName == nameof(PacketDto.DynamicField) ||
        columnFilter.ColumnName == nameof(PacketDto.Metadata);

    public static bool IsStatusFilter(this ColumnFilterDto columnFilter) =>
        columnFilter.ColumnName == nameof(PacketDto.Status);

    public static bool IsDataFilter(this ColumnFilterDto columnFilter) =>
        columnFilter.ColumnName == nameof(PacketDto.Data) ||
        columnFilter.ColumnName == nameof(PacketDto.PreviewData) ||
        columnFilter.ColumnName == nameof(PacketDto.BinaryData);
}
