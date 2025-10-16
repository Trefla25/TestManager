using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;
using Microsoft.AspNetCore.Http;

namespace eHub.PlugIn;

/// <summary>
/// Represents configuration for mapping HTTP endpoints used by <see cref="IHttpConnector"/> or <see cref="IHttpPacketTransfer"/>.
/// </summary>
public class HttpConnectorEndpointConfig
{
    /// <summary>
    /// Specifies the path for both incoming and outgoing HTTP connector endpoints.
    /// <para>Usage:</para>
    /// <list type="bullet">
    /// <item>
    /// <description><strong>Incoming:</strong> The route pattern that incoming HTTP requests must match.</description>
    /// </item>
    /// <item>
    /// <description><strong>Outgoing:</strong> The path to which outgoing HTTP requests are sent.</description>
    /// </item>
    /// </list>
    /// <para>Note: This property should follow the format <c>[StringSyntax("Route")]</c>.</para>
    /// </summary>
    [StringSyntax("Route")]
    public string? Path { get; set; }

    /// <summary>
    /// Defines the HTTP method for both incoming and outgoing HTTP requests (e.g., GET, POST).
    /// <para>Usage:</para>
    /// <list type="bullet">
    /// <item>
    /// <description><strong>Incoming:</strong> The HTTP method that incoming requests must match.</description>
    /// </item>
    /// <item>
    /// <description><strong>Outgoing:</strong> The HTTP method used when sending outgoing requests.</description>
    /// </item>
    /// </list>
    /// <para>Note: This property should use the constants defined in the <see cref="HttpMethods"/> class.</para>
    /// </summary>
    public string? HttpMethod { get; set; }

    /// <summary>
    /// Indicates the <c>eMessenger</c> topic for both incoming and outgoing requests.
    /// <para>Usage:</para>
    /// <list type="bullet">
    /// <item>
    /// <description><strong>Incoming:</strong> The eMessenger topic to which messages are redirected using the <c>eMessenger.Ask</c> request.</description>
    /// </item>
    /// <item>
    /// <description><strong>Outgoing:</strong> The eMessenger topic from which messages are received using <c>eMessenger.Answer</c> response.</description>
    /// </item>
    /// </list>
    /// </summary>
    public string? Topic { get; set; }

    /// <summary>
    /// Specifies the content type (MIME type) of the HTTP request (e.g., application/json, application/xml).
    /// <para>Note: This property should use the constants defined in the <see cref="MediaTypeNames"/> class.</para>
    /// </summary>
    [Obsolete("Use the ContentTypes property instead.")]
    public string? ContentType { get => ContentTypes?.FirstOrDefault(); set => ContentTypes = value is { } ? [value] : null; }

    /// <summary>
    /// Specifies the content types (MIME types) accepted by the HTTP request (e.g., application/json, application/xml).
    /// <para>Note: This property should use the constants defined in the <see cref="MediaTypeNames"/> class.</para>
    /// </summary>
    public string[]? ContentTypes { get; set; } // TODO: Should make Hashset<string>

    /// <summary>
    /// Used to specify the target Api used for HTTP requests.
    /// <para>Details:</para>
    /// <list type="bullet">
    /// <item>
    /// <description>If provided, the HTTP requests will be made using the configured Api.</description>
    /// </item>
    /// <item>
    /// <description>If not provided, the <see cref="Path"/> is treated as the absolute URL for the request.</description>
    /// </item>
    /// <item>
    /// <description>If provided for incoming endpoints, the requst will be redirected with the same incoming <see cref="Path"/> and <see cref="HttpMethod"/> to the API.</description>
    /// </item>
    /// </list>
    /// <para><strong>Warning:</strong> If no matching key is found in the Api section of the configuration file, an error will be thrown.</para>
    /// </summary>
    public string? Api { get; set; }

    /// <summary>
    /// Used only for incoming endpoints to specify if the HTTP request requires authorization.
    /// <para>Details:</para>
    /// <list type="bullet">
    /// <item>
    /// <description>If <see langword="true"/>, the HTTP request must be authorized (Authentication must be configured in the connector).</description>
    /// </item>
    /// <item>
    /// <description>If <see langword="false"/>, the HTTP request does not require authorization.</description>
    /// </item>
    /// <item>
    /// <description>If <see langword="null"/>, defaults to <see langword="true"/> if the connector has authentication setup; otherwise, defaults to <see langword="false"/>.</description>
    /// </item>
    /// </list>
    /// </summary>
    public bool? Authorize { get; set; }

    /// <summary>
    /// Specifies the maximum size of the HTTP request body.
    /// </summary>
    public long? RequestSizeLimit { get; set; }

    /// <summary>
    /// Specifies the store mode for the endpoint config into the packet metadata.
    /// </summary>
    public StoreMode? StoreMode { get; set; }

    /// <summary>
    /// Configures how outgoing HTTP requests handle communication errors such as network issues or server downtime.
    /// When <see langword="true"/>, if a communication error occurs, eHub will respond to the original caller with a 202 Accepted status 
    /// and update the packet with a status of <see cref="PacketStatus.Error"/>, triggering it to be reprocessed until it is eventually succesfull.
    /// When <see langword="false"/>, eHub will immediately respond with a 502 Bad Gateway status and update the packet with <see cref="PacketStatus.FatalError"/>, 
    /// preventing further resend attempts.
    /// </summary>
    public bool? ResendPacketsOnCommunicationError { get; set; }

}

/// <summary>
/// Options for how the endpoint configuration is stored in the packet metadata.
/// </summary>
public enum StoreMode
{
    /// <summary>
    /// Only stores the dictionary key of the endpoint configuration in the packet metadata.
    /// This means that the endpoint configuration is always retrieved from the configuration file.
    /// </summary>
    Dynamic,
    /// <summary>
    /// Stores the endpoint configuration in the packet metadata.
    /// This means that the endpoint configuration is always the same as when the packet was created.
    /// </summary>
    Persistent

};
