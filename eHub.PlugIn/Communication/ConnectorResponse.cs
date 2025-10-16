using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Primitives;

namespace eHub.PlugIn.Communication;

/// <summary>
/// Represents a base response for any type of connector communication.
/// </summary>
public class ConnectorResponse
{
    /// <summary>
    /// The binary content of the response.
    /// </summary>
    public required ReadOnlyMemory<byte> Content { get; init; }

    /// <summary>
    /// The MIME type of the content.
    /// For more information on MIME types, see <see href="https://developer.mozilla.org/en-US/docs/Web/HTTP/MIME_types/Common_types">MIME types</see>.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// The name of the connector that provided this response.
    /// </summary>
    public required string ConnectorName { get; init; }

    /// <summary>
    /// Information for communication between HTTP connectors.
    /// </summary>
    public HttpConnectorResponse? Http { get; init; }

    /// <summary>
    /// Information for communication between Packet Transfer connectors.
    /// </summary>
    public PacketTransferResponse? PacketTransfer { get; init; }

    /// <summary>
    /// Initializes a new empty instance of the <see cref="ConnectorResponse"/> class.
    /// </summary>
    public ConnectorResponse() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectorResponse"/> class with the specified content, content type, connector name, and optional HTTP and Packet Transfer responses.
    /// </summary>
    /// <param name="content">The binary content of the response.</param>
    /// <param name="contentType">The MIME type of the content.</param>
    /// <param name="connectorName">The name of the connector that provided this response.</param>
    /// <param name="http">Optional HTTP-specific response information.</param>
    /// <param name="packetTransfer">Optional Packet Transfer-specific response information.</param>
    [SetsRequiredMembers]
    public ConnectorResponse(ReadOnlyMemory<byte> content, string contentType, string connectorName, HttpConnectorResponse? http = null, PacketTransferResponse? packetTransfer = null)
    {
        Content = content;
        ContentType = contentType;
        ConnectorName = connectorName;
        Http = http;
        PacketTransfer = packetTransfer;
    }

    /// <summary>
    /// Represents HTTP response information for communication between connectors.
    /// </summary>
    public class HttpConnectorResponse
    {
        /// <summary>
        /// The HTTP status code for the response.
        /// </summary>
        public required int StatusCode { get; init; }

        /// <summary>
        /// The HTTP headers associated with the response.
        /// </summary>
        [JsonConverter(typeof(StringValuesDictionaryJsonConverter))]
        public IReadOnlyDictionary<string, StringValues>? Headers { get; init; }

        /// <summary>
        /// Gets a value indicating whether the HTTP response status code is a success (2xx).
        /// </summary>
        [JsonIgnore]
        public bool IsSuccessStatusCode => StatusCode >= 200 && StatusCode < 300;

        /// <summary>
        /// Initializes a new empty instance of the <see cref="HttpConnectorResponse"/> class.
        /// </summary>
        public HttpConnectorResponse() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpConnectorResponse"/> class with the specified status code and optional headers.
        /// </summary>
        /// <param name="statusCode">The HTTP status code for the response.</param>
        /// <param name="headers">The HTTP headers associated with the response.</param>
        [SetsRequiredMembers]
        public HttpConnectorResponse(int statusCode, IReadOnlyDictionary<string, StringValues>? headers = null)
        {
            StatusCode = statusCode;
            Headers = headers;
        }
    }

    /// <summary>
    /// Represents Packet Transfer response information for communication between connectors.
    /// </summary>
    public class PacketTransferResponse
    {
        /// <summary>
        /// The state of the packet transfer process.
        /// </summary>
        public required ProcessPacketState ProcessPacketState { get; init; }

        /// <summary>
        /// Initializes a new empty instance of the <see cref="PacketTransferResponse"/> class.
        /// </summary>  
        public PacketTransferResponse() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="PacketTransferResponse"/> class with the specified packet process state.
        /// </summary>
        /// <param name="processPacketState">The state of the packet transfer process.</param>
        [SetsRequiredMembers]
        public PacketTransferResponse(ProcessPacketState processPacketState)
        {
            ProcessPacketState = processPacketState;
        }
    }
}
