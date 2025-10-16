using eHub.Contracts;
using eHub.PlugIn.UI;
using eHub.UI.Services;
using eHub.UI.State;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace eHub.UI.Tests.State;

[TestClass]
public class CombinedContextManagerTests
{
    private readonly Dictionary<ConnectorIdentifier, IConnectorContext> _providerContexts = [];
    private TestCombinedManager? _combinedManager;
    private ConnectorUiData _connectorUiData = null!;
    private ConnectorIdentifier _managerIdentifier;
    private ConnectorIdentifier _identifierA;
    private ConnectorIdentifier _identifierB;
    private IConnectorContext _connectorA = null!;
    private IConnectorContext _connectorB = null!;
    private IConnectorRegistry _connectorRegistry = null!;
    private ILogger _logger = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _connectorRegistry = Substitute.For<IConnectorRegistry>();
        _connectorA = Substitute.For<IConnectorContext>();
        _connectorB = Substitute.For<IConnectorContext>();
        _logger = NullLogger.Instance;

        _managerIdentifier = new ConnectorIdentifier("instance", "Combined");
        _identifierA = new ConnectorIdentifier("instance", "ConnectorA");
        _identifierB = new ConnectorIdentifier("instance", "ConnectorB");

        _connectorUiData = new ConnectorUiData("Combined", UIViewConfig.CustomUIViewType,
            new UIViewConfig() { Views = new() { { "All", new() } } });
    }

    [TestMethod]
    public void Constructor_WhenNoChannelsDefined_DoesNotAddContexts()
    {
        // Act
        _combinedManager = CreateCombinedManager();

        // Assert
        _combinedManager.Connectors.Should().BeEmpty();
    }

    [TestMethod]
    public void Constructor_WhenNoChannelsActive_DoesNotAddContexts()
    {
        // Arrange
        SetupExpectedConnectors("ConnectorA", "ConnectorB");

        // Act
        _combinedManager = CreateCombinedManager();

        // Assert
        _combinedManager.Connectors.Should().BeEmpty();
    }

    [TestMethod]
    public void Constructor_WhenGetContextThrows_DoesNotAddContexts()
    {
        // Arrange
        SetupExpectedConnectors("ConnectorA", "ConnectorB");
        SetupActiveConnectors(_identifierA, _identifierB);

        // Act
        _combinedManager = CreateCombinedManager();

        // Assert
        _combinedManager.Connectors.Should().BeEmpty();
    }

    [TestMethod]
    public void Constructor_WhenConnectorsActive_AddsContexts()
    {
        // Arrange
        SetupExpectedConnectors("ConnectorA", "ConnectorB");
        SetupActiveConnectors(_identifierA, _identifierB);

        _providerContexts.Add(_identifierA, _connectorA);
        _providerContexts.Add(_identifierB, _connectorB);

        // Act
        _combinedManager = CreateCombinedManager();

        // Assert
        _combinedManager.Connectors.Should().ContainKey("ConnectorA");
        _combinedManager.Connectors.Should().ContainKey("ConnectorB");
        _combinedManager.Connectors["ConnectorA"].Should().BeSameAs(_connectorA);
        _combinedManager.Connectors["ConnectorB"].Should().BeSameAs(_connectorB);
    }

    [TestMethod]
    public void Constructor_WithDuplicateConnectorNames_AddsContexts()
    {
        // Arrange
        SetupExpectedConnectors("ConnectorA", "ConnectorB");

        var otherIdentifierA = new ConnectorIdentifier("otherInstance", "ConnectorA");
        var otherConnectorA = Substitute.For<IConnectorContext>();

        SetupActiveConnectors(_identifierA, _identifierB, otherIdentifierA);

        _providerContexts.Add(_identifierA, _connectorA);
        _providerContexts.Add(_identifierB, _connectorB);
        _providerContexts.Add(otherIdentifierA, otherConnectorA);

        // Act
        _combinedManager = CreateCombinedManager();

        // Assert
        _combinedManager.Connectors.Should().HaveCount(2);
        _combinedManager.Connectors.Should().ContainKey("ConnectorA");
        _combinedManager.Connectors.Should().ContainKey("ConnectorB");
        _combinedManager.Connectors["ConnectorA"].Should().BeSameAs(_connectorA);
        _combinedManager.Connectors["ConnectorB"].Should().BeSameAs(_connectorB);
    }

    [TestMethod]
    public void InnerConnectorPacketsChanged_WhenRaised_RaisesManagerEvent()
    {
        // Arrange
        SetupExpectedConnectors("ConnectorA", "ConnectorB");
        SetupActiveConnectors(_identifierA, _identifierB);

        _providerContexts.Add(_identifierA, _connectorA);
        _providerContexts.Add(_identifierB, _connectorB);

        _combinedManager = CreateCombinedManager();

        // Act
        _connectorA.OnConnectorPacketsChanged += Raise.Event<OnConnectorPacketsChangedDelegate>(_identifierA, ConnectorPacketsChangedDto.Any);
        _connectorB.OnConnectorPacketsChanged += Raise.Event<OnConnectorPacketsChangedDelegate>(_identifierB, ConnectorPacketsChangedDto.Any);

        // Assert
        _combinedManager.Notifications.Should().HaveCount(2);
        _combinedManager.Notifications.Should().Contain(_identifierA);
        _combinedManager.Notifications.Should().Contain(_identifierB);
    }

    [TestMethod]
    public void ActiveConnectorsChanged_WhenConnectorAdded_AddsContext()
    {
        // Arrange
        SetupExpectedConnectors("ConnectorA", "ConnectorB");
        SetupActiveConnectors(_identifierA);

        _providerContexts.Add(_identifierA, _connectorA);
        _providerContexts.Add(_identifierB, _connectorB);

        _combinedManager = CreateCombinedManager();

        // Act
        SetupActiveConnectors(_identifierA, _identifierB);
        _connectorRegistry.ActiveConnectorsChanged += Raise.Event<ConnectorsChangedDelegate>();

        // Assert
        _combinedManager.Connectors.Should().ContainKey("ConnectorA");
        _combinedManager.Connectors.Should().ContainKey("ConnectorB");
        _combinedManager.Connectors["ConnectorA"].Should().BeSameAs(_connectorA);
        _combinedManager.Connectors["ConnectorB"].Should().BeSameAs(_connectorB);
    }

    [TestMethod]
    public void ActiveConnectorsChanged_WhenConnectorRemoved_RemovesAndDisposesContext()
    {
        // Arrange
        SetupExpectedConnectors("ConnectorA", "ConnectorB");
        SetupActiveConnectors(_identifierA, _identifierB);

        _providerContexts.Add(_identifierA, _connectorA);
        _providerContexts.Add(_identifierB, _connectorB);

        _combinedManager = CreateCombinedManager();

        // Act
        SetupActiveConnectors(_identifierB);
        _connectorRegistry.ActiveConnectorsChanged += Raise.Event<ConnectorsChangedDelegate>();
        _connectorA.OnConnectorPacketsChanged += Raise.Event<OnConnectorPacketsChangedDelegate>(_identifierA, ConnectorPacketsChangedDto.Any);

        // Assert
        _combinedManager.Connectors.Should().NotContainKey("ConnectorA");
        _combinedManager.Connectors.Should().ContainKey("ConnectorB");
        _combinedManager.Connectors["ConnectorB"].Should().BeSameAs(_connectorB);
        _combinedManager.Notifications.Should().BeEmpty();
        _ = _connectorA.Received(1).DisposeAsync().AsTask();
    }

    [TestCleanup]
    public async Task TestCleanup()
    {
        if (_combinedManager != null)
        {
            await _combinedManager.DisposeAsync();
        }
    }

    private TestCombinedManager CreateCombinedManager()
        => new(_providerContexts, _managerIdentifier, _connectorUiData, _connectorRegistry, _logger);

    private void SetupExpectedConnectors(params string[] connectors)
    {
        var view = _connectorUiData.UIViewConfig.Views.First().Value;
        view.Channels.Clear();
        foreach (var connector in connectors)
        {
            view.Channels.Add(new() { Connector = connector });
        }
    }

    private void SetupActiveConnectors(params ConnectorIdentifier[] connectors)
    {
        var activeConnectors = connectors.ToDictionary(
            identifier => identifier,
            identifier => new ConnectorUiData(identifier.ConnectorKey, "TestConnector", new()));

        _connectorRegistry.ActiveConnectors.Returns(activeConnectors);
    }

    private class TestCombinedManager(
        Dictionary<ConnectorIdentifier, IConnectorContext> contexts,
        ConnectorIdentifier id,
        ConnectorUiData data,
        IConnectorRegistry registry,
        ILogger logger)
        : CombinedContextManager<IConnectorContext>(id, data, registry, logger)
    {
        private readonly Dictionary<ConnectorIdentifier, IConnectorContext> _contexts = contexts;

        public new IReadOnlyDictionary<string, IConnectorContext> Connectors => base.Connectors;
        public List<ConnectorIdentifier> Notifications { get; } = [];

        protected override IConnectorContext GetContext(ConnectorIdentifier identifier)
            => _contexts[identifier];

        protected override void OnInnerConnectorPacketsChanged(ConnectorIdentifier connectorIdentifier, ConnectorPacketsChangedDto changed)
            => Notifications.Add(connectorIdentifier);
    }
}
