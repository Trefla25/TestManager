using System.Diagnostics.CodeAnalysis;
using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eHub.PlugIn;
using eHub.PlugIn.UI;
using eHub.UI.Models;
using eHub.UI.Services;
using eHub.UI.State;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor;
using NSubstitute;

namespace eHub.UI.Tests.State;

[TestClass]
public class CombinedConnectorScopedContextTests
{
    private ConnectorIdentifier _connectorIdentifier = default!;
    private IConnectorContext _connectorContext = default!;
    private IConnectorRegistry _connectorRegistry = default!;
    private IConnectorScopedContextProvider _scopedContextProvider = default!;
    private Dictionary<string, IConnectorScopedContext> _connectorScopes = [];

    private CombinedConnectorScopedContext? _combinedScopedContext;
    private UIViewConfig? _uIViewConfig;

    [TestInitialize]
    public void TestInitialize()
    {
        _connectorIdentifier = new ConnectorIdentifier("instance", "Combined");
        _connectorContext = Substitute.For<IConnectorContext>();
        _connectorRegistry = Substitute.For<IConnectorRegistry>();
        _scopedContextProvider = Substitute.For<IConnectorScopedContextProvider>();
    }

    [TestMethod]
    public void GetUIViewConfig_WhenCalled_ReturnsExpectedConfig()
    {
        InitializeCombinedConnectorScopedContext(["ConnectorA", "ConnectorB"]);
        _connectorContext.GetUIViewConfig().Returns(_uIViewConfig);

        var result = _combinedScopedContext.GetUIViewConfig();

        result.Should().Be(_uIViewConfig);
    }

    [TestMethod]
    public void GetCustomFilters_ReturnsExpectedFilters()
    {
        InitializeCombinedConnectorScopedContext(["ConnectorA", "ConnectorB"]);

        var customFilters = new Dictionary<string, Type>
        {
            { "Filter1", typeof(int) },
            { "Filter2", typeof(string) },
            { "Filter3", typeof(DateTime) }
        }.AsReadOnly();

        _connectorContext.GetCustomFilters().Returns(customFilters);

        var result = _combinedScopedContext.GetCustomFilters();

        result.Should().BeSameAs(customFilters);
    }

    [TestMethod]
    public void ApplyFilter_WithNewValues_CallsAllScopedContexts()
    {
        InitializeCombinedConnectorScopedContext(["ConnectorA", "ConnectorB"]);

        var newStartDateTime = new DateTime(2024, 1, 1);
        var newEndDateTime = new DateTime(2024, 12, 31);
        var newColumnFilters = new List<PacketColumnFilter>
        {
            new() { ColumnName = nameof(PacketDto.Channel), Operator = FilterOperator.String.Contains, Value = "Value1" },
            new() { ColumnName = nameof(PacketDto.Id), Operator = FilterOperator.Number.GreaterThan, Value = "10" },
        };

        _combinedScopedContext.ApplyFilter(newStartDateTime, newEndDateTime, newColumnFilters);

        _combinedScopedContext.FilterOptions.StartDateTime.Should().Be(newStartDateTime);
        _combinedScopedContext.FilterOptions.EndDateTime.Should().Be(newEndDateTime);
        _combinedScopedContext.FilterOptions.ColumnFilters.Should().BeEquivalentTo(newColumnFilters);

        foreach (var scopedContext in _connectorScopes.Values)
        {
            scopedContext.Received(1).ApplyFilter(newStartDateTime, newEndDateTime, newColumnFilters);
        }
    }

    [TestMethod]
    public async Task StartFilterRunner_WhenCalled_CallsAllScopedContexts()
    {
        InitializeCombinedConnectorScopedContext(["ConnectorA", "ConnectorB"]);

        await _combinedScopedContext.StartFilterRunner();

        foreach (var scopedContext in _connectorScopes.Values)
        {
            await scopedContext.Received(1).StartFilterRunner();
        }

        _combinedScopedContext.IsFilterRunning.Should().BeTrue();
    }

    [TestMethod]
    public async Task StopFilterRunner_WhenCalled_CallsAllScopedContexts()
    {
        InitializeCombinedConnectorScopedContext(["ConnectorA", "ConnectorB"]);

        await _combinedScopedContext.StopFilterRunner();

        foreach (var scopedContext in _connectorScopes.Values)
        {
            await scopedContext.Received(1).StopFilterRunner();
        }

        _combinedScopedContext.IsFilterRunning.Should().BeFalse();
    }

    [TestMethod]
    public async Task RunAsync_WhenCalled_CallsConnectorContext()
    {
        InitializeCombinedConnectorScopedContext(["ConnectorA", "ConnectorB"]);

        var cancellationToken = CancellationToken.None;
        await _combinedScopedContext.RunAsync(cancellationToken);

        await _connectorContext.Received(1).RunAsync(cancellationToken);
    }

    [TestMethod]
    public async Task DeletePacketsAsync_WhenCalled_CallsConnectorContext()
    {
        InitializeCombinedConnectorScopedContext(["ConnectorA", "ConnectorB"]);

        var packet1 = new PacketDto { Id = 1, ConnectorName = "ConnectorA", Channel = "Test", DateCreated = DateTime.Now, ParentId = null, Status = PacketStatus.Enqueued };
        var packet2 = new PacketDto { Id = 1, ConnectorName = "ConnectorB", Channel = "Test", DateCreated = DateTime.Now, ParentId = null, Status = PacketStatus.Enqueued };
        var packets = new HashSet<PacketDto> { packet1, packet2 };

        await _combinedScopedContext.DeletePacketsAsync(packets);

        await _connectorContext.Received(1).DeletePacketsAsync(packets);
    }

    [TestMethod]
    public async Task ResendPacketsAsync_WhenCalled_CallsConnectorContext()
    {
        InitializeCombinedConnectorScopedContext(["ConnectorA", "ConnectorB"]);

        var packet1 = new PacketDto { Id = 1, ConnectorName = "ConnectorA", Channel = "Test", DateCreated = DateTime.Now, ParentId = null, Status = PacketStatus.Enqueued };
        var packet2 = new PacketDto { Id = 1, ConnectorName = "ConnectorB", Channel = "Test", DateCreated = DateTime.Now, ParentId = null, Status = PacketStatus.Enqueued };
        var packets = new HashSet<PacketDto> { packet1, packet2 };

        await _combinedScopedContext.ResendPacketsAsync(packets);

        await _connectorContext.Received(1).ResendPacketsAsync(packets);
    }

    [TestMethod]
    public async Task StopPacketsAsync_WhenCalled_CallsConnectorContext()
    {
        InitializeCombinedConnectorScopedContext(["ConnectorA", "ConnectorB"]);

        var packet1 = new PacketDto {Id = 1, ConnectorName = "ConnectorA", Channel = "Test", DateCreated = DateTime.Now, ParentId = null, Status = PacketStatus.Enqueued };
        var packet2 = new PacketDto {Id = 1, ConnectorName = "ConnectorB", Channel = "Test", DateCreated = DateTime.Now, ParentId = null, Status = PacketStatus.Enqueued };
        var packets = new HashSet<PacketDto> { packet1, packet2 };

        await _combinedScopedContext.StopPacketsAsync(packets);

        await _connectorContext.Received(1).StopPacketsAsync(packets);
    }

    [TestMethod]
    public async Task ImportPacketsAsync_WhenCalled_CallsConnectorContext()
    {
        InitializeCombinedConnectorScopedContext(["ConnectorA", "ConnectorB"]);

        var packet1 = new PacketDto { Id = 1, ConnectorName = "ConnectorA", Channel = "Test", DateCreated = DateTime.Now, ParentId = null, Status = PacketStatus.Enqueued };
        var packet2 = new PacketDto { Id = 1, ConnectorName = "ConnectorB", Channel = "Test", DateCreated = DateTime.Now, ParentId = null, Status = PacketStatus.Enqueued };
        var packets = new HashSet<PacketDto> { packet1, packet2 };
        var importData = new ConnectorPacketsExportDto([.. packets]);

        _connectorContext.ImportPacketsAsync(importData, true).Returns(true);

        var result = await _combinedScopedContext.ImportPacketsAsync(importData, true);

        result.Should().BeTrue();
        await _connectorContext.Received(1).ImportPacketsAsync(importData, true);
    }

    [TestMethod]
    public void ConnectorPacketsChangedHandler_WhenCalled_WhenNewPacketsFetched_AddsPackets()
    {
        InitializeCombinedConnectorScopedContext(["ConnectorA", "ConnectorB"]);

        var now = DateTime.Now;
        var packet1 = new PacketDto { Id = 1, ConnectorName = "ConnectorA", Channel = "ChannelA", DateCreated = now, Data = "data1", ParentId = null, Status = PacketStatus.Enqueued };
        var packet2 = new PacketDto { Id = 1, ConnectorName = "ConnectorB", Channel = "ChannelB", DateCreated = now, Data = "data2", ParentId = null, Status = PacketStatus.Enqueued };
        var packetsA = new HashSet<PacketDto> { packet1 };
        var packetsB = new HashSet<PacketDto> { packet2 };

        _connectorScopes["ConnectorA"].Packets.Returns(packetsA);
        _connectorScopes["ConnectorB"].Packets.Returns(packetsB);

        _connectorScopes["ConnectorA"].OnConnectorPacketsChanged += Raise.Event<OnConnectorPacketsChangedDelegate>(new ConnectorIdentifier("instance", "ConnectorA"), ConnectorPacketsChangedDto.Any);
        _connectorScopes["ConnectorB"].OnConnectorPacketsChanged += Raise.Event<OnConnectorPacketsChangedDelegate>(new ConnectorIdentifier("instance", "ConnectorB"), ConnectorPacketsChangedDto.Any);

        _combinedScopedContext.Packets.Should().HaveCount(2);

        var groupKey1 = new PacketGroupIdentifier(packet1.ConnectorName, packet1.Channel);
        var groupKey2 = new PacketGroupIdentifier(packet2.ConnectorName, packet2.Channel);
        _combinedScopedContext.GroupedPackets.Should().ContainKey(groupKey1);
        _combinedScopedContext.GroupedPackets.Should().ContainKey(groupKey2);
        _combinedScopedContext.GroupedPackets[groupKey1].Should().Contain(packet1);
        _combinedScopedContext.GroupedPackets[groupKey2].Should().Contain(packet2);
    }

    [TestMethod]
    public void ConnectorPacketsChangedHandler_WhenPacketsChangedChannel_UpdatesPacketGroups()
    {
        InitializeCombinedConnectorScopedContext(["ConnectorA", "ConnectorB"]);

        var now = DateTime.Now;
        var packet1 = new PacketDto { Id = 1, ConnectorName = "ConnectorA", Channel = "A", DateCreated = now, Data = "Data1", ParentId = null, Status = PacketStatus.Enqueued };
        var packet2 = new PacketDto { Id = 2, ConnectorName = "ConnectorA", Channel = "B", DateCreated = now, Data = "Data2", ParentId = null, Status = PacketStatus.Enqueued };
        var packet3 = new PacketDto { Id = 3, ConnectorName = "ConnectorA", Channel = "C", DateCreated = now, Data = "Data3", ParentId = null, Status = PacketStatus.Enqueued };
        var packet4 = new PacketDto { Id = 1, ConnectorName = "ConnectorB", Channel = "D", DateCreated = now, Data = "Data4", ParentId = null, Status = PacketStatus.Enqueued };
        var packet5 = new PacketDto { Id = 2, ConnectorName = "ConnectorB", Channel = "E", DateCreated = now, Data = "Data5", ParentId = null, Status = PacketStatus.Enqueued };

        var packetsA = new HashSet<PacketDto> { packet1, packet2, packet3 };
        var packetsB = new HashSet<PacketDto> { packet4, packet5 };

        _connectorScopes["ConnectorA"].Packets.Returns(packetsA);
        _connectorScopes["ConnectorB"].Packets.Returns(packetsB);

        _connectorScopes["ConnectorA"].OnConnectorPacketsChanged += Raise.Event<OnConnectorPacketsChangedDelegate>(new ConnectorIdentifier("instance", "ConnectorA"), ConnectorPacketsChangedDto.Any);
        _connectorScopes["ConnectorB"].OnConnectorPacketsChanged += Raise.Event<OnConnectorPacketsChangedDelegate>(new ConnectorIdentifier("instance", "ConnectorB"), ConnectorPacketsChangedDto.Any);

        _combinedScopedContext.Packets.Should().HaveCount(5);
        _combinedScopedContext.GroupedPackets.Should().ContainKey(new PacketGroupIdentifier(packet2.ConnectorName, "B"));
        _combinedScopedContext.GroupedPackets.Should().ContainKey(new PacketGroupIdentifier(packet4.ConnectorName, "D"));

        var updatedPacket2 = packet2 with
        {
            Channel = "UpdatedB",
            Data = "Data2 Updated",
        };
        var updatedPacket4 = packet4 with
        {
            Channel = "UpdatedD",
            Data = "Data4 Updated",
        };

        packetsA.Remove(packet2);
        packetsB.Remove(packet4);
        packetsA.Add(updatedPacket2);
        packetsB.Add(updatedPacket4);

        _connectorScopes["ConnectorA"].OnConnectorPacketsChanged += Raise.Event<OnConnectorPacketsChangedDelegate>(new ConnectorIdentifier("instance", "ConnectorA"), ConnectorPacketsChangedDto.Any);
        _connectorScopes["ConnectorB"].OnConnectorPacketsChanged += Raise.Event<OnConnectorPacketsChangedDelegate>(new ConnectorIdentifier("instance", "ConnectorB"), ConnectorPacketsChangedDto.Any);

        _combinedScopedContext.Packets.Should().HaveCount(5);
        _combinedScopedContext.Packets.Should().Contain(p => p.ConnectorName == updatedPacket2.ConnectorName && p.Id == updatedPacket2.Id && p.Channel == updatedPacket2.Channel);
        _combinedScopedContext.Packets.Should().Contain(p => p.ConnectorName == updatedPacket4.ConnectorName && p.Id == updatedPacket4.Id && p.Channel == updatedPacket4.Channel);

        var groupKey2 = new PacketGroupIdentifier(updatedPacket2.ConnectorName, updatedPacket2.Channel);
        var groupKey4 = new PacketGroupIdentifier(updatedPacket4.ConnectorName, updatedPacket4.Channel);
        var oldGroupKey2 = new PacketGroupIdentifier(updatedPacket2.ConnectorName, packet2.Channel);
        var oldGroupKey4 = new PacketGroupIdentifier(updatedPacket4.ConnectorName, packet4.Channel);

        _combinedScopedContext.GroupedPackets.Should().ContainKey(groupKey2);
        _combinedScopedContext.GroupedPackets[groupKey2].Should().Contain(p => p.Id == updatedPacket2.Id);
        _combinedScopedContext.GroupedPackets[oldGroupKey2].Should().NotContain(p => p.Id == packet2.Id);

        _combinedScopedContext.GroupedPackets.Should().ContainKey(groupKey4);
        _combinedScopedContext.GroupedPackets[groupKey4].Should().Contain(p => p.Id == packet4.Id);
        _combinedScopedContext.GroupedPackets[oldGroupKey4].Should().NotContain(p => p.Id == packet4.Id);
    }

    [TestMethod]
    public void ConnectorPacketsChangedHandler_WhenExistingPacketsNotFetched_DeletePackets()
    {
        InitializeCombinedConnectorScopedContext(["ConnectorA", "ConnectorB"]);

        var now = DateTime.Now;
        var packet1 = new PacketDto { Id = 1, ConnectorName = "ConnectorA", Channel = "A", DateCreated = now, Data = "Data1", ParentId = null, Status = PacketStatus.Enqueued };
        var packet2 = new PacketDto { Id = 2, ConnectorName = "ConnectorA", Channel = "B", DateCreated = now, Data = "Data2", ParentId = null, Status = PacketStatus.Enqueued };
        var packet3 = new PacketDto { Id = 3, ConnectorName = "ConnectorA", Channel = "C", DateCreated = now, Data = "Data3", ParentId = null, Status = PacketStatus.Enqueued };
        var packet4 = new PacketDto { Id = 1, ConnectorName = "ConnectorB", Channel = "D", DateCreated = now, Data = "Data4", ParentId = null, Status = PacketStatus.Enqueued };
        var packet5 = new PacketDto { Id = 2, ConnectorName = "ConnectorB", Channel = "E", DateCreated = now, Data = "Data5", ParentId = null, Status = PacketStatus.Enqueued };

        var packetsA = new HashSet<PacketDto> { packet1, packet2, packet3 };
        var packetsB = new HashSet<PacketDto> { packet4, packet5 };

        _connectorScopes["ConnectorA"].Packets.Returns(packetsA);
        _connectorScopes["ConnectorB"].Packets.Returns(packetsB);

        _connectorScopes["ConnectorA"].OnConnectorPacketsChanged += Raise.Event<OnConnectorPacketsChangedDelegate>(new ConnectorIdentifier("instance", "ConnectorA"), ConnectorPacketsChangedDto.Any);
        _connectorScopes["ConnectorB"].OnConnectorPacketsChanged += Raise.Event<OnConnectorPacketsChangedDelegate>(new ConnectorIdentifier("instance", "ConnectorB"), ConnectorPacketsChangedDto.Any);

        _combinedScopedContext.Packets.Should().HaveCount(5);

        packetsA.Remove(packet2);
        packetsA.Remove(packet3);
        packetsB.Remove(packet4);

        _connectorScopes["ConnectorA"].OnConnectorPacketsChanged += Raise.Event<OnConnectorPacketsChangedDelegate>(new ConnectorIdentifier("instance", "ConnectorA"), ConnectorPacketsChangedDto.Any);
        _connectorScopes["ConnectorB"].OnConnectorPacketsChanged += Raise.Event<OnConnectorPacketsChangedDelegate>(new ConnectorIdentifier("instance", "ConnectorB"), ConnectorPacketsChangedDto.Any);

        _combinedScopedContext.Packets.Should().HaveCount(2);
        _combinedScopedContext.Packets.Should().Contain(p => p.Id == packet1.Id);
        _combinedScopedContext.Packets.Should().Contain(p => p.Id == packet5.Id);

        var groupKey1 = new PacketGroupIdentifier(packet1.ConnectorName, packet1.Channel);
        var groupKey2 = new PacketGroupIdentifier(packet2.ConnectorName, packet2.Channel);
        var groupKey3 = new PacketGroupIdentifier(packet3.ConnectorName, packet3.Channel);
        var groupKey4 = new PacketGroupIdentifier(packet4.ConnectorName, packet4.Channel);
        var groupKey5 = new PacketGroupIdentifier(packet5.ConnectorName, packet5.Channel);

        _combinedScopedContext.GroupedPackets[groupKey1].Should().Contain(p => p.Id == packet1.Id);
        _combinedScopedContext.GroupedPackets[groupKey2].Should().NotContain(p => p.Id == packet2.Id);
        _combinedScopedContext.GroupedPackets[groupKey3].Should().NotContain(p => p.Id == packet3.Id);
        _combinedScopedContext.GroupedPackets[groupKey4].Should().NotContain(p => p.Id == packet4.Id);
        _combinedScopedContext.GroupedPackets[groupKey5].Should().Contain(p => p.Id == packet5.Id);
    }

    [TestMethod]
    public async Task DisposeAsync_WhenCalled_DisposesAllScopedContexts()
    {
        InitializeCombinedConnectorScopedContext(["ConnectorA", "ConnectorB"]);

        await _combinedScopedContext.DisposeAsync();

        await _connectorScopes["ConnectorA"].Received(1).DisposeAsync();
        await _connectorScopes["ConnectorA"].Received(1).DisposeAsync();
    }


    [MemberNotNull(nameof(_combinedScopedContext))]
    [MemberNotNull(nameof(_uIViewConfig))]
    private void InitializeCombinedConnectorScopedContext(string[] connectorNames)
    {
        var channelConfigs = connectorNames.Select(name => new ChannelConfig { Connector = name }).ToList();
        var viewConfig = new ViewConfig
        {
            Name = "TestView",
            Channels = channelConfigs,
            Columns = []
        };

        _uIViewConfig = new UIViewConfig
        {
            Views = new Dictionary<string, ViewConfig> { { viewConfig.Name, viewConfig } },
            Filter = new UIFilterConfig(),
            ViewPacketCount = 100
        };

        var connectorUiData = new ConnectorUiData(_connectorIdentifier.ConnectorKey, "TestConnector", _uIViewConfig);

        var activeConnectors = new Dictionary<ConnectorIdentifier, ConnectorUiData>();
        foreach (var connectorName in connectorNames)
        {
            var identifier = new ConnectorIdentifier(_connectorIdentifier.Instance, connectorName);
            var scopedContext = Substitute.For<IConnectorScopedContext>();

            _connectorScopes.Add(connectorName, scopedContext);
            _scopedContextProvider.GetConnectorScopedContext(identifier).Returns(scopedContext);

            var childConnectorUiData = new ConnectorUiData(identifier.ConnectorKey, connectorName, new UIViewConfig());
            activeConnectors.Add(identifier, childConnectorUiData);
        }

        _connectorRegistry.ActiveConnectors.Returns(activeConnectors);
        _combinedScopedContext = new CombinedConnectorScopedContext(
            _connectorIdentifier,
            _connectorContext,
            connectorUiData,
            _connectorRegistry,
            _scopedContextProvider,
            NullLogger<CombinedConnectorScopedContext>.Instance);
    }
}
