using eHub.Contracts.UIConfig;
using eHub.Database.Models;
using eHub.PlugIn;
using Riok.Mapperly.Abstractions;

namespace eHub.Database;

[Mapper]
public static partial class Mappings
{
    [MapperIgnoreSource(nameof(Packet.BinaryData))]
    [MapperIgnoreTarget(nameof(PacketDto.Data))]
    [MapperIgnoreTarget(nameof(PacketDto.PreviewData))]
    [MapperIgnoreTarget(nameof(Packet.BinaryData))]
    [MapperIgnoreTarget(nameof(PacketDto.DataType))]
    [MapperIgnoreTarget(nameof(PacketDto.CanResend))]
    [MapperIgnoreTarget(nameof(PacketDto.ConnectorName))]
    public static partial PacketDto ToDto(this Packet packet);
    [MapperIgnoreTarget(nameof(Packet.BinaryData))]
    [MapperIgnoreSource(nameof(PacketDto.Data))]
    [MapperIgnoreSource(nameof(PacketDto.PreviewData))]
    [MapperIgnoreSource(nameof(PacketDto.BinaryData))]
    [MapperIgnoreSource(nameof(PacketDto.DataType))]
    [MapperIgnoreSource(nameof(PacketDto.CanResend))]
    [MapperIgnoreSource(nameof(PacketDto.ConnectorName))]
    public static partial Packet ToDbModel(this PacketDto packetDto);
    [MapperIgnoreTarget(nameof(Packet.BinaryData))]
    [MapperIgnoreSource(nameof(PacketDto.Data))]
    [MapperIgnoreSource(nameof(PacketDto.PreviewData))]
    [MapperIgnoreSource(nameof(PacketDto.DataType))]
    [MapperIgnoreSource(nameof(PacketDto.CanResend))]
    public static partial Packet[] ToDbModel(this IEnumerable<PacketDto> packetDto);
    [MapperIgnoreSource(nameof(PacketData.Id))]
    public static partial Packet[] ToDbModel(this IEnumerable<PacketData> packetData);
    public static partial PacketData ToData(this Packet packet);
    public static partial PacketData[] ToData(this Packet[] packet);
    public static partial IQueryable<PacketDto> ProjectToDto(this IQueryable<Packet> q);
}
