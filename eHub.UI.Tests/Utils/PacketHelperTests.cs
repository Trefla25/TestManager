using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eHub.PlugIn;
using eHub.UI.Util;
using FluentAssertions;

namespace eHub.UI.Tests.Utils;

public class PacketHelperTests
{
    private readonly ConnectorIdentifier _connectorIdentifier = new("instance", "TestConnector");
    private readonly Dictionary<long, PacketDto> _packetDictionary = [];
    private readonly Dictionary<PacketGroupIdentifier, HashSet<PacketDto>> _groupDictionary = [];
    private readonly List<PacketDto> _incomingPackets =
    [
        new() { Id = 1, Channel = "Channel1", Status = PacketStatus.Enqueued, DateCreated = DateTime.Now, ParentId = null },
        new() { Id = 2, Channel = "Channel1", Status = PacketStatus.Enqueued, DateCreated = DateTime.Now, ParentId = null },
        new() { Id = 3, Channel = "Channel2", Status = PacketStatus.Processed, DateCreated = DateTime.Now, ParentId = null }
    ];

    [Fact]
    public void Statuses_ContainsAllPacketStatuses()
    {
        // Arrange
        var statuses = PacketHelper.Statuses;
        var expectedStatuses = Enum.GetValues<PacketStatus>();
        
        // Assert
        statuses.Should().HaveCount(6);
        foreach (var status in expectedStatuses)
        {
            statuses.Should().Contain(status);
        }
    }

    [Fact]
    public void AggregatePacketCollections_WhenNewPacketsAdded_ReturnsTrueAndUpdatesDictionaries()
    {
        // Act
        var result = PacketHelper.AggregatePacketCollections(
            incomingPackets: _incomingPackets,
            packetDictionary: _packetDictionary,
            groupDictionary: _groupDictionary,
            keySelector: packet => packet.Id,
            groupSelector: packet => new PacketGroupIdentifier(_connectorIdentifier.ConnectorKey, packet.Channel));
        
        // Assert
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

    [Fact]
    public void AggregatePacketCollections_WhenExistingPacketUnchanged_ReturnsFalse()
    {
        // Arrange
        // Prepopulate dictionaries
        foreach (var packet in _incomingPackets)
        {
            _packetDictionary[packet.Id] = packet;
            var group = new PacketGroupIdentifier(_connectorIdentifier.ConnectorKey, packet.Channel);
            _groupDictionary.GetOrAdd(group, () => []).Add(packet);
        }
        
        // Act
        var result = PacketHelper.AggregatePacketCollections(
            incomingPackets: _incomingPackets,
            packetDictionary: _packetDictionary,
            groupDictionary: _groupDictionary,
            keySelector: packet => packet.Id,
            groupSelector: packet => new PacketGroupIdentifier(_connectorIdentifier.ConnectorKey, packet.Channel));
        
        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void AggregatePacketCollections_WhenExistingPacketUpdated_ReturnsTrue()
    {
        // Arrange
        // Prepopulate dictionary with different status
        var existingPacket = new PacketDto { Id = 1, Channel = "Channel1", Status = PacketStatus.FatalError, DateCreated = DateTime.Now, ParentId = null };
        _packetDictionary[1] = existingPacket;
        var group = new PacketGroupIdentifier(_connectorIdentifier.ConnectorKey, existingPacket.Channel);
        _groupDictionary.GetOrAdd(group, () => []).Add(existingPacket);
       
        // Act
        var result = PacketHelper.AggregatePacketCollections(
            incomingPackets: _incomingPackets,
            packetDictionary: _packetDictionary,
            groupDictionary: _groupDictionary,
            keySelector: packet => packet.Id,
            groupSelector: packet => new PacketGroupIdentifier(_connectorIdentifier.ConnectorKey, packet.Channel));
        
        // Assert
        result.Should().BeTrue();

        // Ensure packet status was updated
        _packetDictionary[1].Status.Should().Be(PacketStatus.Enqueued);

        // Ensure the packet inside the group dictionary is also updated
        _groupDictionary[group].Should().NotContain(existingPacket); // The old packet should be removed
        _groupDictionary[group].Should().Contain(_packetDictionary[1]); // The new packet should be inside
    }

    [Fact]
    public void AggregatePacketCollections_WhenPacketMovesToNewChannel_UpdatesGroupDictionary()
    {
        // Arrange
        var originalPacket = new PacketDto { Id = 1, Channel = "OldChannel", Status = PacketStatus.Enqueued, DateCreated = DateTime.Now, ParentId = null };
        _packetDictionary[1] = originalPacket;
        var oldGroup = new PacketGroupIdentifier(_connectorIdentifier.ConnectorKey, "OldChannel");
        _groupDictionary.GetOrAdd(oldGroup, () => []).Add(originalPacket);

        var updatedPacket = new PacketDto { Id = 1, Channel = "NewChannel", Status = PacketStatus.Enqueued, DateCreated = DateTime.Now, ParentId = null };
        
        // Act
        var result = PacketHelper.AggregatePacketCollections(
            incomingPackets: [updatedPacket],
            packetDictionary: _packetDictionary,
            groupDictionary: _groupDictionary,
            keySelector: packet => packet.Id,
            groupSelector: packet => new PacketGroupIdentifier(_connectorIdentifier.ConnectorKey, packet.Channel));
        
        // Assert
        result.Should().BeTrue();

        // Ensure packet dictionary is updated
        _packetDictionary[1].Channel.Should().Be("NewChannel");

        // Ensure old group does not contain the packet anymore
        _groupDictionary[oldGroup].Should().NotContain(originalPacket);

        var newGroup = new PacketGroupIdentifier(_connectorIdentifier.ConnectorKey, "NewChannel");

        // Ensure new group contains the packet
        _groupDictionary[newGroup].Should().Contain(updatedPacket);
    }

    [Fact]
    public void AggregatePacketCollections_WhenPrunePacketsIsFalse_DoesNotRemoveOldPackets()
    {
        // Arrange
        _packetDictionary[99] = new PacketDto {Id = 99, Channel = "OrphanChannel", Status = PacketStatus.Enqueued, DateCreated = DateTime.Now, ParentId = null };
        
        // Act
        var result = PacketHelper.AggregatePacketCollections(
            incomingPackets: _incomingPackets,
            packetDictionary: _packetDictionary,
            groupDictionary: _groupDictionary,
            keySelector: packet => packet.Id,
            groupSelector: packet => new PacketGroupIdentifier(_connectorIdentifier.ConnectorKey, packet.Channel),
            prunePackets: false);
        
        // Assert
        result.Should().BeTrue();
        _packetDictionary.Should().ContainKey(99);
    }

    [Fact]
    public void AggregatePacketCollections_WhenPrunePacketsIsTrue_RemovesOldPackets()
    {
        // Arrange
        var oldPacket = new PacketDto { Id = 99, Channel = "OrphanChannel", Status = PacketStatus.Enqueued, DateCreated = DateTime.Now, ParentId = null };
        _packetDictionary[99] = oldPacket;
        var orphanGroup = new PacketGroupIdentifier(_connectorIdentifier.ConnectorKey, "OrphanChannel");
        _groupDictionary.GetOrAdd(orphanGroup, () => []).Add(oldPacket);
        
        // Act
        var result = PacketHelper.AggregatePacketCollections(
            incomingPackets: _incomingPackets,
            packetDictionary: _packetDictionary,
            groupDictionary: _groupDictionary,
            keySelector: packet => packet.Id,
            groupSelector: packet => new PacketGroupIdentifier(_connectorIdentifier.ConnectorKey, packet.Channel),
            prunePackets: true);
        
        // Assert
        result.Should().BeTrue();

        // Ensure old packet is removed from packet dictionary
        _packetDictionary.Should().NotContainKey(99);

        // Ensure old packet is removed from group dictionary
        _groupDictionary[orphanGroup].Should().NotContain(oldPacket);
    }

    [Fact]
    public void AggregatePacketCollections_WhenKeysToKeepSelectorIsUsed_KeepsSelectedPackets()
    {
        // Arrange
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
        static bool KeysToKeepSelector(long key) => key == 99;
        
        // Act
        // Apply the method
        var result = PacketHelper.AggregatePacketCollections(
            incomingPackets: incomingPackets,
            packetDictionary: _packetDictionary,
            groupDictionary: _groupDictionary,
            keySelector: packet => packet.Id,
            groupSelector: packet => new PacketGroupIdentifier(_connectorIdentifier.ConnectorKey, packet.Channel),
            prunePackets: true,
            keysToKeepSelector: KeysToKeepSelector);
        
        // Assert
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