using System.Collections.Immutable;
using System.Threading.Channels;
using eHub.Scripting.Connectors;

namespace eHub.Tests.Helper;

/// <summary>Testing utility to wait for a specific event on a connector.</summary>
internal class AwaitableConnectorListener : IConnectorMessageHandler
{
    private readonly Channel<(string connectorName, IReadOnlyDictionary<string, string> statusDictionary)> StatusWaiter = Channel.CreateUnbounded<(string connectorName, IReadOnlyDictionary<string, string> statusDictionary)>();
    private readonly Channel<object> ConfigChangedWaiter = Channel.CreateUnbounded<object>();

    public ValueTask<(string connectorName, IReadOnlyDictionary<string, string> statusDictionary)> NextStatus()
        => StatusWaiter.Reader.ReadAsync();
    public async ValueTask NextConfigReload()
        => await ConfigChangedWaiter.Reader.ReadAsync();

    public void ResetStatus()
    {
        while (StatusWaiter.Reader.TryRead(out _))
        {
            ;
        }
    }
    public void ResetConfigReload()
    {
        while (ConfigChangedWaiter.Reader.TryRead(out _))
        {
            ;
        }
    }

    void IConnectorMessageHandler.StatusChanged(string connectorName, IReadOnlyDictionary<string, string> statusDictionary)
    {
        StatusWaiter.Writer.WriteAsync((connectorName, statusDictionary.ToImmutableDictionary())).AsTask().Wait();
    }

    void IConnectorMessageHandler.ConfigChanged()
    {
        ConfigChangedWaiter.Writer.WriteAsync(new()).AsTask().Wait();
    }
}
