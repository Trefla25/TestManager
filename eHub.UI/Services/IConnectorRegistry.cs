using eHub.Contracts;

namespace eHub.UI.Services;

public interface IConnectorRegistry
{
    public event ConnectorsChangedDelegate ActiveConnectorsChanged;
    IReadOnlyDictionary<ConnectorIdentifier, ConnectorUiData> ActiveConnectors { get; }
}

public delegate void ConnectorsChangedDelegate();
