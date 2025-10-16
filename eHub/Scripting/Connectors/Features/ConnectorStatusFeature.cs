using System.Collections.Concurrent;
using System.Collections.Immutable;
using eHub.PlugIn;

namespace eHub.Scripting.Connectors.Features;

public class ConnectorStatusFeature(
    IConnector connector,
    IEnumerable<IConnectorMessageHandler> messageHandler,
    ConnectorMetadata metadata
    ) : IConnectorFeature
{
    private readonly ImmutableArray<IConnectorMessageHandler> _messageHandler = messageHandler.ToImmutableArray();

    public ConcurrentDictionary<string, string> ConnectorStatus { get; } = new();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        connector.UpdateStatus += ConnectorStatusUpdate;
        return Task.CompletedTask;
    }

    public void ConnectorStatusUpdate(string key, string? value)
    {
        if (value is null)
        {
            ConnectorStatus.Remove(key, out _);
        }
        else
        {
            ConnectorStatus[key] = value;
        }

        foreach (var handler in _messageHandler)
        {
            handler.StatusChanged(metadata.TemplateName, ConnectorStatus);
        }
    }

    public ValueTask DisposeAsync()
    {
        connector.UpdateStatus -= ConnectorStatusUpdate;
        return ValueTask.CompletedTask;
    }
}
