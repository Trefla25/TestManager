using System.ComponentModel;
using System.Linq.Expressions;

namespace eHub.PlugIn;

/// <summary></summary>
public interface IPacketRepository
{
    /// <summary></summary>
    IPacketCreateBuilder Create() => default!;

    /// <summary></summary>
    IPacketUpdateBuilder Update() => default!;

    /// <summary></summary>
    IPacketQueryBuilder Query() => default!;
}

/// <summary></summary>
public interface IPacketCreateBuilder
{
    /// <summary></summary>
    /// <param name="packet"></param>
    /// <returns></returns>
    IPacketCreateBuilder Add(PacketData packet);

    /// <summary></summary>
    /// <param name="packets"></param>
    /// <returns></returns>
    [Obsolete("Use AddRange(IEnumerable) instead.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    IPacketCreateBuilder AddRange(PacketData[] packets) => AddRange(packets.AsEnumerable());

    /// <summary></summary>
    /// <param name="packets"></param>
    /// <returns></returns>
    IPacketCreateBuilder AddRange(params IEnumerable<PacketData> packets);

    /// <summary></summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    ValueTask<PacketData[]> CreateAsync(CancellationToken cancellationToken = default);
}

/// <summary></summary>
public interface IPacketUpdateBuilder
{
    /// <summary></summary>
    /// <typeparam name="TProp"></typeparam>
    /// <param name="property"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    IPacketUpdateBuilder Set<TProp>(Expression<Func<PacketData, TProp>> property, TProp value);

    /// <summary></summary>
    /// <param name="conditionExpression"></param>
    /// <returns></returns>
    IPacketUpdateBuilder Where(Expression<Func<PacketData, bool>> conditionExpression);

    /// <summary></summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    ValueTask<int> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary></summary>
public interface IPacketQueryBuilder
{
    /// <summary></summary>
    /// <param name="conditionExpression"></param>
    /// <returns></returns>
    IPacketQueryBuilder Where(Expression<Func<PacketData, bool>> conditionExpression);

    /// <summary></summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    ValueTask<PacketData[]> GetAsync(CancellationToken cancellationToken = default);
}

/// <summary></summary>
public static class PacketRepositoryExtensions
{
    public static async ValueTask<PacketData?> FirstOrDefaultPacket(this ValueTask<PacketData[]> awaitablePacketResponse)
    {
        var packets = await awaitablePacketResponse;
        return packets.FirstOrDefault();
    }

    public static async ValueTask<PacketData> FirstPacket(this ValueTask<PacketData[]> awaitablePacketResponse)
    {
        var packets = await awaitablePacketResponse;

        if (!packets.Any())
        {
            throw new Exception("No packet responses.");
        }

        return packets.First();
    }

    /// <summary>Convenience method for adding a single packet to the history.</summary>
    /// <param name="packetRepository">Repository to work on</param>
    /// <param name="packet">The packet to add</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The newly added packet with id.</returns>
    public static ValueTask<PacketData> AddAsync(this IPacketRepository packetRepository, PacketData packet, CancellationToken cancellationToken = default)
        => packetRepository.Create().Add(packet).CreateAsync(cancellationToken).FirstPacket();

    /// <summary>Convenience method for adding multiple packets to the history.</summary>
    /// <param name="packetRepository">Repository to work on</param>
    /// <param name="packets">The packets to add</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The newly added packets with ids.</returns>
    [Obsolete("Use AddRangeAsync(IEnumerable) instead.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ValueTask<PacketData[]> AddRangeAsync(this IPacketRepository packetRepository, PacketData[] packets, CancellationToken cancellationToken = default)
        => AddRangeAsync(packetRepository, packets.AsEnumerable(), cancellationToken);

    /// <summary>Convenience method for adding multiple packets to the history.</summary>
    /// <param name="packetRepository">Repository to work on</param>
    /// <param name="packets">The packets to add</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The newly added packets with ids.</returns>
    public static ValueTask<PacketData[]> AddRangeAsync(this IPacketRepository packetRepository, IEnumerable<PacketData> packets, CancellationToken cancellationToken = default)
        => packetRepository.Create().AddRange(packets).CreateAsync(cancellationToken);

    /// <summary>Convenience method for updating the status of a single packet to the history.</summary>
    /// <param name="packetRepository">Repository to work on</param>
    /// <param name="packetId">The packet to update</param>
    /// <param name="status">The new packet status</param>
    /// <param name="cancellationToken"></param>
    public static async Task UpdatePacketStatusAsync(this IPacketRepository packetRepository, long packetId, PacketStatus status, CancellationToken cancellationToken = default)
        => await packetRepository.Update().Set(p => p.Status, status).Where(p => p.Id == packetId).ExecuteAsync(cancellationToken);

    /// <summary>Convenience method for getting a packet by its Id.</summary>
    /// <param name="packetRepository">Repository to work on</param>
    /// <param name="packetId">The packet Id to get</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The packet corresponding to the provided Id.</returns>
    public static async ValueTask<PacketData> GetPacketByIdAsync(this IPacketRepository packetRepository, long packetId, CancellationToken cancellationToken = default)
        => await packetRepository.Query().Where(p => p.Id == packetId).GetAsync(cancellationToken).FirstPacket();
}
