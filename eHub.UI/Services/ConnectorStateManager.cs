using System.Collections.Concurrent;
using eHub.Contracts;
using eHub.PlugIn.UI;
using eHub.UI.State;
using eMessenger;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace eHub.UI.Services;

public class ConnectorStateManager(
    IMessenger messenger,
    IConnectorRegistry connectorRegistry,
    ILoggerFactory loggerFactory,
    ILogger<ConnectorStateManager> logger) : IHostedService, IConnectorContextProvider, IAsyncDisposable
{
    private readonly IMessenger _messenger = messenger;
    private readonly IConnectorRegistry _connectorRegistry = connectorRegistry;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly ILogger<ConnectorStateManager> _logger = logger;
    private readonly ConcurrentDictionary<ConnectorIdentifier, ConnectorContextState> _connectorStates = [];
    private readonly CancellationTokenSource _globalCancellationTokenSource = new();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        SyncConnectorStates();

        _connectorRegistry.ActiveConnectorsChanged += OnConnectorCollectionChanged;

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _connectorRegistry.ActiveConnectorsChanged -= OnConnectorCollectionChanged;
        await _globalCancellationTokenSource.CancelAsync();
        foreach (var connectorContext in _connectorStates.Values)
        {
            await ShutdownConnectorContextAsync(connectorContext, cancellationToken);
        }
    }

    public IConnectorContext GetConnectorContext(ConnectorIdentifier connectorIdentifier)
    {
        if (!_connectorRegistry.ActiveConnectors.TryGetValue(connectorIdentifier, out var uiData))
        {
            throw new KeyNotFoundException($"No active connector with identifier '{connectorIdentifier}' was found in the connector registry.");
        }

        var connectorState = _connectorStates.GetOrAdd(connectorIdentifier, (key) =>
            uiData.ConnectorType != UIViewConfig.CustomUIViewType
                ? CreateConnectorContextState(key)
                : CreateCombinedConnectorContextState(key));

        return connectorState.ConnectorContext;
    }

    public IReadOnlyDictionary<ConnectorIdentifier, IConnectorContext> GetAllConnectorContexts()
    {
        SyncConnectorStates();
        return _connectorStates.ToDictionary(c => c.Key, c => c.Value.ConnectorContext);
    }

    private void OnConnectorCollectionChanged()
    {
        SyncConnectorStates();
    }

    private void SyncConnectorStates()
    {
        var keysToRemove = _connectorStates.Keys.Where(key => !_connectorRegistry.ActiveConnectors.ContainsKey(key));

        foreach (var connectorIdentifier in keysToRemove)
        {
            if (!_connectorStates.TryRemove(connectorIdentifier, out var removedConnector))
            {
                continue;
            }

            _ = Task.Run(async () =>
            {
                await ShutdownConnectorContextAsync(removedConnector);
            });
        }

        foreach (var connector in _connectorRegistry.ActiveConnectors)
        {
            try
            {
                var connectorContext = _connectorStates.GetOrAdd(connector.Key, (key) =>
                    connector.Value.ConnectorType != UIViewConfig.CustomUIViewType
                        ? CreateConnectorContextState(key)
                        : CreateCombinedConnectorContextState(key));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize connector context for connector '{ConnectorIdentifier}'", connector.Key);
            }
        }
    }

    private ConnectorContextState CreateCombinedConnectorContextState(ConnectorIdentifier connectorIdentifier)
    {
        var uiData = _connectorRegistry.ActiveConnectors[connectorIdentifier];
        var logger = _loggerFactory.CreateLogger<CombinedConnectorContext>();
        var connectorContext = new CombinedConnectorContext(connectorIdentifier, uiData, _connectorRegistry, this, logger);
        var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(_globalCancellationTokenSource.Token);
        var workTask = connectorContext.RunAsync(tokenSource.Token);

        return new ConnectorContextState(connectorContext, workTask, tokenSource);
    }

    private ConnectorContextState CreateConnectorContextState(ConnectorIdentifier connectorIdentifier)
    {
        var connectorContext = new ConnectorContext(connectorIdentifier, _connectorRegistry.ActiveConnectors[connectorIdentifier], _messenger);
        var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(_globalCancellationTokenSource.Token);
        var workTask = connectorContext.RunAsync(tokenSource.Token);

        return new ConnectorContextState(connectorContext, workTask, tokenSource);
    }

    private async Task ShutdownConnectorContextAsync(ConnectorContextState connectorState, CancellationToken cancellationToken = default)
    {
        await connectorState.CancellationTokenSource.CancelAsync();

        try
        {
            await connectorState.WorkTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

            _logger.LogInformation("Connector stopped.");
        }
        catch (OperationCanceledException) // Expected when Token gets cancelled.
        {
            _logger.LogInformation("Connector stopped by cancellation.");
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Timed out waiting for graceful shutdown of connector");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop connector gracefully");
        }

        try
        {
            await connectorState.ConnectorContext.DisposeAsync();
            connectorState.CancellationTokenSource.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispose connector context.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var connector in _connectorStates.Values)
        {
            await ShutdownConnectorContextAsync(connector);
        }

        _globalCancellationTokenSource.Dispose();
    }
}

public record ConnectorContextState(IConnectorContext ConnectorContext, Task WorkTask, CancellationTokenSource CancellationTokenSource);
