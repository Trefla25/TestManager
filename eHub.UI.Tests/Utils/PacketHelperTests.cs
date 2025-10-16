using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eHub.PlugIn;
using eHub.UI.Util;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace eHub.UI.Tests.Utils;

[TestClass]
public class PacketHelperTests
{
    private Dictionary<long, PacketDto> _packetDictionary = default!;
    private Dictionary<PacketGroupIdentifier, HashSet<PacketDto>> _groupDictionary = default!;
    private List<PacketDto> _incomingPackets = default!;
    private ConnectorIdentifier _connectorIdentifier = default!;

    [TestInitialize]
    public void TestInitialize()
    {
        _packetDictionary = [];
        _groupDictionary = [];
        _connectorIdentifier = new ConnectorIdentifier("instance", "TestConnector");

        _incomingPackets =
        [
            new() { Id = 1, Channel = "Channel1", Status = PacketStatus.Enqueued, DateCreated = DateTime.Now, ParentId = null },
            new() { Id = 2, Channel = "Channel1", Status = PacketStatus.Enqueued, DateCreated = DateTime.Now, ParentId = null },
            new() { Id = 3, Channel = "Channel2", Status = PacketStatus.Processed, DateCreated = DateTime.Now, ParentId = null }
        ];
    }

    [TestMethod]
    public void Statuses_ContainsAllPacketStatuses()
    {
        var statuses = PacketHelper.Statuses;

        var expectedStatuses = Enum.GetValues<PacketStatus>();

        statuses.Should().HaveCount(6);
        foreach (var status in expectedStatuses)
        {
            statuses.Should().Contain(status);
        }
    }

    [TestMethod]
    public void AggregatePacketCollections_WhenNewPacketsAdded_ReturnsTrueAndUpdatesDictionaries()
    {
        var result = PacketHelper.AggregatePacketCollections(
            incomingPackets: _incomingPackets,
            packetDictionary: _packetDictionary,
            groupDictionary: _groupDictionary,
            keySelector: packet => packet.Id,
            groupSelector: packet => new PacketGroupIdentifier(_connectorIdentifier.ConnectorKey, packet.Channel));

        result.Should().BeTrue();

        // Ensure dictionary contains all packets
        _packetDictionary.Should().HaveCount(3);
        _groupDictionary.Should().HaveCount(2);

        foreach (var packet in _incomingPackets)
        {
            var group = new PacketGroupIdentifier(_connectorIdentifier.ConnectorKey, packet.Channel);
            _packetDictionary.Should().ContainKey(packet.Id);
            _groupDictionary[group].Should().Contain(packet);
        }
    }

    [TestMethod]
    public void AggregatePacketCollections_WhenExistingPacketUnchanged_ReturnsFalse()
    {
        // Prepopulate dictionaries
        foreach (var packet in _incomingPackets)
        {
            _packetDictionary[packet.Id] = packet;
            var group = new PacketGroupIdentifier(_connectorIdentifier.ConnectorKey, packet.Channel);
            _groupDictionary.GetOrAdd(group, () => []).Add(packet);
        }

        var result = PacketHelper.AggregatePacketCollections(
            incomingPackets: _incomingPackets,
            packetDictionary: _packetDictionary,
            groupDictionary: _groupDictionary,
            keySelector: packet => packet.Id,
            groupSelector: packet => new PacketGroupIdentifier(_connectorIdentifier.ConnectorKey, packet.Channel));

        result.Should().BeFalse();
    }

    [TestMethod]
    public void AggregatePacketCollections_WhenExistingPacketUpdated_ReturnsTrue()
    {
        // Prepopulate dictionary with different status
        var existingPacket = new PacketDto { Id = 1, Channel = "Channel1", Status = PacketStatus.FatalError, DateCreated = DateTime.Now, ParentId = null };
        _packetDictionary[1] = existingPacket;
        var group = new PacketGroupIdentifier(_connectorIdentifier.ConnectorKey, existingPacket.Channel);
        _groupDictionary.GetOrAdd(group, () => []).Add(existingPacket);

        var result = PacketHelper.AggregatePacketCollections(
            incomingPackets: _incomingPackets,
            packetDictionary: _packetDictionary,
            groupDictionary: _groupDictionary,
            keySelector: packet => packet.Id,
            groupSelector: packet => new PacketGroupIdentifier(_connectorIdentifier.ConnectorKey, packet.Channel));

        result.Should().BeTrue();

        // Ensure packet status was updated
        _packetDictionary[1].Status.Should().Be(PacketStatus.Enqueued);

        // Ensure the packet inside the group dictionary is also updated
        _groupDictionary[group].Should().NotContain(existingPacket); // The old packet should be removed
        _groupDictionary[group].Should().Contain(_packetDictionary[1]); // The new packet should be inside
    }

    [TestMethod]
    public void AggregatePacketCollections_WhenPacketMovesToNewChannel_UpdatesGroupDictionary()
    {
        var originalPacket = new PacketDto { Id = 1, Channel = "OldChannel", Status = PacketStatus.Enqueued, DateCreated = DateTime.Now, ParentId = null };
        _packetDictionary[1] = originalPacket;
        var oldGroup = new PacketGroupIdentifier(_connectorIdentifier.ConnectorKey, "OldChannel");
        _groupDictionary.GetOrAdd(oldGroup, () => []).Add(originalPacket);

        var updatedPacket = new PacketDto { Id = 1, Channel = "NewChannel", Status = PacketStatus.Enqueued, DateCreated = DateTime.Now, ParentId = null };

        var result = PacketHelper.AggregatePacketCollections(
            incomingPackets: [updatedPacket],
            packetDictionary: _packetDictionary,
            groupDictionary: _groupDictionary,
            keySelector: packet => packet.Id,
            groupSelector: packet => new PacketGroupIdentifier(_connectorIdentifier.ConnectorKey, packet.Channel));

        result.Should().BeTrue();

        // Ensure packet dictionary is updated
        _packetDictionary[1].Channel.Should().Be("NewChannel");

        // Ensure old group does not contain the packet anymore
        _groupDictionary[oldGroup].Should().NotContain(originalPacket);

        var newGroup = new PacketGroupIdentifier(_connectorIdentifier.ConnectorKey, "NewChannel");

        // Ensure new group contains the packet
        _groupDictionary[newGroup].Should().Contain(updatedPacket);
    }

    [TestMethod]
    public void AggregatePacketCollections_WhenPrunePacketsIsFalse_DoesNotRemoveOldPackets()
    {
        _packetDictionary[99] = new PacketDto {Id = 99, Channel = "OrphanChannel", Status = PacketStatus.Enqueued, DateCreated = DateTime.Now, ParentId = null };

        var result = PacketHelper.AggregatePacketCollections(
            incomingPackets: _incomingPackets,
            packetDictionary: _packetDictionary,
            groupDictionary: _groupDictionary,
            keySelector: packet => packet.Id,
            groupSelector: packet => new PacketGroupIdentifier(_connectorIdentifier.ConnectorKey, packet.Channel),
            prunePackets: false);

        result.Should().BeTrue();
        _packetDictionary.Should().ContainKey(99);
    }

    [TestMethod]
    public void AggregatePacketCollections_WhenPrunePacketsIsTrue_RemovesOldPackets()
    {
        var oldPacket = new PacketDto { Id = 99, Channel = "OrphanChannel", Status = PacketStatus.Enqueued, DateCreated = DateTime.Now, ParentId = null };
        _packetDictionary[99] = oldPacket;
        var orphanGroup = new PacketGroupIdentifier(_connectorIdentifier.ConnectorKey, "OrphanChannel");
        _groupDictionary.GetOrAdd(orphanGroup, () => []).Add(oldPacket);

        var result = PacketHelper.AggregatePacketCollections(
            incomingPackets: _incomingPackets,
            packetDictionary: _packetDictionary,
            groupDictionary: _groupDictionary,
            keySelector: packet => packet.Id,
            groupSelector: packet => new PacketGroupIdentifier(_connectorIdentifier.ConnectorKey, packet.Channel),
            prunePackets: true);

        result.Should().BeTrue();

        // Ensure old packet is removed from packet dictionary
        _packetDictionary.Should().NotContainKey(99);

        // Ensure old packet is removed from group dictionary
        _groupDictionary[orphanGroup].Should().NotContain(oldPacket);
    }

    [TestMethod]
    public void AggregatePacketCollections_WhenKeysToKeepSelectorIsUsed_KeepsSelectedPackets()
    {
        // Prepare initial data: existing packet that should be kept based on selector
        var packetToKeep = new PacketDto { Id = 99, Channel = "OrphanChannel", Status = PacketStatus.Enqueued, DateCreated = DateTime.Now, ParentId = null };
        var packetToPrune = new PacketDto { Id = 99, Channel = "OrphanChannel", Status = PacketStatus.Enqueued, DateCreated = DateTime.Now, ParentId = null };
        _packetDictionary[99] = packetToKeep;
        _packetDictionary[100] = packetToPrune;
        var orphanGroup = new PacketGroupIdentifier(_connectorIdentifier.ConnectorKey, "OrphanChannel");
        _groupDictionary.GetOrAdd(orphanGroup, () => []).AddRange([packetToKeep, packetToPrune]);

        // Prepare incoming packets: other packets that will replace or prune old ones
        var incomingPackets = new List<PacketDto>
        {
            new() { Id = 1, Channel = "Channel1", Status = PacketStatus.Enqueued, DateCreated = DateTime.Now, ParentId = null },
            new() { Id = 2, Channel = "Channel1", Status = PacketStatus.Enqueued, DateCreated = DateTime.Now, ParentId = null }
        };

        // Define the keysToKeepSelector: keep the packet with Id 99
        static bool keysToKeepSelector(long key) => key == 99;

        // Apply the method
        var result = PacketHelper.AggregatePacketCollections(
            incomingPackets: incomingPackets,
            packetDictionary: _packetDictionary,
            groupDictionary: _groupDictionary,
            keySelector: packet => packet.Id,
            groupSelector: packet => new PacketGroupIdentifier(_connectorIdentifier.ConnectorKey, packet.Channel),
            prunePackets: true,
            keysToKeepSelector: keysToKeepSelector);

        // Verify the result
        result.Should().BeTrue();

        // Ensure packet with Id 99 is not removed
        _packetDictionary.Should().ContainKey(99);
        _groupDictionary[orphanGroup].Should().Contain(packetToKeep);

        // Ensure other packets (1 and 2) are added to the dictionary and group
        _packetDictionary.Should().ContainKey(1);
        _packetDictionary.Should().ContainKey(2);

        // Ensure that packet with Id 100 was removed
        _packetDictionary.Should().NotContainKey(100);
        _groupDictionary[orphanGroup].Should().NotContain(packetToPrune);
    }

}

