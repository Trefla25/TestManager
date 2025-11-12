using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eHub.PlugIn;
using eHub.UI.State;
using FluentAssertions;

namespace eHub.UI.Tests.State;

public class JsonViewerScopedContextTests
{
    private readonly ConnectorIdentifier _connectorIdentifier = new("instance", "JsonViewer");
    private JsonViewerScopedContext? _jsonViewerScopedContext;
    private ConnectorPacketsExportDto? _exportData;
    private DateTime _startDateTime;
    private DateTime _endDateTime;

    [Fact]
    public void Constructor_WithDateRangeFilters_FiltersAndGroupsPackets()
    {
        // Arrange
        _startDateTime = new DateTime(2024, 1, 1);
        _endDateTime = new DateTime(2024, 12, 31);

        var packets = new List<PacketDto>
        {
            new()
            {
                Id = 1, ConnectorName = "Connector1", Channel = "Channel1", DateCreated = new DateTime(2023, 12, 30),
                ParentId = null, Status = PacketStatus.Enqueued
            },
            new()
            {
                Id = 2, ConnectorName = "Connector2", Channel = "Channel2", DateCreated = new DateTime(2024, 6, 1),
                ParentId = null, Status = PacketStatus.Enqueued
            },
            new()
            {
                Id = 3, ConnectorName = "Connector1", Channel = "Channel1", DateCreated = new DateTime(2024, 8, 29),
                ParentId = null, Status = PacketStatus.Enqueued
            },
            new()
            {
                Id = 4, ConnectorName = "Connector2", Channel = "Channel1", DateCreated = new DateTime(2024, 12, 1),
                ParentId = null, Status = PacketStatus.Enqueued
            }
        };

        _exportData = new ConnectorPacketsExportDto([.. packets]);
        _jsonViewerScopedContext =
            new JsonViewerScopedContext(_connectorIdentifier, _exportData, _startDateTime, _endDateTime);

        // Assert
        _jsonViewerScopedContext.Packets.Count.Should().Be(3);
        _jsonViewerScopedContext.Packets.Should()
            .NotContain(p => p.DateCreated < _startDateTime || p.DateCreated >= _endDateTime);

        _jsonViewerScopedContext.GroupedPackets.Count.Should().Be(3);
        _jsonViewerScopedContext.GroupedPackets.SelectMany(g => g.Value).Should()
            .NotContain(p => p.DateCreated < _startDateTime || p.DateCreated >= _endDateTime);
        _jsonViewerScopedContext.GroupedPackets.Should()
            .ContainKey(new PacketGroupIdentifier("Connector1", "Channel1"));
        _jsonViewerScopedContext.GroupedPackets.Should()
            .ContainKey(new PacketGroupIdentifier("Connector2", "Channel2"));
        _jsonViewerScopedContext.GroupedPackets.Should()
            .ContainKey(new PacketGroupIdentifier("Connector2", "Channel1"));
    }

    [Fact]
    public void IsFilterRunning_Get_AlwaysFalse()
    {
        // Arrange
        _jsonViewerScopedContext =
            new JsonViewerScopedContext(_connectorIdentifier, new([]), DateTime.MinValue, DateTime.MaxValue);

        // Assert
        _jsonViewerScopedContext.IsFilterRunning.Should().BeFalse();
    }

    [Fact]
    public void GetUIViewConfig_WhenCalled_ShouldReturnExpectedConfig()
    {
        // Arrange
        _startDateTime = new DateTime(2024, 1, 1);
        _endDateTime = new DateTime(2024, 12, 31);

        var packets = new List<PacketDto>
        {
            new()
            {
                Id = 1, ConnectorName = "Connector1", Channel = "Channel1", DateCreated = new DateTime(2023, 12, 30),
                ParentId = null, Status = PacketStatus.Enqueued
            },
            new()
            {
                Id = 2, ConnectorName = "Connector2", Channel = "Channel2", DateCreated = new DateTime(2024, 6, 1),
                ParentId = null, Status = PacketStatus.Enqueued
            },
            new()
            {
                Id = 3, ConnectorName = "Connector1", Channel = "Channel1", DateCreated = new DateTime(2024, 12, 29),
                ParentId = null, Status = PacketStatus.Enqueued
            }
        };

        _exportData = new ConnectorPacketsExportDto([.. packets]);
        _jsonViewerScopedContext =
            new JsonViewerScopedContext(_connectorIdentifier, _exportData, _startDateTime, _endDateTime);

        // Act
        var result = _jsonViewerScopedContext.GetUIViewConfig();

        // Assert
        result.Should().NotBeNull();
        result.Views.Should().ContainKey("All");
        result.Views["All"].Channels.Should().HaveCount(2);
        result.Views["All"].Channels.Should().Contain(c => c.Name == "Channel1");
        result.Views["All"].Channels.Should().Contain(c => c.Name == "Channel2");
    }

    [Fact]
    public void GetCustomFilters_WhenCalled_ReturnsEmpty()
    {
        // Arrange
        _jsonViewerScopedContext =
            new JsonViewerScopedContext(_connectorIdentifier, new([]), DateTime.MinValue, DateTime.MaxValue);

        // Act
        var result = _jsonViewerScopedContext.GetCustomFilters();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void ApplyFilter_WithDateRangeFilters_UpdatesFilteredPackets()
    {
        // Arrange
        _startDateTime = new DateTime(2024, 1, 1);
        _endDateTime = new DateTime(2024, 12, 31);

        var packets = new List<PacketDto>
        {
            new()
            {
                Id = 1, ConnectorName = "Connector1", Channel = "Channel1", DateCreated = new DateTime(2023, 12, 30),
                ParentId = null, Status = PacketStatus.Enqueued
            },
            new()
            {
                Id = 2, ConnectorName = "Connector2", Channel = "Channel2", DateCreated = new DateTime(2024, 6, 1),
                ParentId = null, Status = PacketStatus.Enqueued
            },
            new()
            {
                Id = 3, ConnectorName = "Connector1", Channel = "Channel1", DateCreated = new DateTime(2024, 12, 29),
                ParentId = null, Status = PacketStatus.Enqueued
            },
        };

        _exportData = new ConnectorPacketsExportDto([.. packets]);

        _jsonViewerScopedContext =
            new JsonViewerScopedContext(_connectorIdentifier, _exportData, _startDateTime, _endDateTime);

        var raisedEvents = new List<(ConnectorIdentifier Identifier, ConnectorPacketsChangedDto Changed)>();
        _jsonViewerScopedContext.OnConnectorPacketsChanged += (x, y) => raisedEvents.Add((x, y));

        var newStartDate = new DateTime(2024, 5, 1);
        var newEndDate = new DateTime(2024, 7, 1);

        // Act
        _jsonViewerScopedContext.ApplyFilter(newStartDate, newEndDate);

        // Assert
        _jsonViewerScopedContext.Packets.Count.Should().Be(1);
        _jsonViewerScopedContext.Packets.First().Id.Should().Be(2);

        raisedEvents.Should().ContainSingle();
        raisedEvents.First().Identifier.Should().Be(_connectorIdentifier);
        raisedEvents.First().Changed.Should().Be(ConnectorPacketsChangedDto.Any);
    }

    [Fact]
    public void ApplyFilter_WhenNoSubscribers_DoesNotThrow()
    {
        // Arrange
        _jsonViewerScopedContext =
            new JsonViewerScopedContext(_connectorIdentifier, new([]), DateTime.MinValue, DateTime.MaxValue);

        // Act
        var act = () => _jsonViewerScopedContext.ApplyFilter();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task RunAsync_WhenCalled_CompletesSuccessfully()
    {
        // Arrange
        _jsonViewerScopedContext =
            new JsonViewerScopedContext(_connectorIdentifier, new([]), DateTime.MinValue, DateTime.MaxValue);

        // Act
        var action = async () => await _jsonViewerScopedContext.RunAsync(CancellationToken.None);

        // Assert
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StartFilterRunner_WhenCalled_ThrowsNotImplementedException()
    {
        // Arrange
        _jsonViewerScopedContext =
            new JsonViewerScopedContext(_connectorIdentifier, new([]), DateTime.MinValue, DateTime.MaxValue);

        // Act
        var action = async () => await _jsonViewerScopedContext.StartFilterRunner();

        // Assert
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    public async Task StopFilterRunner_WhenCalled_ThrowsNotImplementedException()
    {
        // Arrange
        _jsonViewerScopedContext =
            new JsonViewerScopedContext(_connectorIdentifier, new([]), DateTime.MinValue, DateTime.MaxValue);

        // Act
        var action = async () => await _jsonViewerScopedContext.StopFilterRunner();

        // Assert
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    public async Task StopPacketsAsync_WhenCalled_ThrowsNotImplementedException()
    {
        // Arrange
        _jsonViewerScopedContext =
            new JsonViewerScopedContext(_connectorIdentifier, new([]), DateTime.MinValue, DateTime.MaxValue);

        // Act
        var action = async () => await _jsonViewerScopedContext.StopPacketsAsync([]);

        // Assert
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    public async Task DeletePacketsAsync_WhenCalled_ThrowsNotImplementedException()
    {
        // Arrange
        _jsonViewerScopedContext =
            new JsonViewerScopedContext(_connectorIdentifier, new([]), DateTime.MinValue, DateTime.MaxValue);

        // Act
        var action = async () => await _jsonViewerScopedContext.DeletePacketsAsync([]);

        // Assert
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    public async Task ResendPacketsAsync_WhenCalled_ThrowsNotImplementedException()
    {
        // Arrange
        _jsonViewerScopedContext =
            new JsonViewerScopedContext(_connectorIdentifier, new([]), DateTime.MinValue, DateTime.MaxValue);

        // Act
        var action = async () => await _jsonViewerScopedContext.ResendPacketsAsync([]);

        // Assert
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    public async Task ImportPacketsAsync_WhenCalled_ThrowsNotImplementedException()
    {
        // Arrange
        _jsonViewerScopedContext =
            new JsonViewerScopedContext(_connectorIdentifier, new([]), DateTime.MinValue, DateTime.MaxValue);

        // Act
        var action = async () => await _jsonViewerScopedContext.ImportPacketsAsync(new([]), false);

        // Assert
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    public async Task DisposeAsync_WhenCalled_CompletesSuccessfully()
    {
        // Arrange
        _jsonViewerScopedContext =
            new JsonViewerScopedContext(_connectorIdentifier, new([]), DateTime.MinValue, DateTime.MaxValue);

        // Act
        var action = async () => await _jsonViewerScopedContext.DisposeAsync();

        // Assert
        await action.Should().NotThrowAsync();
    }
}