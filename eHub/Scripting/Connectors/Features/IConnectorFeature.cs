namespace eHub.Scripting.Connectors.Features;

public interface IConnectorFeature : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken);
}
