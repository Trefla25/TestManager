using System.Collections.Frozen;
using System.Collections.Immutable;
using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eHub.PlugIn.UI;
using eHub.UI.Services;
using Microsoft.Extensions.Logging;

namespace eHub.UI.State;

internal class CombinedConnectorContext(
    ConnectorIdentifier connectorIdentifier,
    ConnectorUiData connectorUiData,
    IConnectorRegistry connectorRegistry,
    IConnectorContextProvider contextProvider,
    ILogger<CombinedConnectorContext> logger)
    : CombinedContextManager<IConnectorContext>(
        connectorIdentifier,
        connectorUiData,
        connectorRegistry,
        logger), IConnectorContext
{
    private readonly IConnectorContextProvider _contextProvider = contextProvider;
    private IReadOnlyDictionary<string, Type>? _customFilters;

    public event OnConnectorPacketsChangedDelegate? OnConnectorPacketsChanged;

    protected override IConnectorContext GetContext(ConnectorIdentifier identifier)
        => _contextProvider.GetConnectorContext(identifier);

    protected override void OnInnerConnectorPacketsChanged(ConnectorIdentifier connectorIdentifier, ConnectorPacketsChangedDto changed)
        => OnConnectorPacketsChanged?.Invoke(ConnectorIdentifier, changed);

    public Task RunAsync(CancellationToken cancellationToken)
    {
        var customFilters = new Dictionary<string, Type>();
        foreach (var connector in Connectors.Values)
        {
            connector.GetCustomFilters().ForEach(filter => customFilters[filter.Key] = filter.Value);
        }

        _customFilters = customFilters.AsReadOnly();

        return Task.CompletedTask;
    }

    public IReadOnlyDictionary<string, Type> GetCustomFilters() => _customFilters ?? FrozenDictionary<string, Type>.Empty;

    public UIViewConfig GetUIViewConfig() => ConnectorUiData.UIViewConfig;

    public async Task DeletePacketsAsync(HashSet<PacketDto> packets)
    {
        var groups = packets.GroupBy(p => p.ConnectorName);

        foreach (var group in groups)
        {
            if (Connectors.TryGetValue(group.Key, out var connector))
            {
                await connector.DeletePacketsAsync([.. group]);
            }
        }
    }

    public async Task<bool> ImportPacketsAsync(ConnectorPacketsExportDto importData, bool forceImport)
    {
        var success = false;
        var groups = importData.Packets.GroupBy(p => p.ConnectorName);

        foreach (var group in groups)
        {
            if (!Connectors.TryGetValue(group.Key, out var connector))
            {
                continue;
            }

            success |= await connector.ImportPacketsAsync(importData, forceImport);

            if (!success)
            {
                break;
            }
        }

        return success;
    }

    public async Task ResendPacketsAsync(HashSet<PacketDto> packets)
    {
        var groups = packets.GroupBy(p => p.ConnectorName);

        foreach (var group in groups)
        {
            if (Connectors.TryGetValue(group.Key, out var connector))
            {
                await connector.ResendPacketsAsync([.. group]);
            }
        }
    }

    public async Task StopPacketsAsync(HashSet<PacketDto> packets)
    {
        var groups = packets.GroupBy(p => p.ConnectorName);

        foreach (var group in groups)
        {
            if (Connectors.TryGetValue(group.Key, out var connector))
            {
                await connector.StopPacketsAsync([.. group]);
            }
        }
    }
}
