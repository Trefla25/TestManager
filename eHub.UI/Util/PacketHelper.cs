using System.Collections.Frozen;
using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eHub.PlugIn;

namespace eHub.UI.Util;
public static class PacketHelper
{
    public static readonly IReadOnlyCollection<PacketStatus> Statuses = Enum.GetValues<PacketStatus>().ToFrozenSet();

    public static bool AggregatePacketCollections<TKey>(
        IEnumerable<PacketDto> incomingPackets,
        IDictionary<TKey, PacketDto> packetDictionary,
        IDictionary<PacketGroupIdentifier, HashSet<PacketDto>> groupDictionary,
        Func<PacketDto, TKey> keySelector,
        Func<PacketDto, PacketGroupIdentifier> groupSelector,
        bool prunePackets = true,
        Func<TKey, bool>? keysToKeepSelector = null)
    {
        var changed = false;
        var newKeys = new HashSet<TKey>();

        foreach (var packet in incomingPackets)
        {
            var key = keySelector(packet);
            var newGroup = groupSelector(packet);

            newKeys.Add(key);

            var groupCollection = groupDictionary.GetOrAdd(newGroup, () => []);

            if (packetDictionary.TryGetValue(key, out var existingPacket))
            {
                // If the packets are equal, no update is needed.
                if (existingPacket.Equals(packet))
                {
                    continue;
                }

                // Always remove the old packet from the group before adding the updated one.
                var oldGroup = groupSelector(existingPacket);
                groupDictionary[oldGroup].Remove(existingPacket);
            }

            // The new packet is added
            packetDictionary[key] = packet;
            groupCollection.Add(packet);
            changed = true;
        }

        if (!prunePackets)
        {
            return changed;
        }

        // Determine which keys to remove.
        var keysToRemove = packetDictionary.Keys.Where(x => !keysToKeepSelector?.Invoke(x) ?? true).Except(newKeys).ToArray();

        foreach(var key in keysToRemove)
        {
            var packet = packetDictionary[key];
            packetDictionary.Remove(key);

            var group = groupSelector(packet);
            groupDictionary[group].Remove(packet);

            changed = true;
        }

        return changed;
    }
}
