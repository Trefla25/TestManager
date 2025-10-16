using eHub.Contracts;
using eHub.PlugIn.UI;
using eHub.UI.State;
using eMessenger;
using Microsoft.Extensions.Logging;

namespace eHub.UI.Services;
public class ConnectorScopedContextProvider(
    IConnectorContextProvider contextProvider,
    IConnectorRegistry registry,
    IMessenger messenger,
    ILoggerFactory loggerFactory) : IConnectorScopedContextProvider
{
    private readonly IConnectorContextProvider _contextProvider = contextProvider;
    private readonly IMessenger _messenger = messenger;
    private readonly IConnectorRegistry _registry = registry;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;

    public IConnectorScopedContext GetConnectorScopedContext(ConnectorIdentifier connectorIdentifier)
    {
        var connectorContext = _contextProvider.GetConnectorContext(connectorIdentifier);
        var connectorUiData = _registry.ActiveConnectors[connectorIdentifier];

        if(connectorUiData.ConnectorType == UIViewConfig.CustomUIViewType)
        {
            return new CombinedConnectorScopedContext(
                connectorIdentifier,
                connectorContext,
                connectorUiData,
                _registry,
                this,
                _loggerFactory.CreateLogger<CombinedConnectorScopedContext>());
        }

        return new ConnectorScopedContext(
            connectorIdentifier,
            connectorContext,
            connectorUiData,
            _messenger,
            _loggerFactory.CreateLogger<ConnectorScopedContext>());
    }
}
