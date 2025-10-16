using System.Collections.Immutable;
using System.Threading.Tasks;
using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eHub.PlugIn;
using eHub.PlugIn.UI;
using eHub.UI.Services;
using eHub.UI.State;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace eHub.UI.Tests.State;

[TestClass]
public class CombinedConnectorContextTests
{
    private CombinedConnectorContext _combinedContext = default!;
    private ConnectorIdentifier _combinedConnectorIdentifier = default!;
    private ConnectorUiData _connectorUiData = default!;
    private ConnectorIdentifier _connectorIdentifierA = default!;
    private ConnectorIdentifier _connectorIdentifierB = default!;
    private IConnectorContext _connectorA = default!;
    private IConnectorContext _connectorB = default!;
    private IConnectorRegistry _connectorRegistry = default!;
    private IConnectorContextProvider _contextProvider = default!;
    private CancellationTokenSource _cancellationTokenSource = default!;

    [TestInitialize]
    public void TestInitialize()
    {
        _cancellationTokenSource = new();
        _combinedConnectorIdentifier = new ConnectorIdentifier("instance", "Combined");
        _connectorUiData = new ConnectorUiData("TestConnector", UIViewConfig.CustomUIViewType, new UIViewConfig()
        {
            Views = new()
            {
                { "All", new() { Name = "All", Channels = [ new() { Connector = "ConnectorA" }, new() { Connector = "ConnectorB" } ] } }
            }
        });

        var c = _connectorUiData.UIViewConfig.Views.Values
            .SelectMany(x => x.Channels)
            .Select(x => x.Connector)
            .Where(x => x != null)
            .Distinct();

        _connectorA = Substitute.For<IConnectorContext>();
        _connectorB = Substitute.For<IConnectorContext>();

        _connectorIdentifierA = new ConnectorIdentifier("instance", "ConnectorA");
        _connectorIdentifierB = new ConnectorIdentifier("instance", "ConnectorB");

        var activeConnectors = new Dictionary<ConnectorIdentifier, ConnectorUiData>
        {
            { _connectorIdentifierA, new ConnectorUiData("ConnectorA", "TestConnector", new UIViewConfig()) },
            { _connectorIdentifierB, new ConnectorUiData("ConnectorB", "TestConnector", new UIViewConfig()) }
        };

        _connectorRegistry = Substitute.For<IConnectorRegistry>();
        _connectorRegistry.ActiveConnectors.Returns(activeConnectors);

        _contextProvider = Substitute.For<IConnectorContextProvider>();
        _contextProvider.GetConnectorContext(_connectorIdentifierA).Returns(_connectorA);
        _contextProvider.GetConnectorContext(_connectorIdentifierB).Returns(_connectorB);

        _combinedContext = new CombinedConnectorContext(_combinedConnectorIdentifier, _connectorUiData, _connectorRegistry, _contextProvider, NullLogger<CombinedConnectorContext>.Instance);
    }

    [TestMethod]
    public async Task RunAsync_WithInnerContexts_SubscribesToAllContexts()
    {
        var raisedEvents = new List<(ConnectorIdentifier Identifier, ConnectorPacketsChangedDto Changed)>();
        _combinedContext.OnConnectorPacketsChanged += (x, y) => raisedEvents.Add((x, y));

        await _combinedContext.RunAsync(CancellationToken.None);

        _connectorA.OnConnectorPacketsChanged += Raise.Event<OnConnectorPacketsChangedDelegate>(_connectorIdentifierA, ConnectorPacketsChangedDto.Any);
        _connectorB.OnConnectorPacketsChanged += Raise.Event<OnConnectorPacketsChangedDelegate>(_connectorIdentifierB, ConnectorPacketsChangedDto.Any);

        raisedEvents.Should().HaveCount(2);
        raisedEvents.Should().OnlyContain(x => x.Identifier == _combinedConnectorIdentifier && x.Changed == ConnectorPacketsChangedDto.Any);
    }

    [TestMethod]
    public async Task ConnectorPacketsChangedHandler_WhenNoSubscribers_DoesNotThrow()
    {
        await _combinedContext.RunAsync(CancellationToken.None);

        var act = () =>
            _connectorA.OnConnectorPacketsChanged += Raise.Event<OnConnectorPacketsChangedDelegate>(_connectorIdentifierA, ConnectorPacketsChangedDto.Any);

        act.Should().NotThrow();
    }

    [TestMethod]
    public void GetUIViewConfig_WhenCalled_ReturnsExpectedConfig()
    {
        var result = _combinedContext.GetUIViewConfig();

        result.Should().Be(_connectorUiData.UIViewConfig);
    }

    [TestMethod]
    public async Task GetCustomFilters_WithValidFilters_ReturnsExpectedFilters()
    {
        var customFiltersA = new Dictionary<string, Type>
        {
            { "Filter1", typeof(int) },
            { "Filter2", typeof(string) },
        }.AsReadOnly();

        var customFiltersB = new Dictionary<string, Type>
        {
            { "Filter3", typeof(DateTime) }
        }.AsReadOnly();

        _connectorA.GetCustomFilters().Returns(customFiltersA);
        _connectorB.GetCustomFilters().Returns(customFiltersB);

        await _combinedContext.RunAsync(CancellationToken.None);
        var result = _combinedContext.GetCustomFilters();

        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().ContainKey("Filter1");
        result.Should().ContainKey("Filter2");
        result.Should().ContainKey("Filter3");

        result["Filter1"].Should().Be<int>();
        result["Filter2"].Should().Be<string>();
        result["Filter3"].Should().Be<DateTime>();
    }

    [TestMethod]
    public void GetCustomFilters_WhenNotInitialized_ReturnsEmpty()
    {
        var customFiltersA = new Dictionary<string, Type>
        {
            { "Filter1", typeof(int) },
            { "Filter2", typeof(string) },
        }.AsReadOnly();

        var customFiltersB = new Dictionary<string, Type>
        {
            { "Filter3", typeof(DateTime) }
        }.AsReadOnly();

        _connectorA.GetCustomFilters().Returns(customFiltersA);
        _connectorB.GetCustomFilters().Returns(customFiltersB);

        var result = _combinedContext.GetCustomFilters();

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GetCustomFilters_WithConflictingFilters_KeepsLatest()
    {
        var customFiltersA = new Dictionary<string, Type>
        {
            { "Filter1", typeof(int) },
            { "Filter2", typeof(string) },
        }.AsReadOnly();

        var customFiltersB = new Dictionary<string, Type>
        {
            { "Filter2", typeof(DateTime) }
        }.AsReadOnly();

        _connectorA.GetCustomFilters().Returns(customFiltersA);
        _connectorB.GetCustomFilters().Returns(customFiltersB);

        await _combinedContext.RunAsync(CancellationToken.None);
        var result = _combinedContext.GetCustomFilters();

        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().ContainKey("Filter1");
        result.Should().ContainKey("Filter2");

        result["Filter1"].Should().Be<int>();
        result["Filter2"].Should().Be<DateTime>();
    }

    [TestMethod]
    public async Task DeletePacketsAsync_WithInnerContexts_CallsAllContexts()
    {
        var packetA1 = new PacketDto { Id = 1, ConnectorName = "ConnectorA", Channel = "Test", DateCreated = DateTime.Now, Data = "data1", ParentId = null, Status = PacketStatus.Enqueued };
        var packetA2 = new PacketDto { Id = 2, ConnectorName = "ConnectorA", Channel = "Test", DateCreated = DateTime.Now, Data = "data2", ParentId = null, Status = PacketStatus.Enqueued };
        var packetB1 = new PacketDto { Id = 3, ConnectorName = "ConnectorB", Channel = "Test", DateCreated = DateTime.Now, Data = "data3", ParentId = null, Status = PacketStatus.Enqueued };
        var packets = new HashSet<PacketDto> { packetA1, packetA2, packetB1 };

        await _combinedContext.DeletePacketsAsync(packets);

        await _connectorA.Received(1).DeletePacketsAsync(
            Arg.Is<HashSet<PacketDto>>(set => set.SetEquals(new[] { packetA1, packetA2 })));
        await _connectorB.Received(1).DeletePacketsAsync(
            Arg.Is<HashSet<PacketDto>>(set => set.SetEquals(new[] { packetB1 })));
    }

    [TestMethod]
    public async Task ResendPacketsAsync_WithInnerContexts_CallsAllContexts()
    {
        var packetA = new PacketDto { Id = 1, ConnectorName = "ConnectorA", Channel = "Test", DateCreated = DateTime.Now, Data = "data1", ParentId = null, Status = PacketStatus.Enqueued };
        var packetB = new PacketDto { Id = 2, ConnectorName = "ConnectorB", Channel = "Test", DateCreated = DateTime.Now, Data = "data2", ParentId = null, Status = PacketStatus.Enqueued };
        var packets = new HashSet<PacketDto> { packetA, packetB };

        await _combinedContext.ResendPacketsAsync(packets);

        await _connectorA.Received(1).ResendPacketsAsync(
            Arg.Is<HashSet<PacketDto>>(set => set.SetEquals(new[] { packetA })));
        await _connectorB.Received(1).ResendPacketsAsync(
            Arg.Is<HashSet<PacketDto>>(set => set.SetEquals(new[] { packetB })));
    }

    [TestMethod]
    public async Task StopPacketsAsync_WithInnerContexts_CallsAllContexts()
    {
        var packetA = new PacketDto { Id = 1, ConnectorName = "ConnectorA", Channel = "Test", DateCreated = DateTime.Now, Data = "data1", ParentId = null, Status = PacketStatus.Enqueued };
        var packetB = new PacketDto { Id = 2, ConnectorName = "ConnectorB", Channel = "Test", DateCreated = DateTime.Now, Data = "data2", ParentId = null, Status = PacketStatus.Enqueued };
        var packets = new HashSet<PacketDto> { packetA, packetB };

        await _combinedContext.StopPacketsAsync(packets);

        await _connectorA.Received(1).StopPacketsAsync(
            Arg.Is<HashSet<PacketDto>>(set => set.SetEquals(new[] { packetA })));
        await _connectorB.Received(1).StopPacketsAsync(
            Arg.Is<HashSet<PacketDto>>(set => set.SetEquals(new[] { packetB })));
    }

    [TestMethod]
    public async Task ImportPacketsAsync_WhenAnyImportFails_ReturnsFalse()
    {
        var packetA = new PacketDto { Id = 1, ConnectorName = "ConnectorA", Channel = "Test", DateCreated = DateTime.Now, Data = "data1", ParentId = null, Status = PacketStatus.Enqueued };
        var packetB = new PacketDto { Id = 2, ConnectorName = "ConnectorB", Channel = "Test", DateCreated = DateTime.Now, Data = "data2", ParentId = null, Status = PacketStatus.Enqueued };
        var packets = new List<PacketDto> { packetA, packetB };
        var importData = new ConnectorPacketsExportDto([.. packets]);

        _connectorA.ImportPacketsAsync(Arg.Any<ConnectorPacketsExportDto>(), Arg.Any<bool>())
            .Returns(await Task.FromResult(false));
        _connectorB.ImportPacketsAsync(Arg.Any<ConnectorPacketsExportDto>(), Arg.Any<bool>())
            .Returns(await Task.FromResult(true));

        var result = await _combinedContext.ImportPacketsAsync(importData, false);

        result.Should().BeFalse();
        await _connectorA.Received(1).ImportPacketsAsync(importData, false);
        await _connectorB.DidNotReceive().ImportPacketsAsync(importData, false);
    }

    [TestMethod]
    public async Task ImportPacketsAsync_WhenAllImportsSucceed_ReturnsTrue()
    {
        var packetA = new PacketDto { Id = 1, ConnectorName = "ConnectorA", Channel = "Test", DateCreated = DateTime.Now, Data = "data1", ParentId = null, Status = PacketStatus.Enqueued };
        var packetB = new PacketDto { Id = 2, ConnectorName = "ConnectorB", Channel = "Test", DateCreated = DateTime.Now, Data = "data2", ParentId = null, Status = PacketStatus.Enqueued };
        var packets = new List<PacketDto> { packetA, packetB };
        var importData = new ConnectorPacketsExportDto([.. packets]);

        _connectorA.ImportPacketsAsync(Arg.Any<ConnectorPacketsExportDto>(), Arg.Any<bool>())
            .Returns(await Task.FromResult(true));
        _connectorB.ImportPacketsAsync(Arg.Any<ConnectorPacketsExportDto>(), Arg.Any<bool>())
            .Returns(await Task.FromResult(true));

        var result = await _combinedContext.ImportPacketsAsync(importData, true);

        result.Should().BeTrue();
        await _connectorA.Received(1).ImportPacketsAsync(importData, true);
        await _connectorB.Received(1).ImportPacketsAsync(importData, true);
    }

    public async Task TestCleanup()
    {
        await _cancellationTokenSource.CancelAsync();
    }
}
