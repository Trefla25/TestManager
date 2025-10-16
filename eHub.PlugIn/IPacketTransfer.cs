using eHub.PlugIn.UI;
using Microsoft.Extensions.Options;

namespace eHub.PlugIn;

/// <summary>
/// Implement this interface in a public class to expose a connector with packet transfer tools.<br/>
/// A connector can be instantiated multiple times with difference run parameters via connector config files.
/// 
/// <para>
/// You can request any shared component via dependency injection to the constructor.<br/>
/// Common utilities like 
/// <see href="https://docs.microsoft.com/en-us/dotnet/core/extensions/logging?tabs=command-line#create-logs">Logging</see> and
/// <see href="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-6.0#options-interfaces">Options</see>
/// are available.
/// </para>
/// <para>
/// Use <see cref="IOptions{TOptions}"/>, <see cref="IOptionsSnapshot{TOptions}"/>
/// or <see cref="IOptionsMonitor{TOptions}"/> with a custom object annotated with
/// <see cref="ScriptOptionsAttribute"/> to map the passed config.<br/>
/// You can refer to the 
/// <see href="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-6.0#options-interfaces">Options pattern in ASP.NET Core Documentation</see>
/// for usage.
/// </para>
/// </summary>
public interface IPacketTransfer : IConnector
{
    /// <summary>Event to trigger the processing of a channel group.</summary>
    event TriggerPacketProcessDelegate? TriggerPacketProcess { add { } remove { } }
    /// <summary>Event to receive packet data from the connector.</summary>
    [Obsolete("Use PacketRepository.Create() instead")]
    event PacketReceivedDelegate? PacketReceived { add { } remove { } }
    /// <summary>It is used to reset the retry count of a specific Packet</summary>
    [Obsolete("Use PacketRepository.Update() instead")]
    event ResetPacketRetryCountDelegate? ResetPacketRetryCount { add { } remove { } }
    /// <summary>It is used to reset the retry count of all Packets of a specific status</summary>
    [Obsolete("Use PacketRepository.Update() instead")]
    event ResetAllPacketsRetryCountDelegate? ResetAllPacketsRetryCount { add { } remove { } }
    /// <summary>It is used to process packets binaryData</summary>
    IPacketConverter Converter { get; }
    /// <summary></summary>
    IPacketRepository PacketRepository { get; set; }
    /// <summary>
    /// Called by the connector manager while pooling on db
    /// </summary>
    /// <param name="packet"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    ValueTask<ProcessPacketState> ProcessPacket(PacketData packet, CancellationToken cancellationToken);

    /// <summary>
    /// Create a default UIViewConfig used in case UIViewConfig.json is missing.
    /// </summary>
    /// <returns>A default UIViewConfig</returns>
    ValueTask<UIViewConfig?> BuildUIViewConfig() => new(result: null);
}

/// <summary></summary>
public class PacketData
{
    /// <summary>The packet Id.</summary>
    public long Id { get; set; }
    /// <summary>The packet data in binary.</summary>
    public ReadOnlyMemory<byte> BinaryData { get; set; }
    /// <summary>The Additional metadata of the Packet</summary>
    public string? Metadata { get; set; }
    /// <summary>The channel that the packet is received on.</summary>
    public string Channel { get; set; } = string.Empty;
    /// <summary>The status of the received packet.</summary>
    public PacketStatus Status { get; set; } = PacketStatus.Enqueued;
    /// <summary>A dynamic field that can be used for anything</summary>
    public string? DynamicField { get; set; }

    /// <summary>The parent Packet Id.</summary>
    public long? ParentId { get; set; }
    /// <summary>The packet process retry count.</summary>
    public int RetryCount { get; set; }
    /// <summary>The date and time the packet was created.</summary>
    public DateTime DateCreated { get; set; }
    /// <summary>The date and time the packet was last changed.</summary>
    public DateTime? DateChanged { get; set; }

    /// <summary>Default Constructor.</summary>
    public PacketData() { }

    /// <summary>Constructor for new packets to be be created in the database with specific status.</summary>
    /// <param name="binaryData">The packet data.</param>
    /// <param name="channel">The channel that the packet is received on.</param>
    /// <param name="packetStatus">The status of the received packet.</param>
    /// <param name="parentId">Optional.The parent Packet Id.</param>
    public PacketData(ReadOnlyMemory<byte> binaryData, string channel, PacketStatus packetStatus = PacketStatus.Enqueued, long? parentId = null)
    {
        BinaryData = binaryData;
        Channel = channel;
        Status = packetStatus;
        ParentId = parentId;
    }
}

/// <summary>The return states of packet processing.</summary>
public enum ProcessPacketState
{
    /// <summary>Success state.</summary>
    Success,
    /// <summary>Retry state. Will increment the retry count and retry processing.</summary>
    Retry,
    /// <summary>Error state. Will increment the retry count and retry processing.</summary>
    Error,
    /// <summary>Fatal error state. Will not enter processing again.</summary>
    FatalError,
    /// <summary>In progress state. Will not enter processing again.</summary>
    InProgress,
    /// <summary>Retry unchanged state. Will leave everything unchanged and retry processing.</summary>
    RetryUnchanged
}

/// <summary>Triggers the processing of the channel group containing the provided channel.</summary>
/// <param name="channel"></param>
public delegate void TriggerPacketProcessDelegate(string channel);

/// <summary>Delegate type to receive a packet from Connectors.</summary>
/// <param name="eventArgs"></param>
[Obsolete("Use PacketRepository.Create() instead")]
public delegate void PacketReceivedDelegate(PacketData eventArgs);

/// <summary>Delegate type to reset a packet retry count.</summary>
[Obsolete("Use PacketRepository.Update() instead")]
public delegate void ResetPacketRetryCountDelegate(long packetId);

/// <summary>Delegate type to reset all packets retry count by specific status.</summary>
[Obsolete("Use PacketRepository.Update() instead")]
public delegate void ResetAllPacketsRetryCountDelegate(PacketStatus packetStatus);
