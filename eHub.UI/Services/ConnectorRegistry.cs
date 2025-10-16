using System.Collections.Concurrent;
using eHub.Contracts;
using eMessenger;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace eHub.UI.Services;

public class ConnectorRegistry(IMessenger messenger, ILogger<ConnectorRegistry> logger) : BackgroundService, IConnectorRegistry
{
    private readonly ILogger _logger = logger;

    public event ConnectorsChangedDelegate? ActiveConnectorsChanged;

    private readonly ConcurrentDictionary<ConnectorIdentifier, ConnectorUiData> _connectorStateEvents = new();
    public IReadOnlyDictionary<ConnectorIdentifier, ConnectorUiData> ActiveConnectors => _connectorStateEvents;

    public async Task WorkAsync()
    {
        var results = await messenger.AskAsync<ConnectorKeepAliveDto>(ConnectorContract.PollAliveConnectorsTopic());
        // Fast path when nothing changed
        if (results.Count == _connectorStateEvents.Count && results.All(r => _connectorStateEvents.ContainsKey(r.Identifier)))
        {
            return;
        }

        var alive = results.Select(x => x.Identifier).ToHashSet();
        var current = _connectorStateEvents.Keys.ToHashSet();

        bool isChanged = false;

        foreach (var connector in current)
        {
            if (!alive.Contains(connector))
            {
                isChanged |= _connectorStateEvents.TryRemove(connector, out _);
                _logger.LogInformation("{connector} connector disconnected.", connector.ConnectorKey);
            }
        }

        foreach (var connector in alive)
        {
            if (!_connectorStateEvents.ContainsKey(connector))
            {
                var result = await messenger.AskAsync<ConnectorUiData>(ConnectorContract.UIGetTopic(connector)).FirstOrDefaultResponse();
                if (result is null)
                {
                    _logger.LogWarning("Could not retrieve UI data for connector: {connector}.", connector.ConnectorKey);
                    continue;
                }

                isChanged |= _connectorStateEvents.TryAdd(connector, result);
                _logger.LogInformation("{connector} connector connected.", connector.ConnectorKey);
            }
        }

        if (isChanged)
        {
            ActiveConnectorsChanged?.Invoke();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        IRegistrationToken token = NullRegistrationToken.Instance;

        token += await messenger.ListenAsync<ConnectorStartedEventDto>(ConnectorContract.ConnectorStartedTopic(), result =>
        {
            if (_connectorStateEvents.TryAdd(result.Identifier, result.Ui))
            {
                _logger.LogInformation("{connector} connector connected.", result.Identifier.ConnectorKey);
                ActiveConnectorsChanged?.Invoke();
            }
        });

        token += await messenger.ListenAsync<ConnectorStoppedEventDto>(ConnectorContract.ConnectorStoppedTopic(), result =>
        {
            if (_connectorStateEvents.TryRemove(result.Identifier, out _))
            {
                _logger.LogInformation("{connector} connector disconnected.", result.Identifier.ConnectorKey);
                ActiveConnectorsChanged?.Invoke();
            }
        });

        using var token_ = token;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await WorkAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update connectors UI data.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
    }
}
