using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eHub.PlugIn.UI;

namespace eHub.UI.State;

public interface IConnectorContext : IAsyncDisposable
{
    public event OnConnectorPacketsChangedDelegate OnConnectorPacketsChanged;
    Task RunAsync(CancellationToken cancellationToken);
    Task StopPacketsAsync(HashSet<PacketDto> packets);
    Task DeletePacketsAsync(HashSet<PacketDto> packets);
    Task ResendPacketsAsync(HashSet<PacketDto> packets);
    Task<bool> ImportPacketsAsync(ConnectorPacketsExportDto importData, bool forceImport);
    IReadOnlyDictionary<string, Type> GetCustomFilters();
    UIViewConfig GetUIViewConfig();
}

public delegate void OnConnectorPacketsChangedDelegate(ConnectorIdentifier connectorIdentifier, ConnectorPacketsChangedDto changed);
