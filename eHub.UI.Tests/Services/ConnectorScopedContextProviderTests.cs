using eHub.Contracts;
using eHub.PlugIn.UI;
using eHub.UI.Services;
using eHub.UI.State;
using eMessenger;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace eHub.UI.Tests.Services;

[TestClass]
public class ConnectorScopedContextProviderTests
{
    private IConnectorContextProvider _contextProvider = default!;
    private IConnectorRegistry _registry = default!;
    private IMessenger _messenger = default!;
    private ILoggerFactory _loggerFactory = default!;
    private ConnectorScopedContextProvider _provider = default!;

    [TestInitialize]
    public void Setup()
    {
        _contextProvider = Substitute.For<IConnectorContextProvider>();
        _registry = Substitute.For<IConnectorRegistry>();
        _messenger = Substitute.For<IMessenger>();
        _loggerFactory = Substitute.For<ILoggerFactory>();

        _provider = new ConnectorScopedContextProvider(_contextProvider, _registry, _messenger, _loggerFactory);
    }

    [TestMethod]
    public void GetConnectorScopedContext_WhenConnectorTypeIsCustomUIView_ReturnsCombinedConnectorScopedContext()
    {
        var connectorIdentifier = new ConnectorIdentifier("Test", "TestConnector");
        var connectorContext = Substitute.For<IConnectorContext>();
        var connectorUiData = new ConnectorUiData("TestConnector", "TestConnector", new UIViewConfig())
        {
            ConnectorType = UIViewConfig.CustomUIViewType
        };

        _contextProvider.GetConnectorContext(connectorIdentifier).Returns(connectorContext);
        _registry.ActiveConnectors.Returns(new Dictionary<ConnectorIdentifier, ConnectorUiData>
            {
                { connectorIdentifier, connectorUiData }
            });

        var result = _provider.GetConnectorScopedContext(connectorIdentifier);

        result.Should().BeOfType<CombinedConnectorScopedContext>();
    }

    [TestMethod]
    public void GetConnectorScopedContext_WhenConnectorTypeIsNotCustomUIView_ReturnsConnectorScopedContext_()
    {
        var connectorIdentifier = new ConnectorIdentifier("Test", "TestConnector");
        var connectorContext = Substitute.For<IConnectorContext>();
        var connectorUiData = new ConnectorUiData("TestConnector", "TestConnector", new UIViewConfig())
        {
            ConnectorType = "StandardView"
        };

        _contextProvider.GetConnectorContext(connectorIdentifier).Returns(connectorContext);
        _registry.ActiveConnectors.Returns(new Dictionary<ConnectorIdentifier, ConnectorUiData>
            {
                { connectorIdentifier, connectorUiData }
            });

        var result = _provider.GetConnectorScopedContext(connectorIdentifier);

        result.Should().BeOfType<ConnectorScopedContext>();
    }

    [TestMethod]
    public void GetConnectorScopedContext_WhenConnectorIsNotInRegistry_ThrowsKeyNotFoundException()
    {
        var connectorIdentifier = new ConnectorIdentifier("Test", "MissingConnector");
        _registry.ActiveConnectors.Returns(new Dictionary<ConnectorIdentifier, ConnectorUiData>());

        var act = () => _provider.GetConnectorScopedContext(connectorIdentifier);

        act.Should().Throw<KeyNotFoundException>();
    }
}
