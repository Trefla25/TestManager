using System.Collections.Frozen;
using eHub.Contracts;
using eHub.UI.Services;
using Microsoft.Extensions.Logging;

namespace eHub.UI.State;

internal abstract class CombinedContextManager<TContext> : IAsyncDisposable where TContext : IConnectorContext
{
    private readonly ILogger _logger;
    private readonly FrozenSet<string> _innerConnectorNames;
    private readonly IConnectorRegistry _connectorRegistry;

    protected ConnectorIdentifier ConnectorIdentifier { get; }
    protected ConnectorUiData ConnectorUiData { get; }
    protected Dictionary<string, TContext> Connectors { get; } = [];

    protected abstract TContext GetContext(ConnectorIdentifier identifier);
    protected abstract void OnInnerConnectorPacketsChanged(ConnectorIdentifier connectorIdentifier, ConnectorPacketsChangedDto changed);

    protected CombinedContextManager(
        ConnectorIdentifier connectorIdentifier,
        ConnectorUiData connectorUiData,
        IConnectorRegistry connectorRegistry,
        ILogger logger)
    {
        ConnectorIdentifier = connectorIdentifier;
        ConnectorUiData = connectorUiData;

        _logger = logger;
        _connectorRegistry = connectorRegistry;
        _innerConnectorNames = connectorUiData.UIViewConfig.Connectors.ToFrozenSet();

        _connectorRegistry.ActiveConnectorsChanged += ActiveConnectorsChangedHandler;
        ActiveConnectorsChangedHandler();
    }

    private void ActiveConnectorsChangedHandler()
    {
        var activeConnectors = _connectorRegistry
            .ActiveConnectors
            .Select(c => c.Key.ConnectorKey)
            .ToHashSet();

        var lookup = _innerConnectorNames.ToLookup(activeConnectors.Contains);
        var activeInnerConnectors = lookup[true];
        var inactiveInnerConnectors = lookup[false];

        foreach(var name in activeInnerConnectors)
        {
            if (Connectors.ContainsKey(name))
            {
                continue;
            }

            try
            {
                var identifier = _connectorRegistry.ActiveConnectors.Keys.First(k => k.ConnectorKey == name);
                var connectorContext = GetContext(identifier);
                connectorContext.OnConnectorPacketsChanged += OnInnerConnectorPacketsChanged;
                Connectors.Add(name, connectorContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve connector context for connector '{ConnectorIdentifier}'", name);
            }
        }

        foreach (var name in inactiveInnerConnectors)
        {
            if (!Connectors.TryGetValue(name, out var connectorContext))
            {
                continue;
            }

            connectorContext.OnConnectorPacketsChanged -= OnInnerConnectorPacketsChanged;
            _ = connectorContext.DisposeAsync().AsTask();
            Connectors.Remove(name);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _connectorRegistry.ActiveConnectorsChanged -= ActiveConnectorsChangedHandler;

        foreach (var connector in Connectors.Values)
        {
            connector.OnConnectorPacketsChanged -= OnInnerConnectorPacketsChanged;
            await connector.DisposeAsync();
        }
    }
}
