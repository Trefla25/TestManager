using eHub.Contracts.UIConfig;
using eHub.UI.Models;
using eHub.UI.Util;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using System.Collections.Immutable;
using System.Globalization;
using MudBlazor;
using Microsoft.Extensions.Logging;

namespace eHub.UI.Services;

public class HistoryStateUrlService(ILogger<HistoryStateUrlService> logger, IConnectorRegistry connectorRegistry)
{
    private readonly ILogger<HistoryStateUrlService> _logger = logger;
    private readonly IConnectorRegistry _connectorRegistry = connectorRegistry;

    public HistoryState GetStateFromUrl(string url)
    {
        HistoryState state = new HistoryState();

        var uri = new Uri(url);
        var query = QueryHelpers.ParseQuery(uri.Query);
        var normalizedQuery = new Dictionary<string, StringValues>(query, StringComparer.OrdinalIgnoreCase);

        if(normalizedQuery.TryGetValue("Connector", out var connectorName) && _connectorRegistry.ActiveConnectors.Keys.FirstOrDefault(c => c.ConnectorKey == connectorName) is { ConnectorKey: not null } connector)
        {
            state.Connector = connector;
        }

        if (normalizedQuery.TryGetValue("View", out var viewValue) && int.TryParse(viewValue, out var viewIndex))
        {
            state.View = viewIndex;
        }

        if (normalizedQuery.TryGetValue("LiveMode", out var liveModeValue) && bool.TryParse(liveModeValue, out var liveMode))
        {
            state.LiveMode = liveMode;
        }

        if (normalizedQuery.TryGetValue("StartDate", out var startDateValue) && DateTime.TryParseExact(startDateValue, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate))
        {
            state.StartDateTime = startDate;

            if (normalizedQuery.TryGetValue("StartTime", out var startTimeValue) && DateTime.TryParseExact(startTimeValue, "HH-mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startTime))
            {
                state.StartDateTime = state.StartDateTime.Value.Add(startTime.TimeOfDay);
            }
        }

        if (normalizedQuery.TryGetValue("EndDate", out var endDateValue) && DateTime.TryParseExact(endDateValue, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDate))
        {
            state.EndDateTime = endDate;

            if (normalizedQuery.TryGetValue("EndTime", out var endTimeValue) && DateTime.TryParseExact(endTimeValue, "HH-mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endTime))
            {
                state.EndDateTime = state.EndDateTime.Value.Add(endTime.TimeOfDay);
            }
        }

        if (normalizedQuery.TryGetValue("Column", out var filterValues))
        {
            var filters = new List<PacketColumnFilter>();

            foreach (var filterString in filterValues)
            {
                if (string.IsNullOrWhiteSpace(filterString))
                {
                    _logger.LogWarning("Invalid URL filter: the 'Column' parameter is empty.");
                    continue;
                }

                var filterParts = filterString.Split('-');
                if (filterParts.Length != 3)
                {
                    _logger.LogWarning("Invalid URL filter: '{FilterString}' does not match the expected format (column-operator-value).", filterString);
                    continue;
                }

                var columnName = PacketDto.PacketColumns.FirstOrDefault(c => string.Equals(c, filterParts[0], StringComparison.OrdinalIgnoreCase));

                if (columnName == null)
                {
                    _logger.LogWarning("Invalid URL filter: '{FilterString}' contains an invalid column name '{ColumnPart}'.", filterString, filterParts[0]);
                    continue;
                }

                var columnType = typeof(PacketDto).GetProperty(columnName)!.PropertyType;
                var fieldType = FieldType.Identify(columnType);
                var validOperators = FilterOperatorUtil.GetOperators(fieldType);
                var columnOperator = filterParts[1].ToLowerInvariant();

                if (!validOperators.Contains(columnOperator))
                {
                    _logger.LogWarning("Invalid URL filter: '{FilterString}' uses an operator '{Operator}' which is not valid for field type '{FieldType}'.", filterString, columnOperator, fieldType);
                    continue;
                }

                var columnValue = filterParts[2];

                filters.Add(new PacketColumnFilter
                {
                    ColumnName = columnName,
                    Operator = columnOperator,
                    Value = columnValue
                });
            }

            state.ColumnFilters = [.. filters];
        }

        return state;
    }

    public static string AppendStateQuery(string url, HistoryState state)
    {
        if (url.Contains('?'))
        {
            url = url.Remove(url.IndexOf('?'));
        }
        
        var queryParams = new List<KeyValuePair<string, StringValues>>();

        if (state.Connector is { } connector)
        {
            queryParams.Add(new("Connector", connector.ConnectorKey));
        }

        if (state.View != 0)
        {
            queryParams.Add(new("View", state.View.ToString()));
        }

        queryParams.Add(new("LiveMode", state.LiveMode.ToString()));

        if (state.StartDateTime is { } startDateTime)
        {
            queryParams.Add(new("StartDate", startDateTime.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)));
            queryParams.Add(new("StartTime", startDateTime.ToString("HH-mm", CultureInfo.InvariantCulture)));
        }

        if (state.EndDateTime is { } endDateTime)
        {
            queryParams.Add(new("EndDate", endDateTime.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)));
            queryParams.Add(new("EndTime", endDateTime.ToString("HH-mm", CultureInfo.InvariantCulture)));
        }

        if (state.ColumnFilters.Length != 0)
        {
            var filterValues = new List<string>();
            foreach (var filter in state.ColumnFilters)
            {
                var filterString = $"{filter.ColumnName}-{filter.Operator}-{filter.Value}";
                filterValues.Add(filterString);
            }

            queryParams.Add(new("Column", new StringValues([.. filterValues])));
        }

        return QueryHelpers.AddQueryString(url, queryParams);
    }
}
