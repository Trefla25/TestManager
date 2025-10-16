using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eHub.PlugIn;
using eHub.UI.State;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace eHub.UI.Tests.State;

[TestClass]
public class JsonViewerScopedContextTests
{
    private ConnectorIdentifier _connectorIdentifier = default!;
    private JsonViewerScopedContext? _jsonViewerScopedContext;
    private ConnectorPacketsExportDto? _exportData;
    private DateTime _startDateTime;
    private DateTime _endDateTime;

    [TestInitialize]
    public void TestInitialize()
    {
        _connectorIdentifier = new ConnectorIdentifier("instance", "JsonViewer");
    }

    [TestMethod]
    public void Constructor_WithDateRangeFilters_FiltersAndGroupsPackets()
    {
        _startDateTime = new DateTime(2024, 1, 1);
        _endDateTime = new DateTime(2024, 12, 31);

        var packets = new List<PacketDto>
        {
            new() { Id = 1, ConnectorName = "Connector1", Channel = "Channel1", DateCreated = new DateTime(2023, 12, 30), ParentId = null, Status = PacketStatus.Enqueued },
            new() { Id = 2, ConnectorName = "Connector2", Channel = "Channel2", DateCreated = new DateTime(2024, 6, 1), ParentId = null, Status = PacketStatus.Enqueued },
            new() { Id = 3, ConnectorName = "Connector1", Channel = "Channel1", DateCreated = new DateTime(2024, 8, 29), ParentId = null, Status = PacketStatus.Enqueued },
            new() { Id = 4, ConnectorName = "Connector2", Channel = "Channel1", DateCreated = new DateTime(2024, 12, 1), ParentId = null, Status = PacketStatus.Enqueued }
        };

        _exportData = new ConnectorPacketsExportDto([.. packets]);
        _jsonViewerScopedContext = new JsonViewerScopedContext(_connectorIdentifier, _exportData, _startDateTime, _endDateTime);

        _jsonViewerScopedContext.Packets.Count.Should().Be(3);
        _jsonViewerScopedContext.Packets.Should().NotContain(p => p.DateCreated < _startDateTime || p.DateCreated >= _endDateTime);

        _jsonViewerScopedContext.GroupedPackets.Count.Should().Be(3);
        _jsonViewerScopedContext.GroupedPackets.SelectMany(g => g.Value).Should().NotContain(p => p.DateCreated < _startDateTime || p.DateCreated >= _endDateTime);
        _jsonViewerScopedContext.GroupedPackets.Should().ContainKey(new PacketGroupIdentifier("Connector1", "Channel1"));
        _jsonViewerScopedContext.GroupedPackets.Should().ContainKey(new PacketGroupIdentifier("Connector2", "Channel2"));
        _jsonViewerScopedContext.GroupedPackets.Should().ContainKey(new PacketGroupIdentifier("Connector2", "Channel1"));
    }

    [TestMethod]
    public void IsFilterRunning_Get_AlwaysFalse()
    {
        _jsonViewerScopedContext = new JsonViewerScopedContext(_connectorIdentifier, new([]), DateTime.MinValue, DateTime.MaxValue);
        _jsonViewerScopedContext.IsFilterRunning.Should().BeFalse();
    }

    [TestMethod]
    public void GetUIViewConfig_WhenCalled_ShouldReturnExpectedConfig()
    {
        _startDateTime = new DateTime(2024, 1, 1);
        _endDateTime = new DateTime(2024, 12, 31);

        var packets = new List<PacketDto>
        {
            new() { Id = 1, ConnectorName = "Connector1", Channel = "Channel1", DateCreated = new DateTime(2023, 12, 30), ParentId = null, Status = PacketStatus.Enqueued },
            new() { Id = 2, ConnectorName = "Connector2", Channel = "Channel2", DateCreated = new DateTime(2024, 6, 1), ParentId = null, Status = PacketStatus.Enqueued },
            new() { Id = 3, ConnectorName = "Connector1", Channel = "Channel1", DateCreated = new DateTime(2024, 12, 29), ParentId = null, Status = PacketStatus.Enqueued }
        };

        _exportData = new ConnectorPacketsExportDto([.. packets]);
        _jsonViewerScopedContext = new JsonViewerScopedContext(_connectorIdentifier, _exportData, _startDateTime, _endDateTime);

        var result = _jsonViewerScopedContext.GetUIViewConfig();

        result.Should().NotBeNull();
        result.Views.Should().ContainKey("All");
        result.Views["All"].Channels.Should().HaveCount(2);
        result.Views["All"].Channels.Should().Contain(c => c.Name == "Channel1");
        result.Views["All"].Channels.Should().Contain(c => c.Name == "Channel2");
    }

    [TestMethod]
    public void GetCustomFilters_WhenCalled_ReturnsEmpty()
    {
        _jsonViewerScopedContext = new JsonViewerScopedContext(_connectorIdentifier, new([]), DateTime.MinValue, DateTime.MaxValue);

        var result = _jsonViewerScopedContext.GetCustomFilters();

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [TestMethod]
    public void ApplyFilter_WithDateRangeFilters_UpdatesFilteredPackets()
    {
        _startDateTime = new DateTime(2024, 1, 1);
        _endDateTime = new DateTime(2024, 12, 31);

        var packets = new List<PacketDto>
        {
            new() { Id = 1, ConnectorName = "Connector1", Channel = "Channel1", DateCreated = new DateTime(2023, 12, 30), ParentId = null, Status = PacketStatus.Enqueued },
            new() { Id = 2, ConnectorName = "Connector2", Channel = "Channel2", DateCreated = new DateTime(2024, 6, 1), ParentId = null, Status = PacketStatus.Enqueued },
            new() { Id = 3, ConnectorName = "Connector1", Channel = "Channel1", DateCreated = new DateTime(2024, 12, 29), ParentId = null, Status = PacketStatus.Enqueued },
        };

        _exportData = new ConnectorPacketsExportDto([.. packets]);

        _jsonViewerScopedContext = new JsonViewerScopedContext(_connectorIdentifier, _exportData, _startDateTime, _endDateTime);

        var raisedEvents = new List<(ConnectorIdentifier Identifier, ConnectorPacketsChangedDto Changed)>();
        _jsonViewerScopedContext.OnConnectorPacketsChanged += (x, y) => raisedEvents.Add((x, y));

        var newStartDate = new DateTime(2024, 5, 1);
        var newEndDate = new DateTime(2024, 7, 1);

        _jsonViewerScopedContext.ApplyFilter(newStartDate, newEndDate);

        _jsonViewerScopedContext.Packets.Count.Should().Be(1);
        _jsonViewerScopedContext.Packets.First().Id.Should().Be(2);

        raisedEvents.Should().ContainSingle();
        raisedEvents.First().Identifier.Should().Be(_connectorIdentifier);
        raisedEvents.First().Changed.Should().Be(ConnectorPacketsChangedDto.Any);
    }

    [TestMethod]
    public void ApplyFilter_WhenNoSubscribers_DoesNotThrow()
    {
        _jsonViewerScopedContext = new JsonViewerScopedContext(_connectorIdentifier, new([]), DateTime.MinValue, DateTime.MaxValue);

        var act = () => _jsonViewerScopedContext.ApplyFilter();

        act.Should().NotThrow();
    }

    [TestMethod]
    public async Task RunAsync_WhenCalled_CompletesSuccessfully()
    {
        _jsonViewerScopedContext = new JsonViewerScopedContext(_connectorIdentifier, new([]), DateTime.MinValue, DateTime.MaxValue);

        var action = async () => await _jsonViewerScopedContext.RunAsync(CancellationToken.None);

        await action.Should().NotThrowAsync();
    }

    [TestMethod]
    public async Task StartFilterRunner_WhenCalled_ThrowsNotImplementedException()
    {
        _jsonViewerScopedContext = new JsonViewerScopedContext(_connectorIdentifier, new([]), DateTime.MinValue, DateTime.MaxValue);

        var action = async () => await _jsonViewerScopedContext.StartFilterRunner();

        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [TestMethod]
    public async Task StopFilterRunner_WhenCalled_ThrowsNotImplementedException()
    {
        _jsonViewerScopedContext = new JsonViewerScopedContext(_connectorIdentifier, new([]), DateTime.MinValue, DateTime.MaxValue);

        var action = async () => await _jsonViewerScopedContext.StopFilterRunner();

        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [TestMethod]
    public async Task StopPacketsAsync_WhenCalled_ThrowsNotImplementedException()
    {
        _jsonViewerScopedContext = new JsonViewerScopedContext(_connectorIdentifier, new([]), DateTime.MinValue, DateTime.MaxValue);

        var action = async () => await _jsonViewerScopedContext.StopPacketsAsync([]);

        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [TestMethod]
    public async Task DeletePacketsAsync_WhenCalled_ThrowsNotImplementedException()
    {
        _jsonViewerScopedContext = new JsonViewerScopedContext(_connectorIdentifier, new([]), DateTime.MinValue, DateTime.MaxValue);

        var action = async () => await _jsonViewerScopedContext.DeletePacketsAsync([]);

        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [TestMethod]
    public async Task ResendPacketsAsync_WhenCalled_ThrowsNotImplementedException()
    {
        _jsonViewerScopedContext = new JsonViewerScopedContext(_connectorIdentifier, new([]), DateTime.MinValue, DateTime.MaxValue);

        var action = async () => await _jsonViewerScopedContext.ResendPacketsAsync([]);

        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [TestMethod]
    public async Task ImportPacketsAsync_WhenCalled_ThrowsNotImplementedException()
    {
        _jsonViewerScopedContext = new JsonViewerScopedContext(_connectorIdentifier, new([]), DateTime.MinValue, DateTime.MaxValue);

        var action = async () => await _jsonViewerScopedContext.ImportPacketsAsync(new([]), false);

        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [TestMethod]
    public async Task DisposeAsync_WhenCalled_CompletesSuccessfully()
    {
        _jsonViewerScopedContext = new JsonViewerScopedContext(_connectorIdentifier, new([]), DateTime.MinValue, DateTime.MaxValue);

        var action = async () => await _jsonViewerScopedContext.DisposeAsync();

        await action.Should().NotThrowAsync();
    }
}
