using System.Linq.Expressions;
using eHub.Database;
using eHub.Database.Models;
using eHub.PlugIn;
using eHub.Scripting.Connectors.Features;

namespace eHub.Scripting.Connectors.Db;

public class PacketQueryBuilder(PacketTransferFeature scriptCtx) : IPacketQueryBuilder
{
    private Expression<Func<PacketData, bool>>? _condition;

    public IPacketQueryBuilder Where(Expression<Func<PacketData, bool>> conditionExpression)
    {
        _condition = conditionExpression;
        return this;
    }

    public async ValueTask<PacketData[]> GetAsync(CancellationToken cancellationToken = default)
    {
        var condition = _condition != null ? ExpressionMagic.ConvertFilter<PacketData, Packet>(_condition) : null;
        var packets = await scriptCtx.GetPackets(condition, cancellationToken);
        return packets.ToData();
    }
}
