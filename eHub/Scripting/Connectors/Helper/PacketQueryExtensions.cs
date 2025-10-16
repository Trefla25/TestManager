using System.Linq.Expressions;
using eHub.Contracts;
using eHub.Contracts.Helper;
using eHub.Database.Models;
using eHub.PlugIn;

namespace eHub.Scripting.Connectors.Helper;

public static class PacketQueryExtensions
{
    public static IQueryable<Packet> ApplyFilterDto(this IQueryable<Packet> query, PacketRequestDto filter)
    {
        if (filter.DateTimeStart is { } start)
        {
            query = query.Where(p => p.DateCreated >= start);
        }
        if (filter.DateTimeEnd is { } end)
        {
            query = query.Where(p => p.DateCreated <= end);
        }
        if (filter.MinId == filter.MaxId && filter.MinId is { } id)
        {
            query = query.Where(p => p.Id == id);
        }
        else
        {
            if (filter.MinId is { } minId)
            {
                query = query.Where(p => p.Id >= minId);
            }
            if (filter.MaxId is { } maxId)
            {
                query = query.Where(p => p.Id <= maxId);
            }
        }

        if (filter.IsEmpty)
        {
            // No need to apply more filters
            return query;
        }

        if (filter.Channels is { } channels)
        {
            query = query.Where(p => channels.Contains(p.Channel));
        }

        if (filter.ColumnFilters is { } columnFilters && columnFilters.Length > 0)
        {
            foreach (var columnFilter in columnFilters)
            {
                query = query.ApplyColumnFilter(columnFilter);
            }
        }

        return query;
    }

    public static IQueryable<Packet> ApplyColumnFilter(this IQueryable<Packet> query, ColumnFilterDto columnFilter)
    {
        if (columnFilter.IsDataFilter())
        {
            // The filter by Data is done later since it needs to be converted to UI data
            return query;
        }

        if (typeof(Packet).GetProperty(columnFilter.ColumnName) == null)
        {
            // Only apply the filter if the column name is valid
            return query;
        }

        var packetParameterExpression = Expression.Parameter(typeof(Packet), "p");

        var packetColumnExpression = Expression.Property(packetParameterExpression, columnFilter.ColumnName);

        Expression valueExpression;
        if (columnFilter.IsNumericFilter() && long.TryParse(columnFilter.Value, out var numericValue))
        {
            var targetType = Nullable.GetUnderlyingType(packetColumnExpression.Type) ?? packetColumnExpression.Type;
            var convertedValue = Convert.ChangeType(numericValue, targetType);
            valueExpression = Expression.Constant(convertedValue, packetColumnExpression.Type);
        }
        else if (columnFilter.IsDateTimeFilter() && DateTime.TryParse(columnFilter.Value, out var dateTimeValue))
        {
            valueExpression = Expression.Constant(dateTimeValue, packetColumnExpression.Type);
        }
        else if (columnFilter.IsStringFilter())
        {
            valueExpression = Expression.Constant(columnFilter.Value, packetColumnExpression.Type);
        }
        else if (columnFilter.IsStatusFilter() && Enum.TryParse(typeof(PacketStatus), columnFilter.Value, out var statusValue))
        {
            valueExpression = Expression.Constant(statusValue, packetColumnExpression.Type);
        }
        else
        {
            valueExpression = Expression.Default(packetColumnExpression.Type);
        }

        var condition = columnFilter.Operator switch
        {
            ColumnFilterOperator.Equal => Expression.Equal(packetColumnExpression, valueExpression),
            ColumnFilterOperator.NotEqual => Expression.NotEqual(packetColumnExpression, valueExpression),

            ColumnFilterOperator.GreaterThan when columnFilter.IsNumericFilter() || columnFilter.IsDateTimeFilter() => Expression.GreaterThan(packetColumnExpression, valueExpression),
            ColumnFilterOperator.GreaterThan => throw new InvalidOperationException($"{ColumnFilterOperator.GreaterThan} operator is not supported for type {packetColumnExpression.Type}"),

            ColumnFilterOperator.GreaterThanOrEqual when columnFilter.IsNumericFilter() || columnFilter.IsDateTimeFilter() => Expression.GreaterThanOrEqual(packetColumnExpression, valueExpression),
            ColumnFilterOperator.GreaterThanOrEqual => throw new InvalidOperationException($"{ColumnFilterOperator.GreaterThanOrEqual} operator is not supported for type {packetColumnExpression.Type}"),

            ColumnFilterOperator.LessThan when columnFilter.IsNumericFilter() || columnFilter.IsDateTimeFilter() => Expression.LessThan(packetColumnExpression, valueExpression),
            ColumnFilterOperator.LessThan => throw new InvalidOperationException($"{ColumnFilterOperator.LessThan} operator is not supported for type {packetColumnExpression.Type}"),

            ColumnFilterOperator.LessThanOrEqual when columnFilter.IsNumericFilter() || columnFilter.IsDateTimeFilter() => Expression.LessThanOrEqual(packetColumnExpression, valueExpression),
            ColumnFilterOperator.LessThanOrEqual => throw new InvalidOperationException($"{ColumnFilterOperator.LessThanOrEqual} operator is not supported for type {packetColumnExpression.Type}"),

            ColumnFilterOperator.Empty when columnFilter.IsStringFilter() => Expression.OrElse(Expression.Equal(packetColumnExpression, Expression.Constant(null, typeof(string))), Expression.Equal(packetColumnExpression, Expression.Constant(string.Empty))),
            ColumnFilterOperator.Empty when columnFilter.IsNumericFilter() || columnFilter.IsDateTimeFilter() => Expression.Equal(packetColumnExpression, valueExpression),
            ColumnFilterOperator.Empty => throw new InvalidOperationException($"{ColumnFilterOperator.Empty} operator is not supported for type {packetColumnExpression.Type}"),

            ColumnFilterOperator.NotEmpty when columnFilter.IsStringFilter() => Expression.AndAlso(Expression.NotEqual(packetColumnExpression, Expression.Constant(null, typeof(string))), Expression.NotEqual(packetColumnExpression, Expression.Constant(string.Empty))),
            ColumnFilterOperator.NotEmpty when columnFilter.IsNumericFilter() || columnFilter.IsDateTimeFilter() => Expression.NotEqual(packetColumnExpression, valueExpression),
            ColumnFilterOperator.NotEmpty => throw new InvalidOperationException($"{ColumnFilterOperator.NotEmpty} operator is not supported for type {packetColumnExpression.Type}"),

            ColumnFilterOperator.Contains when columnFilter.IsStringFilter() => Expression.Equal(Expression.Call(packetColumnExpression, typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!, valueExpression), Expression.Constant(true)),
            ColumnFilterOperator.Contains => throw new InvalidOperationException($"{ColumnFilterOperator.Contains} operator is not supported for type {packetColumnExpression.Type}"),

            ColumnFilterOperator.NotContains when columnFilter.IsStringFilter() => Expression.Equal(Expression.Call(packetColumnExpression, typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!, valueExpression), Expression.Constant(false)),
            ColumnFilterOperator.NotContains => throw new InvalidOperationException($"{ColumnFilterOperator.NotContains} operator is not supported for type {packetColumnExpression.Type}"),

            ColumnFilterOperator.StartsWith when columnFilter.IsStringFilter() => Expression.Equal(Expression.Call(packetColumnExpression, typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!, valueExpression), Expression.Constant(true)),
            ColumnFilterOperator.StartsWith => throw new InvalidOperationException($"{ColumnFilterOperator.StartsWith} operator is not supported for type {packetColumnExpression.Type}"),

            ColumnFilterOperator.EndsWith when columnFilter.IsStringFilter() => Expression.Equal(Expression.Call(packetColumnExpression, typeof(string).GetMethod(nameof(string.EndsWith), [typeof(string)])!, valueExpression), Expression.Constant(true)),
            ColumnFilterOperator.EndsWith => throw new InvalidOperationException($"{ColumnFilterOperator.EndsWith} operator is not supported for type {packetColumnExpression.Type}"),

            _ => throw new InvalidOperationException($"Operator {columnFilter.Operator} is not supported")

        };

        var lambdaExpression = Expression.Lambda<Func<Packet, bool>>(condition, packetParameterExpression);
        return query.Where(lambdaExpression);
    }
}
