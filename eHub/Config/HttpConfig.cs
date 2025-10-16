using eHub.PlugIn;
using eHub.Scripting.Connectors.Configuration;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace eHub.Config;

#pragma warning disable CS8618 // Disable nullability warning
public class HttpIncoming
{
    /// <summary>Required for <see cref="IHttpConnector"/> and <see cref="IHttpPacketTransfer"/>.
    /// Represents the Kestrel configurations of the connector's internal web host.</summary>
    public IConfigurationSection Kestrel { get; set; }
    /// <summary>Required for <see cref="IHttpConnector"/> and <see cref="IHttpPacketTransfer"/>.
    /// Represents the authentication configurations of the connector's internal web host.</summary>
    public AuthenticationConfig? Auth { get; set; }
    /// <summary>Required for <see cref="IHttpConnector"/> and <see cref="IHttpPacketTransfer"/>.
    /// Represents the endpoint configurations for the incoming HTTP requests.</summary>
    public Dictionary<string, HttpConnectorEndpointConfig> Endpoints { get; set; } = [];
    /// <summary>Required for <see cref="IHttpPacketTransfer"/>.
    /// Whether the <see cref="PacketStatus.InProgress"/> packets should be resent if the eHub stops is interrupted.
    /// </summary>
    public bool ResendInProgressPacketsOnInterrupt { get; set; }
    public bool InsertUnauthorizedPackets { get; set; }
    /// <summary>
    /// List of HTTP paths that should be excluded from packet transfer.
    /// </summary>
    public HashSet<string> ExcludedEndpointsFromPacketTransfer { get; set; } = [];

    public StoreMode? StoreMode { get; set; }

    public KestrelBuilder? KestrelBuilder { get; set; }

    public void ApplyKestrelTo(KestrelServerOptions kestrelOptions)
    {
        if (Kestrel.GetSection("Endpoints").GetChildren().Any())
        {
            kestrelOptions.Configure(Kestrel);
        }
        else
        {
            KestrelBuilder?.ApplyListeners(kestrelOptions);
        }

        KestrelBuilder?.ApplyLimits(kestrelOptions);
    }
}

public class HttpOutgoing
{
    /// <summary>For configuring the API used by the connector.</summary>
    public Dictionary<string, ApiConfig> Api { get; set; }
    /// <summary>Required for <see cref="IHttpConnector"/> and <see cref="IHttpPacketTransfer"/>.
    /// Represents the endpoint configurations for the outgoing HTTP requests.</summary>
    public Dictionary<string, HttpConnectorEndpointConfig> Endpoints { get; set; } = [];
    /// <summary>Required for <see cref="IHttpPacketTransfer"/>.
    /// Whether the <see cref="PacketStatus.InProgress"/> packets should be resent if the eHub stops is interrupted.
    /// </summary>
    public bool ResendInProgressPacketsOnInterrupt { get; set; }
    /// <summary>
    /// List of topics that should be excluded from packet transfer.
    /// </summary>
    public HashSet<string> ExcludedEndpointsFromPacketTransfer { get; set; } = [];

    public StoreMode StoreMode { get; set; } = StoreMode.Dynamic;

    /// <summary>
    /// Specifies if the packets should be retried on communication error.
    /// </summary>
    public bool ResendPacketsOnCommunicationError { get; set; }
}
#pragma warning restore CS8618
