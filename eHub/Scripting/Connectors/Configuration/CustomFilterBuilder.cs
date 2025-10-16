using eHub.Contracts.UIConfig;
using eHub.PlugIn.UI;

namespace eHub.Scripting.Connectors.Configuration;

public class CustomFilterBuilder : ICustomFilterBuilder
{
    private readonly Dictionary<string, CustomFilterDefinition> _customFilters = [];

    public ICustomFilterBuilder AddCustomFilter<T>(string filterKey, string sqlExpression, Func<T?, T?>? valueBuilder = null) where T : IConvertible
    {
        if(string.IsNullOrWhiteSpace(filterKey))
        {
            throw new ArgumentException("Filter key cannot be null or whitespace.", nameof(filterKey));
        }

        if (PacketDto.PacketColumns.Contains(filterKey, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"The filter key '{filterKey}' is reserved as it matches an existing packet column.", nameof(filterKey));
        }

        if (string.IsNullOrWhiteSpace(sqlExpression))
        {
            throw new ArgumentException("SQL expression cannot be null or whitespace.", nameof(sqlExpression));
        }

        if(_customFilters.ContainsKey(filterKey))
        {
            throw new ArgumentException($"A filter with the key '{filterKey}' already exists.", nameof(filterKey));
        }

        var f = (object? o) => valueBuilder is null ? o : valueBuilder((T?)o);
        _customFilters.Add(filterKey, new(typeof(T), sqlExpression, f));

        return this;
    }

    public IReadOnlyDictionary<string, CustomFilterDefinition> GetCustomFilters()
    {
        return _customFilters.AsReadOnly();
    }
}

public record CustomFilterDefinition(Type Type, string SqlExpression, Func<object?, object?> ValueBuilder);
