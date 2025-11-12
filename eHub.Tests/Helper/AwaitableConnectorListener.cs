using System.Collections.Immutable;
using System.Threading.Channels;
using eHub.Scripting.Connectors;

namespace eHub.Tests.Helper;

/// <summary>Testing utility to wait for a specific event on a connector.</summary>
internal class AwaitableConnectorListener : IConnectorMessageHandler
{
    private readonly Channel<(string connectorName, IReadOnlyDictionary<string, string> statusDictionary)> _statusWaiter = Channel.CreateUnbounded<(string connectorName, IReadOnlyDictionary<string, string> statusDictionary)>();
    private readonly Channel<object> _configChangedWaiter = Channel.CreateUnbounded<object>();

    public ValueTask<(string connectorName, IReadOnlyDictionary<string, string> statusDictionary)> NextStatusAsync()
        => _statusWaiter.Reader.ReadAsync();
    public async ValueTask NextConfigReloadAsync()
        => await _configChangedWaiter.Reader.ReadAsync();

    public void ResetStatus()
    {
        while (_statusWaiter.Reader.TryRead(out _))
        {
            ;
        }
    }
    public void ResetConfigReload()
    {
        while (_configChangedWaiter.Reader.TryRead(out _))
        {
            ;
        }
    }

    void IConnectorMessageHandler.StatusChanged(string connectorName, IReadOnlyDictionary<string, string> statusDictionary)
    {
        _statusWaiter.Writer.WriteAsync((connectorName, statusDictionary.ToImmutableDictionary())).AsTask().Wait();
    }

    void IConnectorMessageHandler.ConfigChanged()
    {
        _configChangedWaiter.Writer.WriteAsync(new()).AsTask().Wait();
    }
}
