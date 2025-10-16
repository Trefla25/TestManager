using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json.Serialization;
using eHub.PlugIn;
using eHub.PlugIn.UI;
using eController.Util;

namespace eHub.Contracts.UIConfig;

/// <summary></summary>
public class PacketWrapperDto
{
    public required ConnectorIdentifier ConnectorIdentifier { get; init; }
    public required ImmutableArray<PacketDto> Packets { get; init; }
}

/// <summary></summary>
public record PacketDto
{
    [PacketColumn]
    public required long Id { get; init; }
    [PacketColumn]
    public required PacketStatus Status { get; init; }
    [PacketColumn]
    public required string Channel { get; init; }
    [PacketColumn]
    public required DateTime DateCreated { get; init; }
    [PacketColumn]
    public DateTime? DateChanged { get; init; }
    [PacketColumn]
    public string? Data { get; init; }
    [PacketColumn]
    public string? Metadata { get; init; }
    [PacketColumn]
    public string? PreviewData { get; init; }
    public string DataType { get; init; } = UIDataTypes.Plaintext;
    [PacketColumn]
    public required long? ParentId { get; init; }
    [PacketColumn]
    public int RetryCount { get; init; }
    [PacketColumn]
    public string? DynamicField { get; init; }
    [JsonIgnore]
    [PacketColumn]
    public string? BinaryData { get; init; }
    public bool CanResend { get; init; }
    public string ConnectorName { get; init; } = string.Empty;

    public virtual bool Equals(PacketDto? packetDto) => ObjUtil.AreEqual(this, packetDto);

    public override int GetHashCode() => HashCode.Combine(Id, Channel, ConnectorName);

    public static readonly ImmutableArray<string> PacketColumns
        = typeof(PacketDto).GetProperties()
            .Where(propertyInfo => propertyInfo.IsDefined(typeof(PacketColumn)))
            .Select(propertyInfo => propertyInfo.Name)
            .ToImmutableArray();
}

[AttributeUsage(AttributeTargets.Property)]
public class PacketColumn : Attribute { }
public record PacketResendDto(long Id, string? NewData = null);
