using eHub.Contracts;
using eHub.PlugIn.UI;
using eHub.UI.Services;
using eHub.UI.State;
using eMessenger;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace eHub.UI.Tests.Services;

public class ConnectorScopedContextProviderTests
{
    private IConnectorContextProvider _contextProvider = default!;
    private IConnectorRegistry _registry = default!;
    private IMessenger _messenger = default!;
    private ILoggerFactory _loggerFactory = default!;
    private ConnectorScopedContextProvider _provider = default!;

    public ConnectorScopedContextProviderTests()
    {
        _contextProvider = Substitute.For<IConnectorContextProvider>();
        _registry = Substitute.For<IConnectorRegistry>();
        _messenger = Substitute.For<IMessenger>();
        _loggerFactory = Substitute.For<ILoggerFactory>();

        _provider = new ConnectorScopedContextProvider(_contextProvider, _registry, _messenger, _loggerFactory);
    }

    [Fact]
    public void GetConnectorScopedContext_WhenConnectorTypeIsCustomUIView_ReturnsCombinedConnectorScopedContext()
    {
        // Arrange
        var connectorIdentifier = new ConnectorIdentifier("Test", "TestConnector");
        var connectorContext = Substitute.For<IConnectorContext>();
        var connectorUiData = new ConnectorUiData("TestConnector", "TestConnector", new UIViewConfig())
        {
            ConnectorType = UIViewConfig.CustomUIViewType
        };

        // Act
        _contextProvider.GetConnectorContext(connectorIdentifier).Returns(connectorContext);
        _registry.ActiveConnectors.Returns(new Dictionary<ConnectorIdentifier, ConnectorUiData>
            {
                { connectorIdentifier, connectorUiData }
            });
        var result = _provider.GetConnectorScopedContext(connectorIdentifier);
        // Assert
        result.Should().BeOfType<CombinedConnectorScopedContext>();
    }

    [Fact]
    public void GetConnectorScopedContext_WhenConnectorTypeIsNotCustomUIView_ReturnsConnectorScopedContext_()
    {
        // Arrange
        var connectorIdentifier = new ConnectorIdentifier("Test", "TestConnector");
        var connectorContext = Substitute.For<IConnectorContext>();
        var connectorUiData = new ConnectorUiData("TestConnector", "TestConnector", new UIViewConfig())
        {
            ConnectorType = "StandardView"
        };

        // Act
        _contextProvider.GetConnectorContext(connectorIdentifier).Returns(connectorContext);
        _registry.ActiveConnectors.Returns(new Dictionary<ConnectorIdentifier, ConnectorUiData>
            {
                { connectorIdentifier, connectorUiData }
            });
        var result = _provider.GetConnectorScopedContext(connectorIdentifier);
        // Assert
        result.Should().BeOfType<ConnectorScopedContext>();
    }

    [Fact]
    public void GetConnectorScopedContext_WhenConnectorIsNotInRegistry_ThrowsKeyNotFoundException()
    {
        // Arrange
        var connectorIdentifier = new ConnectorIdentifier("Test", "MissingConnector");
        _registry.ActiveConnectors.Returns(new Dictionary<ConnectorIdentifier, ConnectorUiData>());
        // Act
        var act = () => _provider.GetConnectorScopedContext(connectorIdentifier);
        // Assert
        act.Should().Throw<KeyNotFoundException>();
    }
}