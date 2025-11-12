using System.Collections.Immutable;
using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eHub.PlugIn;
using eHub.PlugIn.UI;
using eHub.UI.State;
using eMessenger;
using eMessenger.Tests;
using FluentAssertions;

namespace eHub.UI.Tests.State;

public class ConnectorContextTests
{
    private readonly ConnectorContext _connectorContext;
    private readonly ConnectorIdentifier _connectorIdentifier;
    private readonly ConnectorUiData _connectorUiData;
    private readonly IMessenger _messenger;
    public ConnectorContextTests()
    {
        _connectorIdentifier = new ConnectorIdentifier(Guid.NewGuid().ToString(), "TestConnector");
        _connectorUiData = new ConnectorUiData("TestConnector", "TestConnector", new UIViewConfig());
        _messenger = TestingMessenger.Create();
        _connectorContext = new ConnectorContext(_connectorIdentifier, _connectorUiData, _messenger);
    }

    [Fact]
    public async Task RunAsync_WhenCalled_RegistersListener()
    {
        // Act
        await _connectorContext.RunAsync(CancellationToken.None);

        var raisedEvents = new List<(ConnectorIdentifier Identifier, ConnectorPacketsChangedDto Changed)>();
        _connectorContext.OnConnectorPacketsChanged += (x, y) => raisedEvents.Add((x, y));

        var expectedChangedDto = new ConnectorPacketsChangedDto(new(DateTime.Now.AddDays(-1), DateTime.Now));
        await _messenger.SendAsync(ConnectorContract.PacketsChangedTopic(_connectorIdentifier), expectedChangedDto);
        
        // Assert
        raisedEvents.Should().ContainSingle();
        raisedEvents.First().Identifier.Should().Be(_connectorIdentifier);
        raisedEvents.First().Changed.FilterHint.Should().Be(expectedChangedDto.FilterHint);
    }

    [Fact]
    public async Task ConnectorPacketsChangedHandler_WhenNoSubscribers_DoesNotThrow()
    {
        // Arrange
        await _connectorContext.RunAsync(CancellationToken.None);
        
        // Act
        var act = async () => await _messenger.SendAsync(ConnectorContract.PacketsChangedTopic(_connectorIdentifier), new ConnectorPacketsChangedDto(new()));
        
        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void GetUIViewConfig_WhenCalled_ReturnsExpectedConfig()
    {
        // Act
        var result = _connectorContext.GetUIViewConfig();
        
        // Assert
        result.Should().Be(_connectorUiData.UIViewConfig);
    }

    [Fact]
    public async Task GetCustomFilters_WithValidFilters_ReturnsExpectedFilters()
    {
        // Arrange
        var customFilters = new Dictionary<string, string>
        {
            { "Filter1", typeof(int).ToString() },
            { "Filter2", typeof(string).ToString() },
            { "Filter3", typeof(DateTime).ToString() }
        }.AsReadOnly();

        await _messenger.AnswerAsync(
            ConnectorContract.GetCustomFiltersTopic(_connectorIdentifier),
            () => customFilters);

        await _connectorContext.RunAsync(CancellationToken.None);
        
        // Act
        var result = _connectorContext.GetCustomFilters();
        
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
    public async Task GetCustomFilters_WhenNoListener_ReturnsEmpty()
    {
        // Arrange
        await _connectorContext.RunAsync(CancellationToken.None);
        
        // Act
        var result = _connectorContext.GetCustomFilters();
        
        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task StopPacketsAsync_WhenCalled_SendsCorrectRequest()
    {
        // Arrange
        var packets = new HashSet<PacketDto>
        {
            new() { Id = 1, Channel = "Test", DateCreated = DateTime.Now, Data = "data1", ParentId = null, Status = PacketStatus.Enqueued },
            new() { Id = 2, Channel = "Test", DateCreated = DateTime.Now, Data = "data2", ParentId = null, Status = PacketStatus.Enqueued }
        };

        ImmutableArray<long>? receivedPacketIds = null;
        await _messenger.ListenAsync<ImmutableArray<long>>(
            ConnectorContract.PacketManualStopSequenceTopic(_connectorIdentifier),
            x => receivedPacketIds = x);
        
        // Act
        await _connectorContext.StopPacketsAsync(packets);
        
        // Assert
        receivedPacketIds.Should().NotBeNull();
        receivedPacketIds.Should().BeEquivalentTo(new long[] { 1, 2 });
    }

    [Fact]
    public async Task DeletePacketsAsync_WhenCalled_SendsCorrectRequest()
    {
        // Arrange
        var packets = new HashSet<PacketDto>
        {
            new() { Id = 1, Channel = "Test", DateCreated = DateTime.Now, Data = "data1", ParentId = null, Status = PacketStatus.Enqueued },
            new() { Id = 2, Channel = "Test", DateCreated = DateTime.Now, Data = "data2", ParentId = null, Status = PacketStatus.Enqueued }
        };

        ImmutableArray<long>? receivedPacketIds = null;
        await _messenger.ListenAsync<ImmutableArray<long>>(
            ConnectorContract.PacketDeleteSequenceTopic(_connectorIdentifier),
            x => receivedPacketIds = x);
        
        // Act
        await _connectorContext.DeletePacketsAsync(packets);
        
        // Assert
        receivedPacketIds.Should().NotBeNull();
        receivedPacketIds.Should().BeEquivalentTo(new long[] { 1, 2 });
    }

    [Fact]
    public async Task ResendPacketsAsync_WhenCalled_SendsCorrectRequest()
    {
        // Arrange
        var packets = new HashSet<PacketDto>
        {
            new() { Id = 1, Channel = "Test", DateCreated = DateTime.Now, Data = "data1", ParentId = null, Status = PacketStatus.Enqueued },
            new() { Id = 2, Channel = "Test", DateCreated = DateTime.Now, Data = "data2", ParentId = null, Status = PacketStatus.Enqueued }
        };

        ImmutableArray<PacketResendDto>? receivedPacketDtos = null;
        await _messenger.ListenAsync<ImmutableArray<PacketResendDto>>(
            ConnectorContract.PacketResendTopic(_connectorIdentifier),
            x => receivedPacketDtos = x);

        // Act
        await _connectorContext.ResendPacketsAsync(packets);
        var packetResendDtos = packets.Select(packet => new PacketResendDto(packet.Id, packet.Data)).ToImmutableArray();
        
        // Assert
        receivedPacketDtos.Should().NotBeNull();
        receivedPacketDtos.Should().BeEquivalentTo(packetResendDtos);
    }

    [Fact]
    public async Task ImportPacketsAsync_WhenCalled_SendsCorrectRequestAndReturnsExpectedResult()
    {
        // Arrange
        var packets = new HashSet<PacketDto>
        {
            new() { Id = 1, Channel = "Test", DateCreated = DateTime.Now, Data = "data1", ParentId = null, Status = PacketStatus.Enqueued },
            new() { Id = 2, Channel = "Test", DateCreated = DateTime.Now, Data = "data2", ParentId = null, Status = PacketStatus.Enqueued }
        };

        var importData = new ConnectorPacketsExportDto([.. packets]);

        ConnectorPacketsTryImportDto? receivedImportDto = null;
        await _messenger.AnswerAsync<ConnectorPacketsTryImportDto, bool>(
            ConnectorContract.PacketTryImportTopic(_connectorIdentifier),
            x => { receivedImportDto = x; return true; });
        
        // Act
        var result = await _connectorContext.ImportPacketsAsync(importData, true);
        
        // Assert
        result.Should().BeTrue();
        receivedImportDto.Should().NotBeNull();
        receivedImportDto.Export.Should().BeEquivalentTo(importData);
        receivedImportDto.ForceImport.Should().BeTrue();
    }
}