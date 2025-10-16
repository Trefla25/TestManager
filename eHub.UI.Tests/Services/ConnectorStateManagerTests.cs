using eHub.Contracts;
using eHub.PlugIn.UI;
using eHub.UI.Services;
using eHub.UI.State;
using eMessenger;
using eMessenger.Tests;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace eHub.UI.Tests.Services;

[TestClass]
public class ConnectorStateManagerTests
{
    private readonly IMessenger _messenger = TestingMessenger.Create();
    private readonly IConnectorRegistry _connectorRegistrySubstitute = Substitute.For<IConnectorRegistry>();
    private readonly Dictionary<ConnectorIdentifier, ConnectorUiData> _activeConnectors = [];
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private ConnectorStateManager _connectorStateManager = default!;
    private ConnectorsChangedDelegate? _eventHandler;

    [TestInitialize]
    public void TestInitialize()
    {
        // Set up ActiveConnectors property
        _connectorRegistrySubstitute.ActiveConnectors.Returns(_activeConnectors.AsReadOnly());

        // Set up event subscription and unsubscription behavior
        _connectorRegistrySubstitute
            .When(registry => registry.ActiveConnectorsChanged += Arg.Any<ConnectorsChangedDelegate>())
            .Do(callInfo =>  _eventHandler += callInfo.Arg<ConnectorsChangedDelegate>());

        _connectorRegistrySubstitute
            .When(registry => registry.ActiveConnectorsChanged -= Arg.Any<ConnectorsChangedDelegate>())
            .Do(callInfo => _eventHandler -= callInfo.Arg<ConnectorsChangedDelegate>());

        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        var logger = loggerFactory.CreateLogger<ConnectorStateManager>();

        _connectorStateManager = new ConnectorStateManager(
            _messenger,
            _connectorRegistrySubstitute,
            loggerFactory,
            logger);
    }

    [TestMethod]
    public async Task StartAsync_WhenOk_SubscribesToConnectorRegistryEvent()
    {
        await _connectorStateManager.StartAsync(_cancellationTokenSource.Token);
        _eventHandler.Should().NotBeNull("the ConnectorStateManager did not subscribe to the IConnectorRegistry.ConnectorCollectionChanged event");
    }

    [TestMethod]
    public async Task StartAsync_NoConnectors_CreatesNoContexts()
    {
        await _connectorStateManager.StartAsync(_cancellationTokenSource.Token);

        var connectorContexts = _connectorStateManager.GetAllConnectorContexts();
        connectorContexts.Should().BeEmpty("the ConnectorStateManager should not create any connector context instances when no connectors are active");
    }

    [TestMethod]
    public async Task StartAsync_WhenSingleConnector_CreatesContext()
    {
        var connectorIdentifier = CreateFakeConnectorIdentifier();
        var connectorUiData = CreateFakeConnectorUiData();
        _activeConnectors.Add(connectorIdentifier, connectorUiData);

        await _connectorStateManager.StartAsync(_cancellationTokenSource.Token);

        var connectorContext = _connectorStateManager.GetConnectorContext(connectorIdentifier);
        connectorContext.Should().NotBeNull("the ConnectorStateManager should create a connector context instance for the specified active connector");
        connectorContext.Should().BeAssignableTo<ConnectorContext>();
    }

    [TestMethod]
    public async Task StartAsync_WhenCombinedConnector_CreatesContext()
    {
        var connectorIdentifier = CreateFakeConnectorIdentifier();
        var connectorUiData = new ConnectorUiData("TestConnector", UIViewConfig.CustomUIViewType, new UIViewConfig());
        _activeConnectors.Add(connectorIdentifier, connectorUiData);

        await _connectorStateManager.StartAsync(_cancellationTokenSource.Token);

        var connectorContext = _connectorStateManager.GetConnectorContext(connectorIdentifier);
        connectorContext.Should().NotBeNull("the ConnectorStateManager should create a connector context instance for the specified active connector");
        connectorContext.Should().BeAssignableTo<CombinedConnectorContext>();
    }

    [TestMethod]
    public async Task StartAsync_SingleConnector_CreatesSingleContext()
    {
        var connectorIdentifier = CreateFakeConnectorIdentifier();
        var connectorUiData = CreateFakeConnectorUiData();
        _activeConnectors.Add(connectorIdentifier, connectorUiData);

        await _connectorStateManager.StartAsync(_cancellationTokenSource.Token);

        var connectorContexts = _connectorStateManager.GetAllConnectorContexts();
        connectorContexts.Count.Should().Be(1, "the ConnectorStateManager should create a single connector context instance for the active connector");
        connectorContexts.FirstOrDefault().Value.Should().BeAssignableTo<ConnectorContext>();
    }

    [TestMethod]
    public async Task StartAsync_WhenMultipleConnectors_CreatesContexts()
    {
        _activeConnectors.Add(CreateFakeConnectorIdentifier(), CreateFakeConnectorUiData());
        _activeConnectors.Add(CreateFakeConnectorIdentifier(), CreateFakeConnectorUiData());
        _activeConnectors.Add(CreateFakeConnectorIdentifier(), CreateFakeConnectorUiData());
        _activeConnectors.Add(new(Guid.NewGuid().ToString(), "CombinedConnector"), new ConnectorUiData("TestConnector", UIViewConfig.CustomUIViewType, new UIViewConfig()));

        await _connectorStateManager.StartAsync(_cancellationTokenSource.Token);

        var connectorContexts = _connectorStateManager.GetAllConnectorContexts();
        connectorContexts.Count.Should().Be(4, "the ConnectorStateManager should create a connector context instance for each active connector");

        foreach (var connector in connectorContexts)
        {
            var expectedType = connector.Key.ConnectorKey == "CombinedConnector"
                ? typeof(CombinedConnectorContext)
                : typeof(ConnectorContext);

            connector.Value.Should().BeAssignableTo(expectedType);
        }

    }

    [TestMethod]
    public async Task OnConnectorChange_WhenAddedConnector_AddsContext()
    {
        var initialConnector = CreateFakeConnectorIdentifier();
        _activeConnectors.Add(initialConnector, CreateFakeConnectorUiData());
        await _connectorStateManager.StartAsync(_cancellationTokenSource.Token);

        var newConnector = CreateFakeConnectorIdentifier();
        _activeConnectors.Add(newConnector, CreateFakeConnectorUiData());

        _eventHandler?.Invoke();

        var connectorContext = _connectorStateManager.GetConnectorContext(newConnector);
        connectorContext.Should().NotBeNull("the ConnectorStateManager should create a context when a new connector is added in the connector registry");
    }

    [TestMethod]
    public async Task OnConnectorChange_WhenRemovedConnector_RemovesContext()
    {
        var connectorIdentifier = CreateFakeConnectorIdentifier();
        _activeConnectors.Add(connectorIdentifier, CreateFakeConnectorUiData());
        await _connectorStateManager.StartAsync(_cancellationTokenSource.Token);

        _activeConnectors.Remove(connectorIdentifier);
        _eventHandler?.Invoke();

        var allConnectorContexts = _connectorStateManager.GetAllConnectorContexts();
        allConnectorContexts.Should().NotContainKey(connectorIdentifier, "the ConnectorStateManager should remove the connector context when a connector is removed from the connector registry");
    }

    [TestMethod]
    public async Task OnConnectorChange_WhenAddedAndRemovedConnector_AddsAndRemovesContext()
    {
        var connectorToRemove = CreateFakeConnectorIdentifier();
        _activeConnectors.Add(connectorToRemove, CreateFakeConnectorUiData());
        await _connectorStateManager.StartAsync(_cancellationTokenSource.Token);

        _activeConnectors.Remove(connectorToRemove);
        var connectorToAdd = CreateFakeConnectorIdentifier();
        _activeConnectors.Add(connectorToAdd, CreateFakeConnectorUiData());

        _eventHandler?.Invoke();

        var allConnectorContexts = _connectorStateManager.GetAllConnectorContexts();
        allConnectorContexts.Should().NotContainKey(connectorToRemove, "the ConnectorStateManager should remove the connector context when a connector is removed from the connector registry");
        allConnectorContexts.Should().ContainKey(connectorToAdd, "the ConnectorStateManager should create a context when a new connector is added in the connector registry");
    }

    [TestMethod]
    public async Task GetConnectorContext_WhenIdentifierNotFound_ThrowsNotFoundException()
    {
        _activeConnectors.Add(CreateFakeConnectorIdentifier(), CreateFakeConnectorUiData());
        await _connectorStateManager.StartAsync(_cancellationTokenSource.Token);

        var action = () => _connectorStateManager.GetConnectorContext(CreateFakeConnectorIdentifier());

        action.Should().Throw<KeyNotFoundException>("the requested connector identifier does not exist in the ConnectorStateManager");
    }

    [TestMethod]
    public async Task GetConnectorContext_WhenBeforeOnChangeOccurs_CreatesContext()
    {
        await _connectorStateManager.StartAsync(_cancellationTokenSource.Token);

        var connectorIdentifier = CreateFakeConnectorIdentifier();
        var connectorUiData = CreateFakeConnectorUiData();
        _activeConnectors.Add(connectorIdentifier, connectorUiData);

        var connectorContextBeforeEvent = _connectorStateManager.GetConnectorContext(connectorIdentifier);
        connectorContextBeforeEvent.Should().NotBeNull("GetConnectorContext should create and return a non-null context when called before the OnChange event");

        _eventHandler?.Invoke();

        var connectorContextAfterEvent = _connectorStateManager.GetConnectorContext(connectorIdentifier);
        connectorContextBeforeEvent.Should().Be(connectorContextAfterEvent, "GetConnectorContext should create the context if it does not exist and subsequent events should not re-instantiate it");
    }

    [TestMethod]
    public async Task GetAllConnectorContexts_WhenBeforeOnChangeOccurs_CreatesContext()
    {
        await _connectorStateManager.StartAsync(_cancellationTokenSource.Token);

        var connectorIdentifier = CreateFakeConnectorIdentifier();
        var connectorUiData = CreateFakeConnectorUiData();
        _activeConnectors.Add(connectorIdentifier, connectorUiData);

        var connectorContextsBeforeEvent = _connectorStateManager.GetAllConnectorContexts().ToDictionary();
        connectorContextsBeforeEvent.Should().NotBeNullOrEmpty("GetAllConnectorContexts should return a non-null dictionary with all created connector contexts");

        _eventHandler?.Invoke();

        var connectorContextsAfterEvent = _connectorStateManager.GetAllConnectorContexts();
        foreach (var kvp in connectorContextsBeforeEvent)
        {
            connectorContextsAfterEvent.TryGetValue(kvp.Key, out var contextAfter).Should().BeTrue("the context for key '{kvp.Key}' should still exist after the OnChange event");
            kvp.Value.Should().BeSameAs(contextAfter, $"the context instance for key '{kvp.Key}' should remain the same before and after the OnChange event");
        }
    }

    [TestMethod]
    public async Task StopAsync_WhenAllOk_UnsubscribesFromEventAndShutsDownConnectors()
    {
        var connectorIdentifier = CreateFakeConnectorIdentifier();
        _activeConnectors.Add(connectorIdentifier, CreateFakeConnectorUiData());

        await _connectorStateManager.StartAsync(_cancellationTokenSource.Token);
        await _connectorStateManager.StopAsync(_cancellationTokenSource.Token);

        _eventHandler.Should().BeNull("StopAsync should unsubscribe from the ActiveConnectorsChanged event");
    }

    [TestMethod]
    public void SyncConnectorStates_WhenExceptionDuringInitialization_LogsError()
    {
        var faultyConnector = CreateFakeConnectorIdentifier();

        var faultyUiData = new ConnectorUiData("TestConnector", "TestConnector", new UIViewConfig())
        {
            ConnectorType = UIViewConfig.CustomUIViewType
        };

        _activeConnectors.Add(faultyConnector, faultyUiData);

        _connectorRegistrySubstitute.ActiveConnectors.Returns(_activeConnectors.AsReadOnly());

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<ConnectorStateManager>();

        var manager = new ConnectorStateManager(
            _messenger,
            _connectorRegistrySubstitute,
            loggerFactory,
            logger);

        _activeConnectors.Remove(faultyConnector); 

        var action = () => manager.GetConnectorContext(faultyConnector);
        action.Should().ThrowExactly<KeyNotFoundException>();
    }

    [TestMethod]
    public void GetConnectorContext_WhenInvalidConnector_ThrowsException()
    {
        var invalidConnector = CreateFakeConnectorIdentifier();

        Action action = () => _connectorStateManager.GetConnectorContext(invalidConnector);
        action.Should().Throw<KeyNotFoundException>()
            .WithMessage($"No active connector with identifier '{invalidConnector}' was found in the connector registry.");
    }

    private static ConnectorIdentifier CreateFakeConnectorIdentifier() => new(Guid.NewGuid().ToString(), "TestConnector");

    private static ConnectorUiData CreateFakeConnectorUiData() => new("TestConnector", "TestConnector", new UIViewConfig());
}
