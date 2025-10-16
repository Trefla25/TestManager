using eController.Util;
using eHub.PlugIn;
using Microsoft.Extensions.Options;

namespace eHub.Config;

#pragma warning disable CS8618 // Disable nullability warning for ORM-Wrapped config stuff
/// <summary>Root configuration object for the IntegrationHub</summary>
public class IntegrationHubConfig
{
    /// <summary>Default path in the appsettings.json</summary>
    public const string DefaultKey = "IntegrationHub";
}

/// <summary>A Dictionary of connector templates.</summary>
/// <remarks>
/// (<see cref="string"/>) <i>Key</i>: The template name.<br/>
/// (<see cref="ConnectorTemplate"/>) <i>Value</i>: The template declaration<br/>
/// </remarks>
public class NamedConnectorTemplateCollection : Dictionary<string, ConnectorTemplate>
{
    public NamedConnectorTemplateCollection() : base() { }
    public NamedConnectorTemplateCollection(IEnumerable<KeyValuePair<string, ConnectorTemplate>> templates) : base(templates) { }
}

/// <summary>A connector template. Each template found will be automatically started, and reloaded when changed.</summary>
public class ConnectorTemplate : IEquatable<ConnectorTemplate>
{
    /// <summary>The full Namespace + Class name to find.</summary>
    public string Type { get; init; }    
    /// <summary>Optional. An arbitrary json object which can be passed as an <see cref="IOptions{T}"/> to a connector.</summary>
    public IConfigurationSection? Config { get; init; }

	/// <summary>Required for <see cref="IPacketTransfer"/>.</summary>
	public PacketTransfer? PacketTransfer { get; set; }

    /// <summary>Required for <see cref="IHttpConnector"/> and <see cref="IHttpPacketTransfer"/>.
    /// Represent the incoming HTTP connector configurations.</summary>
    public HttpIncoming? HttpIncoming { get; set; }

    /// <summary>Required for <see cref="IHttpConnector"/> and <see cref="IHttpPacketTransfer"/>.
    /// Represent the outgoing HTTP connector configurations.</summary>
    public HttpOutgoing? HttpOutgoing { get; set; }

    /// <summary>
    /// Represents the scheduler configuration for a connector.
    /// </summary>
    public IConfigurationSection? Scheduler { get; set; }

    /// <summary>When not enabled, connector will not be started automatically.
    /// You can still run it via the ui.</summary>
    public bool Enabled { get; init; } = true;

    /// <inheritdoc/>
    public bool Equals(ConnectorTemplate? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Type == other.Type
            && Enabled == other.Enabled;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as ConnectorTemplate);
    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Type, Enabled);
}

public class PacketTransfer : IEquatable<PacketTransfer>
{
    /// <summary>Path to a Database file</summary>
    public string? DbPath { get; set; }

    public Dictionary<string,string> SqlitePragmas { get; set; } = [];
    /// <summary>
    /// All groups based on packet channels. Groups can have any number of channels. 
    /// All groups will be processed concurrently.
    /// Within each group, packets are processed in the order they are received.
    /// The groups should not overlap in any channel.
    /// </summary>
    public Dictionary<string, ChannelGroup> ChannelGroups { get; set; } = [];

    public bool Equals(PacketTransfer? other) => ObjUtil.AreEqual(this, other);
    public override bool Equals(object? obj) => Equals(obj as PacketTransfer);
    public override int GetHashCode() => HashCode.Combine(DbPath, ChannelGroups);
}

public class ChannelGroup : IEquatable<ChannelGroup>
{
    public ChannelMode Mode { get; set; } = ChannelMode.Sequential;
    /// <summary>The interval for database polling</summary>
    public TimeSpan DbPollInterval { get; set; } = TimeSpan.FromSeconds(1);
    /// <summary>The maximum number of packets processed per cycle.</summary>
    public int PacketsPerCycle { get; set; } = 1;
    /// <summary>The retention/lifetime of a packet until removed.</summary>
    public Dictionary<string, string> PacketRetention { get; set; } = new Dictionary<string, string> { { "Default", "30d" } };

    /// <summary>The interval at which the cleaner will check for packets to remove.</summary>
    public TimeSpan CleanerInterval { get; set; } = TimeSpan.FromHours(1);
    /// <summary>List of all channels inside one group</summary>
    public HashSet<string> Channels { get; set; } = [];
    /// <summary>Whether the packets can be resent after already processed.</summary>
    public bool CanResend { get; set; } = true;

    public bool Equals(ChannelGroup? other) => ObjUtil.AreEqual(this, other);
    public override bool Equals(object? obj) => Equals(obj as ChannelGroup);
    public override int GetHashCode() => HashCode.Combine(Mode, DbPollInterval,
        PacketsPerCycle, PacketRetention, CleanerInterval, Channels, CanResend);
}

public enum ChannelMode
{
    Sequential,
    Concurrent
}
#pragma warning restore CS8618
