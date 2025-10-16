using eHub.PlugIn;
using eMessenger;
using eMessenger.Tests;
using FluentAssertions;
using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eHub.PlugIn.UI;
using eHub.UI.State;
using Microsoft.Extensions.Logging;
using NSubstitute;
using eHub.UI.Models;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace eHub.UI.Tests.State;

[TestClass]
public class ConnectorScopedContextTests
{
    private ConnectorScopedContext _connectorScopedContext = default!;
    private ConnectorIdentifier _connectorIdentifier;
    private IConnectorContext _connectorContext = default!;
    private ConnectorUiData _connectorUiData = default!;
    private IMessenger _messenger = default!;

    [TestInitialize]
    public void TestInitialize()
    {
        _connectorIdentifier = new ConnectorIdentifier(Guid.NewGuid().ToString(), "TestConnector");
        _connectorContext = Substitute.For<IConnectorContext>();
        _connectorUiData = new ConnectorUiData("TestConnector", "TestConnector", new UIViewConfig());
        _messenger = TestingMessenger.Create();
        _connectorScopedContext = new ConnectorScopedContext(
            _connectorIdentifier,
            _connectorContext,
            _connectorUiData,
            _messenger,
            NullLogger<ConnectorScopedContext>.Instance);
    }

    [TestMethod]
    public void GetUIViewConfig_WhenCalled_ReturnsExpectedConfig()
    {
        _connectorContext.GetUIViewConfig().Returns(_connectorUiData.UIViewConfig);

        var result = _connectorScopedContext.GetUIViewConfig();

        result.Should().Be(_connectorUiData.UIViewConfig);
    }

    [TestMethod]
    public void GetCustomFilters_ReturnsExpectedFilters()
    {
        var customFilters = new Dictionary<string, Type>
        {
            { "Filter1", typeof(int) },
            { "Filter2", typeof(string) },
            { "Filter3", typeof(DateTime) }
        }.AsReadOnly();

        _connectorContext.GetCustomFilters().Returns(customFilters);
        
        var result = _connectorScopedContext.GetCustomFilters();

        result.Should().BeSameAs(customFilters);
    }

    [TestMethod]
    public async Task ConnectorPacketsChangedHandler_WhenInvoked_ReloadsPackets()
    {
        var now = DateTime.Now;
        var packet1 = new PacketDto { Id = 1, ConnectorName = _connectorIdentifier.ConnectorKey, Channel = "A", DateCreated = now, Data = "Data1", ParentId = null, Status = PacketStatus.Enqueued };
        var packet2 = new PacketDto { Id = 2, ConnectorName = _connectorIdentifier.ConnectorKey, Channel = "B", DateCreated = now, Data = "Data2", ParentId = null, Status = PacketStatus.Enqueued };
        var packets = new List<PacketDto> { packet1, packet2 };
        var wrapper = new PacketWrapperDto()
        {
            ConnectorIdentifier = _connectorIdentifier,
            Packets = [.. packets]
        };

        await _messenger.AnswerAsync<ConnectorPacketsFilterDto, PacketWrapperDto>(
            ConnectorContract.UIPacketsGetTopic(_connectorIdentifier),
            _ => wrapper);

        var tcs = new TaskCompletionSource();

        var raisedEvents = new List<(ConnectorIdentifier Identifier, ConnectorPacketsChangedDto Changed)>();

        _connectorScopedContext.OnConnectorPacketsChanged += (id, change) =>
        {
            raisedEvents.Add((id, change));
            tcs.TrySetResult();
        };

        _connectorContext.OnConnectorPacketsChanged += Raise.Event<OnConnectorPacketsChangedDelegate>(_connectorIdentifier, ConnectorPacketsChangedDto.Any);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1));

        _connectorScopedContext.Packets.Should().HaveCount(2);
        _connectorScopedContext.Packets.Should().Contain([packet1, packet2]);

        _connectorScopedContext.GroupedPackets.Should().ContainKey(new PacketGroupIdentifier(packet1.ConnectorName, packet1.Channel));
        _connectorScopedContext.GroupedPackets.Should().ContainKey(new PacketGroupIdentifier(packet2.ConnectorName, packet2.Channel));

        raisedEvents.Should().ContainSingle();
        raisedEvents.First().Identifier.Should().Be(_connectorIdentifier);
        raisedEvents.First().Changed.Should().Be(ConnectorPacketsChangedDto.Any);
    }

    [TestMethod]
    public void ApplyFilter_WithNewValues_UpdatesFilters()
    {
        var newStartDateTime = new DateTime(2024, 1, 1);
        var newEndDateTime = new DateTime(2024, 12, 31);
        var newColumnFilters = new List<PacketColumnFilter>
        {
            new() { ColumnName = nameof(PacketDto.Channel), Operator = FilterOperator.String.Contains, Value = "Value1" },
            new() { ColumnName = nameof(PacketDto.Id), Operator = FilterOperator.Number.GreaterThan, Value = "10" },
        };

        _connectorScopedContext.ApplyFilter(newStartDateTime, newEndDateTime, newColumnFilters);

        _connectorScopedContext.FilterOptions.StartDateTime.Should().Be(newStartDateTime);
        _connectorScopedContext.FilterOptions.EndDateTime.Should().Be(newEndDateTime);
        _connectorScopedContext.FilterOptions.ColumnFilters.Should().BeEquivalentTo(newColumnFilters);
    }

    [TestMethod]
    public void ApplyFilter_WithNullValues_KeepsExistingFilters()
    {
        var originalStartDateTime = _connectorScopedContext.FilterOptions.StartDateTime;
        var originalEndDateTime = _connectorScopedContext.FilterOptions.EndDateTime;
        var originalColumnFilters = _connectorScopedContext.FilterOptions.ColumnFilters;

        _connectorScopedContext.ApplyFilter();

        _connectorScopedContext.FilterOptions.StartDateTime.Should().Be(originalStartDateTime);
        _connectorScopedContext.FilterOptions.EndDateTime.Should().Be(originalEndDateTime);
        _connectorScopedContext.FilterOptions.ColumnFilters.Should().BeEquivalentTo(originalColumnFilters);
    }


    [TestMethod]
    public async Task ReloadPackets_WhenNewPacketsFetched_AddsPackets()
    {
        var now = DateTime.Now;
        var packet1 = new PacketDto { Id = 1, ConnectorName = _connectorIdentifier.ConnectorKey, Channel = "A", DateCreated = now, Data = "Data1", ParentId = null, Status = PacketStatus.Enqueued };
        var packet2 = new PacketDto { Id = 2, ConnectorName = _connectorIdentifier.ConnectorKey, Channel = "B", DateCreated = now, Data = "Data2", ParentId = null, Status = PacketStatus.Enqueued };
        var packets = new List<PacketDto> { packet1, packet2 };
        var wrapper = new PacketWrapperDto()
        {
            ConnectorIdentifier = _connectorIdentifier,
            Packets = [.. packets]
        };

        await _messenger.AnswerAsync<ConnectorPacketsFilterDto, PacketWrapperDto>(
            ConnectorContract.UIPacketsGetTopic(_connectorIdentifier),
            _ => wrapper);

        var raisedEvents = new List<(ConnectorIdentifier Identifier, ConnectorPacketsChangedDto Changed)>();
        _connectorScopedContext.OnConnectorPacketsChanged += (x, y) => raisedEvents.Add((x, y));

        await _connectorScopedContext.ReloadPackets();

        // Packets contains both packets.
        _connectorScopedContext.Packets.Should().HaveCount(2);
        _connectorScopedContext.Packets.Should().Contain([packet1, packet2]);

        // Each packet is placed in the correct group
        var groupKey1 = new PacketGroupIdentifier(packet1.ConnectorName, packet1.Channel);
        var groupKey2 = new PacketGroupIdentifier(packet2.ConnectorName, packet2.Channel);
        _connectorScopedContext.GroupedPackets.Should().ContainKey(groupKey1);
        _connectorScopedContext.GroupedPackets.Should().ContainKey(groupKey2);
        _connectorScopedContext.GroupedPackets[groupKey1].Should().Contain(packet1);
        _connectorScopedContext.GroupedPackets[groupKey2].Should().Contain(packet2);

        raisedEvents.Should().ContainSingle();
        raisedEvents.First().Identifier.Should().Be(_connectorIdentifier);
        raisedEvents.First().Changed.Should().Be(ConnectorPacketsChangedDto.Any);
    }

    [TestMethod]
    public async Task ReloadPackets_WhenPacketsChanged_UpdatesPackets()
    {
        var now = DateTime.Now;
        var packet1 = new PacketDto { Id = 1, ConnectorName = _connectorIdentifier.ConnectorKey, Channel = "A", DateCreated = now, Data = "Data1", ParentId = null, Status = PacketStatus.Enqueued };
        var packet2 = new PacketDto { Id = 2, ConnectorName = _connectorIdentifier.ConnectorKey, Channel = "B", DateCreated = now, Data = "Data2", ParentId = null, Status = PacketStatus.Enqueued };
        var packet3 = new PacketDto { Id = 3, ConnectorName = _connectorIdentifier.ConnectorKey, Channel = "C", DateCreated = now, Data = "Data3", ParentId = null, Status = PacketStatus.Enqueued };
        var packet4 = new PacketDto { Id = 4, ConnectorName = _connectorIdentifier.ConnectorKey, Channel = "D", DateCreated = now, Data = "Data4", ParentId = null, Status = PacketStatus.Enqueued };
        var packet5 = new PacketDto { Id = 5, ConnectorName = _connectorIdentifier.ConnectorKey, Channel = "E", DateCreated = now, Data = "Data5", ParentId = null, Status = PacketStatus.Enqueued };

        var initialPackets = new List<PacketDto> { packet1, packet2, packet3, packet4, packet5 };
        var initialWrapper = new PacketWrapperDto
        {
            ConnectorIdentifier = _connectorIdentifier,
            Packets = [.. initialPackets]
        };

        var regToken = await _messenger.AnswerAsync<ConnectorPacketsFilterDto, PacketWrapperDto>(
            ConnectorContract.UIPacketsGetTopic(_connectorIdentifier),
            _ => initialWrapper);

        // Trigger initial load
        await _connectorScopedContext.ReloadPackets();

        // 5 packets loaded in their original groups
        _connectorScopedContext.Packets.Should().HaveCount(5);
        _connectorScopedContext.GroupedPackets.Should().ContainKey(new PacketGroupIdentifier(packet2.ConnectorName, "B"));
        _connectorScopedContext.GroupedPackets.Should().ContainKey(new PacketGroupIdentifier(packet4.ConnectorName, "D"));

        var updatedPacket2 = packet2 with
        {
            DateCreated = now.AddSeconds(1),
            Data = "Data2 Updated",
        };
        var updatedPacket4 = packet4 with
        {
            DateCreated = now.AddSeconds(1),
            Data = "Data4 Updated",
        };

        var updatedPackets = new List<PacketDto> { packet1, updatedPacket2, packet3, updatedPacket4, packet5 };
        var updatedWrapper = new PacketWrapperDto
        {
            ConnectorIdentifier = _connectorIdentifier,
            Packets = [.. updatedPackets]
        };

        await regToken.DisposeAsync();

        await _messenger.AnswerAsync<ConnectorPacketsFilterDto, PacketWrapperDto>(
                ConnectorContract.UIPacketsGetTopic(_connectorIdentifier),
                _ => updatedWrapper);

        // Trigger update load
        await _connectorScopedContext.ReloadPackets();

        // All 5 packets should be loaded
        // Packet2 and packet4 should be updated
        _connectorScopedContext.Packets.Should().HaveCount(5);
        _connectorScopedContext.Packets.Single(p => p.Id == packet2.Id).Should().Be(updatedPacket2);
        _connectorScopedContext.Packets.Single(p => p.Id == packet4.Id).Should().Be(updatedPacket4);

        var groupKey2 = new PacketGroupIdentifier(packet2.ConnectorName, packet2.Channel);
        var groupKey4 = new PacketGroupIdentifier(packet4.ConnectorName, packet4.Channel);
        _connectorScopedContext.GroupedPackets[groupKey2].Single(p => p.Id == packet2.Id).Should().Be(updatedPacket2);
        _connectorScopedContext.GroupedPackets[groupKey4].Single(p => p.Id == packet4.Id).Should().Be(updatedPacket4);
    }

    [TestMethod]
    public async Task ReloadPackets_WhenPacketsChangedChannel_UpdatesPacketGroups()
    {
        var now = DateTime.Now;
        var packet1 = new PacketDto { Id = 1, ConnectorName = _connectorIdentifier.ConnectorKey, Channel = "A", DateCreated = now, Data = "Data1", ParentId = null, Status = PacketStatus.Enqueued };
        var packet2 = new PacketDto { Id = 2, ConnectorName = _connectorIdentifier.ConnectorKey, Channel = "B", DateCreated = now, Data = "Data2", ParentId = null, Status = PacketStatus.Enqueued };
        var packet3 = new PacketDto { Id = 3, ConnectorName = _connectorIdentifier.ConnectorKey, Channel = "C", DateCreated = now, Data = "Data3", ParentId = null, Status = PacketStatus.Enqueued };
        var packet4 = new PacketDto { Id = 4, ConnectorName = _connectorIdentifier.ConnectorKey, Channel = "D", DateCreated = now, Data = "Data4", ParentId = null, Status = PacketStatus.Enqueued };
        var packet5 = new PacketDto { Id = 5, ConnectorName = _connectorIdentifier.ConnectorKey, Channel = "E", DateCreated = now, Data = "Data5", ParentId = null, Status = PacketStatus.Enqueued };

        var initialPackets = new List<PacketDto> { packet1, packet2, packet3, packet4, packet5 };
        var initialWrapper = new PacketWrapperDto
        {
            ConnectorIdentifier = _connectorIdentifier,
            Packets = [.. initialPackets]
        };

        var regToken = await _messenger.AnswerAsync<ConnectorPacketsFilterDto, PacketWrapperDto>(
            ConnectorContract.UIPacketsGetTopic(_connectorIdentifier),
            _ => initialWrapper);

        // Trigger initial load
        await _connectorScopedContext.ReloadPackets();

        // 5 packets loaded in their original groups
        _connectorScopedContext.Packets.Should().HaveCount(5);
        _connectorScopedContext.GroupedPackets.Should().ContainKey(new PacketGroupIdentifier(packet2.ConnectorName, "B"));
        _connectorScopedContext.GroupedPackets.Should().ContainKey(new PacketGroupIdentifier(packet4.ConnectorName, "D"));

        var updatedPacket2 = packet2 with
        {
            Channel = "UpdatedB",
            DateCreated = now.AddSeconds(1),
            Data = "Data2 Updated",
        };
        var updatedPacket4 = packet4 with
        {
            Channel = "UpdatedD",
            DateCreated = now.AddSeconds(1),
            Data = "Data4 Updated",
        };

        var updatedPackets = new List<PacketDto> { packet1, updatedPacket2, packet3, updatedPacket4, packet5 };
        var updatedWrapper = new PacketWrapperDto
        {
            ConnectorIdentifier = _connectorIdentifier,
            Packets = [.. updatedPackets]
        };

        await regToken.DisposeAsync();

        await _messenger.AnswerAsync<ConnectorPacketsFilterDto, PacketWrapperDto>(
            ConnectorContract.UIPacketsGetTopic(_connectorIdentifier),
            _ => updatedWrapper);

        // Trigger update load
        await _connectorScopedContext.ReloadPackets();

        // All 5 packets should be loaded
        // packet2 and packet4 should be updated
        _connectorScopedContext.Packets.Should().HaveCount(5);
        _connectorScopedContext.Packets.Should().Contain(p => p.Id == packet2.Id && p.Channel == "UpdatedB");
        _connectorScopedContext.Packets.Should().Contain(p => p.Id == packet4.Id && p.Channel == "UpdatedD");

        // Verify that the new groups exist and the old groups for packet2 and packet4 have been removed
        _connectorScopedContext.GroupedPackets.Should().ContainKey(new PacketGroupIdentifier(packet2.ConnectorName, "UpdatedB"));
        _connectorScopedContext.GroupedPackets[new PacketGroupIdentifier(packet2.ConnectorName, "UpdatedB")].Should().Contain(p => p.Id == packet2.Id);
        _connectorScopedContext.GroupedPackets[new PacketGroupIdentifier(packet2.ConnectorName, "B")].Should().NotContain(p => p.Id == packet2.Id);

        _connectorScopedContext.GroupedPackets.Should().ContainKey(new PacketGroupIdentifier(packet4.ConnectorName, "UpdatedD"));
        _connectorScopedContext.GroupedPackets[new PacketGroupIdentifier(packet4.ConnectorName, "UpdatedD")].Should().Contain(p => p.Id == packet4.Id);
        _connectorScopedContext.GroupedPackets[new PacketGroupIdentifier(packet4.ConnectorName, "D")].Should().NotContain(p => p.Id == packet4.Id);

    }

    [TestMethod]
    public async Task ReloadPackets_WhenExistingPacketsNotFetched_DeletePackets()
    {
        var now = DateTime.Now;
        var packet1 = new PacketDto { Id = 1, ConnectorName = _connectorIdentifier.ConnectorKey, Channel = "A", DateCreated = now, Data = "Data1", ParentId = null, Status = PacketStatus.Enqueued };
        var packet2 = new PacketDto { Id = 2, ConnectorName = _connectorIdentifier.ConnectorKey, Channel = "B", DateCreated = now, Data = "Data2", ParentId = null, Status = PacketStatus.Enqueued };
        var packet3 = new PacketDto { Id = 3, ConnectorName = _connectorIdentifier.ConnectorKey, Channel = "C", DateCreated = now, Data = "Data3", ParentId = null, Status = PacketStatus.Enqueued };
        var packet4 = new PacketDto { Id = 4, ConnectorName = _connectorIdentifier.ConnectorKey, Channel = "D", DateCreated = now, Data = "Data4", ParentId = null, Status = PacketStatus.Enqueued };
        var packet5 = new PacketDto { Id = 5, ConnectorName = _connectorIdentifier.ConnectorKey, Channel = "E", DateCreated = now, Data = "Data5", ParentId = null, Status = PacketStatus.Enqueued };

        var initialPackets = new List<PacketDto> { packet1, packet2, packet3, packet4, packet5 };
        var initialWrapper = new PacketWrapperDto
        {
            ConnectorIdentifier = _connectorIdentifier,
            Packets = [.. initialPackets]
        };

        var regToken = await _messenger.AnswerAsync<ConnectorPacketsFilterDto, PacketWrapperDto>(
            ConnectorContract.UIPacketsGetTopic(_connectorIdentifier),
            _ => initialWrapper);

        // Trigger initial load
        await _connectorScopedContext.ReloadPackets();

        // 5 packets loaded
        _connectorScopedContext.Packets.Should().HaveCount(5);

        var updatedPackets = new List<PacketDto> { packet1, packet3, packet5 };
        var updatedWrapper = new PacketWrapperDto
        {
            ConnectorIdentifier = _connectorIdentifier,
            Packets = [.. updatedPackets]
        };

        await regToken.DisposeAsync();

        await _messenger.AnswerAsync<ConnectorPacketsFilterDto, PacketWrapperDto>(
            ConnectorContract.UIPacketsGetTopic(_connectorIdentifier),
            _ => updatedWrapper);

        await _connectorScopedContext.ReloadPackets();

        // Only 3 packets should remain
        _connectorScopedContext.Packets.Should().HaveCount(3);
        _connectorScopedContext.Packets.Should().Contain(p => p.Id == packet1.Id);
        _connectorScopedContext.Packets.Should().Contain(p => p.Id == packet3.Id);
        _connectorScopedContext.Packets.Should().Contain(p => p.Id == packet5.Id);

        // Groups for the deleted packets should no longer include them
        var groupB = new PacketGroupIdentifier(packet2.ConnectorName, packet2.Channel);
        var groupD = new PacketGroupIdentifier(packet4.ConnectorName, packet4.Channel);
        if (_connectorScopedContext.GroupedPackets.ContainsKey(groupB))
        {
            _connectorScopedContext.GroupedPackets[groupB].Should().NotContain(p => p.Id == packet2.Id);
        }
        if (_connectorScopedContext.GroupedPackets.ContainsKey(groupD))
        {
            _connectorScopedContext.GroupedPackets[groupD].Should().NotContain(p => p.Id == packet4.Id);
        }
    }

    [TestMethod]
    public async Task ReloadPackets_WithFilterHint_CombinesOldAndNewPackets()
    {
        var now = DateTime.Now;
        _connectorScopedContext.ApplyFilter(now.AddMinutes(-10), now.AddMinutes(5));

        var packet1 = new PacketDto { Id = 1, ConnectorName = _connectorIdentifier.ConnectorKey, Channel = "A", DateCreated = now, Data = "Data1", ParentId = null, Status = PacketStatus.Enqueued };
        var packet2 = new PacketDto { Id = 2, ConnectorName = _connectorIdentifier.ConnectorKey, Channel = "B", DateCreated = now, Data = "Data2", ParentId = null, Status = PacketStatus.Enqueued };

        var initialWrapper = new PacketWrapperDto { ConnectorIdentifier = _connectorIdentifier, Packets = [packet1, packet2] };

        var regToken = await _messenger.AnswerAsync<ConnectorPacketsFilterDto, PacketWrapperDto>(
            ConnectorContract.UIPacketsGetTopic(_connectorIdentifier),
            _ => initialWrapper);

        await _connectorScopedContext.ReloadPackets();
        await regToken.DisposeAsync();

        var packet3 = new PacketDto { Id = 3, ConnectorName = _connectorIdentifier.ConnectorKey, Channel = "B", DateCreated = now.AddMinutes(2), Data = "Data3", ParentId = null, Status = PacketStatus.Enqueued };
        var packet4 = new PacketDto { Id = 4, ConnectorName = _connectorIdentifier.ConnectorKey, Channel = "C", DateCreated = now.AddMinutes(4), Data = "Data4", ParentId = null, Status = PacketStatus.Enqueued };

        var newWrapper = new PacketWrapperDto { ConnectorIdentifier = _connectorIdentifier, Packets = [packet3, packet4] };

        var filterHint = new ConnectorPacketsFilterDto(
            DateTimeStart: now.AddMinutes(1),
            DateTimeEnd: now.AddMinutes(10));

        var filterDto = new ConnectorPacketsFilterDto(
            DateTimeStart: _connectorScopedContext.FilterOptions.StartDateTime,
            DateTimeEnd: _connectorScopedContext.FilterOptions.EndDateTime);

        var expectedFilter = filterHint.Intersect(filterDto);

        ConnectorPacketsFilterDto? receivedFilter = null;
        regToken = await _messenger.AnswerAsync<ConnectorPacketsFilterDto, PacketWrapperDto>(
            ConnectorContract.UIPacketsGetTopic(_connectorIdentifier),
            (x) =>
            {
                receivedFilter = x;
                return newWrapper;
            });

        var tcs = new TaskCompletionSource();
        _connectorScopedContext.OnConnectorPacketsChanged += (_, _) => tcs.TrySetResult();

        _connectorContext.OnConnectorPacketsChanged += Raise.Event<OnConnectorPacketsChangedDelegate>(
            _connectorIdentifier, new ConnectorPacketsChangedDto(filterHint));

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await regToken.DisposeAsync();

        receivedFilter.Should().NotBeNull();
        receivedFilter.Should().Be(expectedFilter);

        _connectorScopedContext.Packets.Should().HaveCount(4);
        _connectorScopedContext.Packets.Should().Contain(p => p.Id == packet1.Id);
        _connectorScopedContext.Packets.Should().Contain(p => p.Id == packet2.Id);
        _connectorScopedContext.Packets.Should().Contain(p => p.Id == packet3.Id);
        _connectorScopedContext.Packets.Should().Contain(p => p.Id == packet4.Id);

        _connectorScopedContext.GroupedPackets.Should().HaveCount(3);
        var groupA = new PacketGroupIdentifier(packet2.ConnectorName, packet1.Channel);
        var groupB = new PacketGroupIdentifier(packet2.ConnectorName, packet2.Channel);
        var groupC = new PacketGroupIdentifier(packet4.ConnectorName, packet4.Channel);
        _connectorScopedContext.GroupedPackets.Should().ContainKey(groupA);
        _connectorScopedContext.GroupedPackets.Should().ContainKey(groupB);
        _connectorScopedContext.GroupedPackets.Should().ContainKey(groupC);

        _connectorScopedContext.GroupedPackets[groupA].Should().ContainSingle(p => p.Id == packet1.Id);
        _connectorScopedContext.GroupedPackets[groupB].Should()
            .OnlyContain(p => p.Id == packet2.Id || p.Id == packet3.Id);
        _connectorScopedContext.GroupedPackets[groupC].Should().ContainSingle(p => p.Id == packet4.Id);
    }

    [TestMethod]
    public async Task StartFilterRunner_RegistersListenersAndSendsCorrectRequest()
    {
        var eventTriggered = false;

        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 12, 31);
        var columnFilters = new List<PacketColumnFilter>
    {
        new() { ColumnName = nameof(PacketDto.Channel), Operator = FilterOperator.String.Contains, Value = "Value1" },
        new() { ColumnName = nameof(PacketDto.Id), Operator = FilterOperator.Number.GreaterThan, Value = "10" }
    };
        _connectorScopedContext.ApplyFilter(start, end, columnFilters);

        FilterRunnerRequest? receivedRequest = null;
        await _messenger.ListenAsync<FilterRunnerRequest>(
            ConnectorContract.StartFilterRunnerTopic(_connectorIdentifier),
            x => receivedRequest = x);

        _connectorScopedContext.OnConnectorPacketsChanged += (_, _) => eventTriggered = true;

        await _connectorScopedContext.StartFilterRunner();

        _connectorScopedContext.IsFilterRunning.Should().BeTrue();
        receivedRequest.Should().NotBeNull();
        receivedRequest.PacketRequestDto.DateTimeStart.Should().Be(start);
        receivedRequest.PacketRequestDto.DateTimeEnd.Should().Be(end);
        receivedRequest.PacketRequestDto.ColumnFilters.Should().BeEquivalentTo(columnFilters.Select(x => x.ToColumnFilterDto()));

        var responses = await _messenger.AskAsync<PacketWrapperDto, bool>(
            ConnectorContract.PushFilteredPacketsTopic(receivedRequest.FilterRunnerIdentifier),
            new PacketWrapperDto() { ConnectorIdentifier = _connectorIdentifier, Packets = [] });

        responses.Should().ContainSingle();
        responses.First().Should().BeTrue();
        eventTriggered.Should().BeTrue();

        eventTriggered = false;
        await _messenger.SendAsync(
            ConnectorContract.FilterRunnerStoppedNotification(receivedRequest.FilterRunnerIdentifier),
            _connectorIdentifier);

        eventTriggered.Should().BeTrue();
        _connectorScopedContext.IsFilterRunning.Should().BeFalse();
    }

    [TestMethod]
    public async Task StopFilterRunner_WhenCalled_SendsFilterRunnerStopRequest()
    {
        string? receivedFilterRunnerIdentifier = null;
        await _messenger.ListenAsync<string>(
            ConnectorContract.StopFilterRunnerTopic(_connectorIdentifier),
            x => receivedFilterRunnerIdentifier = x);

        await _connectorScopedContext.StopFilterRunner();

        receivedFilterRunnerIdentifier.Should().NotBeNull();
    }

    [TestMethod]
    public async Task ResendPacketsAsync_WhenCalled_CallsConnectorContext()
    {
        var packets = new HashSet<PacketDto>
        {
            new() { Id = 1, Channel = "Test", DateCreated = DateTime.Now, Data = "data1", ParentId = null, Status = PacketStatus.Enqueued },
            new() { Id = 2, Channel = "Test", DateCreated = DateTime.Now, Data = "data2", ParentId = null, Status = PacketStatus.Enqueued }
        };

        await _connectorScopedContext.ResendPacketsAsync(packets);
        await _connectorContext.Received(1).ResendPacketsAsync(packets);
    }

    [TestMethod]
    public async Task StopPacketsAsync_WhenCalled_CallsConnectorContext()
    {
        var packets = new HashSet<PacketDto>
        {
            new() { Id = 1, Channel = "Test", DateCreated = DateTime.Now, Data = "data1", ParentId = null, Status = PacketStatus.Enqueued },
            new() { Id = 2, Channel = "Test", DateCreated = DateTime.Now, Data = "data2", ParentId = null, Status = PacketStatus.Enqueued }
        };

        await _connectorScopedContext.StopPacketsAsync(packets);
        await _connectorContext.Received(1).StopPacketsAsync(packets);
    }

    [TestMethod]
    public async Task DeletePacketsAsync_WhenCalled_CallsConnectorContext()
    {
        var packets = new HashSet<PacketDto>
        {
            new() { Id = 1, Channel = "Test", DateCreated = DateTime.Now, Data = "data1", ParentId = null, Status = PacketStatus.Enqueued },
            new() { Id = 2, Channel = "Test", DateCreated = DateTime.Now, Data = "data2", ParentId = null, Status = PacketStatus.Enqueued }
        };

        await _connectorScopedContext.DeletePacketsAsync(packets);
        await _connectorContext.Received(1).DeletePacketsAsync(packets);
    }

    [TestMethod]
    public async Task ImportPacketsAsync_WhenCalled_CallsConnectorContext()
    {
        var importData = new ConnectorPacketsExportDto([]);
        _connectorContext.ImportPacketsAsync(importData, true).Returns(true);

        var result = await _connectorScopedContext.ImportPacketsAsync(importData, true);

        result.Should().BeTrue();
        await _connectorContext.Received(1).ImportPacketsAsync(importData, true);
    }

    [TestMethod]
    public async Task RunAsync_WhenCalled_CallsConnectorContext()
    {
        var cancellationToken = CancellationToken.None;
        await _connectorScopedContext.RunAsync(cancellationToken);
        await _connectorContext.Received(1).RunAsync(cancellationToken);
    }

    [TestCleanup]
    public async Task TestCleanup()
    {
        await _connectorScopedContext.DisposeAsync();
    }
}

