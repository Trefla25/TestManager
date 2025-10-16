using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Primitives;

namespace eHub.PlugIn.Communication;

/// <summary>
/// Represents a base request for any type of connector communication.
/// </summary>
public class ConnectorRequest
{
    /// <summary>
    /// The binary content of the request.
    /// </summary>
    public required ReadOnlyMemory<byte> Content { get; init; }

    /// <summary>
    /// The MIME type of the content.
    /// For more information on MIME types, see <see href="https://developer.mozilla.org/en-US/docs/Web/HTTP/MIME_types/Common_types">MIME types</see>.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// The name of the connector initiating the request.
    /// </summary>
    public required string ConnectorName { get; init; }

    /// <summary>
    /// Information for communication between HTTP connectors.
    /// </summary>
    public HttpConnectorRequest? Http { get; init; }

    /// <summary>
    /// Initializes a new empty instance of the <see cref="ConnectorRequest"/> class.
    /// </summary>
    public ConnectorRequest() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectorRequest"/> class with the specified content, content type, connector name, and optional HTTP information.
    /// </summary>
    /// <param name="content">The binary content of the request.</param>
    /// <param name="contentType">The MIME type of the content.</param>
    /// <param name="connectorName">The name of the connector initiating the request.</param>
    /// <param name="http">Optional HTTP-specific information for communication between connectors.</param>
    [SetsRequiredMembers]
    public ConnectorRequest(ReadOnlyMemory<byte> content, string contentType, string connectorName, HttpConnectorRequest? http = null)
    {
        Content = content;
        ContentType = contentType;
        ConnectorName = connectorName;
        Http = http;
    }

    /// <summary>
    /// Represents HTTP request information for communication between connectors.
    /// </summary>
    public class HttpConnectorRequest
    {
        /// <summary>
        /// The HTTP request path.
        /// </summary>
        public required string Path { get; init; }

        /// <summary>
        /// The HTTP request method (e.g., GET, POST).
        /// </summary>
        public required string Method { get; init; }

        /// <summary>
        /// The HTTP route parameters extracted from the request path, mapped by parameter name.
        /// </summary>
        public IReadOnlyDictionary<string, string>? RouteParameters { get; init; }

        /// <summary>
        /// The HTTP request headers.
        /// </summary>
        [JsonConverter(typeof(StringValuesDictionaryJsonConverter))]
        public IReadOnlyDictionary<string, StringValues>? Headers { get; init; }

        /// <summary>
        /// The query parameters of the request.
        /// </summary>
        [JsonConverter(typeof(StringValuesDictionaryJsonConverter))]
        public IReadOnlyDictionary<string, StringValues>? Query { get; set; }

        /// <summary>
        /// Initializes a new empty instance of the <see cref="HttpConnectorRequest"/> class.
        /// </summary>
        public HttpConnectorRequest() { }

        /// <summary>
        /// Initializes a new empty instance of the <see cref="HttpConnectorRequest"/> class with the specified path, method, and optional headers.
        /// </summary>
        /// <param name="path">The HTTP request path.</param>
        /// <param name="method">The HTTP request method, such as GET or POST.</param>
        /// <param name="routeParameters">The optional HTTP route parameters extracted from the request path, mapped by parameter name.</param>
        /// <param name="headers">The optional HTTP request headers.</param>
        /// <param name="query">The optional query parameters.</param>
        [SetsRequiredMembers]
        public HttpConnectorRequest(string path, string method, IReadOnlyDictionary<string, string>? routeParameters = null, IReadOnlyDictionary<string, StringValues>? headers = null, IReadOnlyDictionary<string, StringValues>? query = null)
        {
            Path = path;
            Method = method;
            RouteParameters = routeParameters;
            Headers = headers;
            Query = query;
        }
    }
}
