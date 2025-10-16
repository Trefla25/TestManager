using eHub.Contracts;
using eHub.UI.State;

namespace eHub.UI.Services;

public interface IConnectorContextProvider
{
    IConnectorContext GetConnectorContext(ConnectorIdentifier connectorIdentifier);
}
