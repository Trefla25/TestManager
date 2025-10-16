using eHub.PlugIn;
using eHub.Scripting.Connectors.Features;

namespace eHub.Scripting.Connectors.Db;

public class PacketRepository(PacketTransferFeature packetTransferCore) : IPacketRepository
{
    public IPacketCreateBuilder Create() => new PacketCreateBuilder(packetTransferCore);
    public IPacketQueryBuilder Query() => new PacketQueryBuilder(packetTransferCore);
    public IPacketUpdateBuilder Update() => new PacketUpdateBuilder(packetTransferCore);
}
