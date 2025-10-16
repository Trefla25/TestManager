using eHub.Contracts;
using eHub.UI.State;

namespace eHub.UI.Services;
public interface IConnectorScopedContextProvider
{
    IConnectorScopedContext GetConnectorScopedContext(ConnectorIdentifier connectorIdentifier);
}
