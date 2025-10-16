using System.Collections.Frozen;
using System.Linq.Expressions;
using eHub.Database.Models;
using eHub.PlugIn;
using eHub.Scripting.Connectors.Features;

namespace eHub.Scripting.Connectors.Db;

public class PacketUpdateBuilder(PacketTransferFeature scriptCtx) : IPacketUpdateBuilder
{
    private readonly List<(Type PropType, LambdaExpression Access, Expression Set)> _updateActions = [];
    private Expression<Func<PacketData, bool>>? _condition;
    private static readonly FrozenSet<string> DisallowedProperties = [
        nameof(PacketData.BinaryData),
        nameof(PacketData.DateCreated),
        nameof(PacketData.DateChanged)
    ];

    public IPacketUpdateBuilder Set<TProp>(Expression<Func<PacketData, TProp>> property, TProp value)
    {
        _updateActions.Add((typeof(TProp), property, Expression.Constant(value)));
        return this;
    }

    public IPacketUpdateBuilder Where(Expression<Func<PacketData, bool>> conditionExpression)
    {
        _condition = conditionExpression;
        return this;
    }

    public async ValueTask<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var invalidUpdates = _updateActions
            .Select(x => (x.Access.Body as MemberExpression)?.Member.Name)
            .Where(x => x != null && DisallowedProperties.Contains(x));
        if (invalidUpdates.Any())
        {
            var invalidProperties = string.Join(" or ", invalidUpdates.Select(x => $"'{x}'"));
            throw new InvalidOperationException($"Failed to update the database. Updating {invalidProperties} is strictly prohibited to maintain message integrity. Create a new packet instead!");
        }

        var updateActions = _updateActions.Select(x => (
            x.PropType,
            ExpressionMagic.ConvertNode(x.Access, typeof(PacketData), typeof(Packet), []),
            ExpressionMagic.ConvertNode(x.Set, typeof(PacketData), typeof(Packet), [])
        ));
        var condition = _condition != null ? ExpressionMagic.ConvertFilter<PacketData, Packet>(_condition) : null;

        return await scriptCtx.UpdatePackets(updateActions, condition, cancellationToken);
    }
}
