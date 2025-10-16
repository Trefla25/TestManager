using eHub.Database;
using eHub.PlugIn;
using eHub.Scripting.Connectors.Features;

namespace eHub.Scripting.Connectors.Db;

public class PacketCreateBuilder(PacketTransferFeature scriptCtx) : IPacketCreateBuilder
{
    private readonly List<PacketData> _packets = [];

    public IPacketCreateBuilder Add(PacketData packet)
    {
        _packets.Add(packet);
        return this;
    }

    public IPacketCreateBuilder AddRange(IEnumerable<PacketData> packets)
    {
        _packets.AddRange(packets);
        return this;
    }

    public async ValueTask<PacketData[]> CreateAsync(CancellationToken cancellationToken = default)
    {
        var packets = await scriptCtx.CreatePackets(_packets.ToDbModel(), cancellationToken);
        _packets.Clear();
        return packets.ToData();
    }
}
