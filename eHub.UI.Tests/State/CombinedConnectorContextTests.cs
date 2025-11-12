using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eHub.PlugIn;
using eHub.PlugIn.UI;
using eHub.UI.Services;
using eHub.UI.State;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace eHub.UI.Tests.State;

public class CombinedConnectorContextTests : IAsyncLifetime
{
    private CombinedConnectorContext _combinedContext = null!;
    private ConnectorIdentifier _combinedConnectorIdentifier;
    private ConnectorUiData _connectorUiData = null!;
    private ConnectorIdentifier _connectorIdentifierA;
    private ConnectorIdentifier _connectorIdentifierB;
    private IConnectorContext _connectorA = null!;
    private IConnectorContext _connectorB = null!;
    private IConnectorRegistry _connectorRegistry = null!;
    private IConnectorContextProvider _contextProvider = null!;
    private CancellationTokenSource _cancellationTokenSource = null!;

    public ValueTask InitializeAsync()
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
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task RunAsync_WithInnerContexts_SubscribesToAllContexts()
    {
        // Arrange
        var raisedEvents = new List<(ConnectorIdentifier Identifier, ConnectorPacketsChangedDto Changed)>();
        _combinedContext.OnConnectorPacketsChanged += (x, y) => raisedEvents.Add((x, y));

        // Act
        await _combinedContext.RunAsync(CancellationToken.None);

        _connectorA.OnConnectorPacketsChanged += Raise.Event<OnConnectorPacketsChangedDelegate>(_connectorIdentifierA, ConnectorPacketsChangedDto.Any);
        _connectorB.OnConnectorPacketsChanged += Raise.Event<OnConnectorPacketsChangedDelegate>(_connectorIdentifierB, ConnectorPacketsChangedDto.Any);
        
        // Assert
        raisedEvents.Should().HaveCount(2);
        raisedEvents.Should().OnlyContain(x => x.Identifier == _combinedConnectorIdentifier && x.Changed == ConnectorPacketsChangedDto.Any);
    }

    [Fact]
    public async Task ConnectorPacketsChangedHandler_WhenNoSubscribers_DoesNotThrow()
    {
        // Arrange
        await _combinedContext.RunAsync(CancellationToken.None);
        
        // Act
        var act = () =>
            _connectorA.OnConnectorPacketsChanged += Raise.Event<OnConnectorPacketsChangedDelegate>(_connectorIdentifierA, ConnectorPacketsChangedDto.Any);
        
        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void GetUIViewConfig_WhenCalled_ReturnsExpectedConfig()
    {
        // Act
        var result = _combinedContext.GetUIViewConfig();
        
        // Assert
        result.Should().Be(_connectorUiData.UIViewConfig);
    }

    [Fact]
    public async Task GetCustomFilters_WithValidFilters_ReturnsExpectedFilters()
    {
        // Arrange
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
        
        // Act
        var result = _combinedContext.GetCustomFilters();
        
        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().ContainKey("Filter1");
        result.Should().ContainKey("Filter2");
        result.Should().ContainKey("Filter3");

        result["Filter1"].Should().Be<int>();
        result["Filter2"].Should().Be<string>();
        result["Filter3"].Should().Be<DateTime>();
    }

    [Fact]
    public void GetCustomFilters_WhenNotInitialized_ReturnsEmpty()
    {
        // Arrange
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
        
        // Act
        var result = _combinedContext.GetCustomFilters();
        
        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCustomFilters_WithConflictingFilters_KeepsLatest()
    {
        // Arrange
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

        // Act
        await _combinedContext.RunAsync(CancellationToken.None);
        var result = _combinedContext.GetCustomFilters();
        
        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().ContainKey("Filter1");
        result.Should().ContainKey("Filter2");

        result["Filter1"].Should().Be<int>();
        result["Filter2"].Should().Be<DateTime>();
    }

    [Fact]
    public async Task DeletePacketsAsync_WithInnerContexts_CallsAllContexts()
    {
        // Arrange
        var packetA1 = new PacketDto { Id = 1, ConnectorName = "ConnectorA", Channel = "Test", DateCreated = DateTime.Now, Data = "data1", ParentId = null, Status = PacketStatus.Enqueued };
        var packetA2 = new PacketDto { Id = 2, ConnectorName = "ConnectorA", Channel = "Test", DateCreated = DateTime.Now, Data = "data2", ParentId = null, Status = PacketStatus.Enqueued };
        var packetB1 = new PacketDto { Id = 3, ConnectorName = "ConnectorB", Channel = "Test", DateCreated = DateTime.Now, Data = "data3", ParentId = null, Status = PacketStatus.Enqueued };
        var packets = new HashSet<PacketDto> { packetA1, packetA2, packetB1 };
        
        // Act
        await _combinedContext.DeletePacketsAsync(packets);
        
        // Assert
        await _connectorA.Received(1).DeletePacketsAsync(
            Arg.Is<HashSet<PacketDto>>(set => set.SetEquals(new[] { packetA1, packetA2 })));
        await _connectorB.Received(1).DeletePacketsAsync(
            Arg.Is<HashSet<PacketDto>>(set => set.SetEquals(new[] { packetB1 })));
    }

    [Fact]
    public async Task ResendPacketsAsync_WithInnerContexts_CallsAllContexts()
    {
        // Arrange
        var packetA = new PacketDto { Id = 1, ConnectorName = "ConnectorA", Channel = "Test", DateCreated = DateTime.Now, Data = "data1", ParentId = null, Status = PacketStatus.Enqueued };
        var packetB = new PacketDto { Id = 2, ConnectorName = "ConnectorB", Channel = "Test", DateCreated = DateTime.Now, Data = "data2", ParentId = null, Status = PacketStatus.Enqueued };
        var packets = new HashSet<PacketDto> { packetA, packetB };
        
        // Act
        await _combinedContext.ResendPacketsAsync(packets);
        
        // Assert
        await _connectorA.Received(1).ResendPacketsAsync(
            Arg.Is<HashSet<PacketDto>>(set => set.SetEquals(new[] { packetA })));
        await _connectorB.Received(1).ResendPacketsAsync(
            Arg.Is<HashSet<PacketDto>>(set => set.SetEquals(new[] { packetB })));
    }

    [Fact]
    public async Task StopPacketsAsync_WithInnerContexts_CallsAllContexts()
    {
        // Arrange
        var packetA = new PacketDto { Id = 1, ConnectorName = "ConnectorA", Channel = "Test", DateCreated = DateTime.Now, Data = "data1", ParentId = null, Status = PacketStatus.Enqueued };
        var packetB = new PacketDto { Id = 2, ConnectorName = "ConnectorB", Channel = "Test", DateCreated = DateTime.Now, Data = "data2", ParentId = null, Status = PacketStatus.Enqueued };
        var packets = new HashSet<PacketDto> { packetA, packetB };
        
        // Act
        await _combinedContext.StopPacketsAsync(packets);
        
        // Assert
        await _connectorA.Received(1).StopPacketsAsync(
            Arg.Is<HashSet<PacketDto>>(set => set.SetEquals(new[] { packetA })));
        await _connectorB.Received(1).StopPacketsAsync(
            Arg.Is<HashSet<PacketDto>>(set => set.SetEquals(new[] { packetB })));
    }

    [Fact]
    public async Task ImportPacketsAsync_WhenAnyImportFails_ReturnsFalse()
    {
        // Arrange
        var packetA = new PacketDto { Id = 1, ConnectorName = "ConnectorA", Channel = "Test", DateCreated = DateTime.Now, Data = "data1", ParentId = null, Status = PacketStatus.Enqueued };
        var packetB = new PacketDto { Id = 2, ConnectorName = "ConnectorB", Channel = "Test", DateCreated = DateTime.Now, Data = "data2", ParentId = null, Status = PacketStatus.Enqueued };
        var packets = new List<PacketDto> { packetA, packetB };
        var importData = new ConnectorPacketsExportDto([.. packets]);

        _connectorA.ImportPacketsAsync(Arg.Any<ConnectorPacketsExportDto>(), Arg.Any<bool>())
            .Returns(await Task.FromResult(false));
        _connectorB.ImportPacketsAsync(Arg.Any<ConnectorPacketsExportDto>(), Arg.Any<bool>())
            .Returns(await Task.FromResult(true));
        
        // Act
        var result = await _combinedContext.ImportPacketsAsync(importData, false);
        
        // Assert
        result.Should().BeFalse();
        await _connectorA.Received(1).ImportPacketsAsync(importData, false);
        await _connectorB.DidNotReceive().ImportPacketsAsync(importData, false);
    }

    [Fact]
    public async Task ImportPacketsAsync_WhenAllImportsSucceed_ReturnsTrue()
    {
        // Arrange
        var packetA = new PacketDto { Id = 1, ConnectorName = "ConnectorA", Channel = "Test", DateCreated = DateTime.Now, Data = "data1", ParentId = null, Status = PacketStatus.Enqueued };
        var packetB = new PacketDto { Id = 2, ConnectorName = "ConnectorB", Channel = "Test", DateCreated = DateTime.Now, Data = "data2", ParentId = null, Status = PacketStatus.Enqueued };
        var packets = new List<PacketDto> { packetA, packetB };
        var importData = new ConnectorPacketsExportDto([.. packets]);

        _connectorA.ImportPacketsAsync(Arg.Any<ConnectorPacketsExportDto>(), Arg.Any<bool>())
            .Returns(await Task.FromResult(true));
        _connectorB.ImportPacketsAsync(Arg.Any<ConnectorPacketsExportDto>(), Arg.Any<bool>())
            .Returns(await Task.FromResult(true));
        
        // Act
        var result = await _combinedContext.ImportPacketsAsync(importData, true);
        
        // Assert
        result.Should().BeTrue();
        await _connectorA.Received(1).ImportPacketsAsync(importData, true);
        await _connectorB.Received(1).ImportPacketsAsync(importData, true);
    }

    public async ValueTask DisposeAsync()
    {
        await _cancellationTokenSource.CancelAsync();
    }
}