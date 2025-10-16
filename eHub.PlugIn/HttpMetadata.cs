using System.Net.Mime;
using System.Text.Json.Serialization;
using eHub.PlugIn.Communication;
using Microsoft.Extensions.Primitives;

namespace eHub.PlugIn;

/// <summary>
/// Represents the metadata for an HTTP packet with information about the HTTP request (e.g., URL, http method, content type, API).
/// </summary>
public class HttpMetadata
{
    /// <summary>
    /// The URL of the HTTP request.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// The HTTP method of the request.
    /// </summary>
    public string? HttpMethod { get; set; }

    /// <summary>
    /// The eMessenger topic.
    /// </summary>
    public string? Topic { get; set; }

    /// <summary>
    /// The content type of the HTTP request.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Specifies the content types (MIME types) accepted by the HTTP request (e.g., application/json, application/xml).
    /// <para>Note: This property should use the constants defined in the <see cref="MediaTypeNames"/> class.</para>
    /// </summary>
    public string[]? AcceptedContentTypes { get; set; }

    /// <summary>
    /// The key of the endpoint config.
    /// </summary>
    public string? EndpointKey { get; set; }    

    /// <summary>
    /// The HTTP route parameters extracted from the request path, mapped by parameter name.
    /// </summary>
    public Dictionary<string, string>? RouteParameters { get; set; }

    /// <summary>
    /// The HTTP headers of the request.
    /// </summary>
    [JsonConverter(typeof(StringValuesDictionaryJsonConverter))]
    public Dictionary<string, StringValues>? Headers { get; set; }

    /// <summary>
    /// The query parameters of the request.
    /// </summary>
    [JsonConverter(typeof(StringValuesDictionaryJsonConverter))]
    public Dictionary<string, StringValues>? Query { get; set; }

    /// <summary>
    /// The API used for the <see cref="HttpClient"/> in outgoing requests.
    /// </summary>
    public string? Api { get; set; }

    /// <summary>
    /// Initializes a new empty instance of the <see cref="HttpMetadata"/> class.
    /// </summary>
    public HttpMetadata() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpMetadata"/> class.
    /// </summary>
    /// <param name="url">The URL of the HTTP request.</param>
    /// <param name="httpMethod">The HTTP method of the request.</param>
    /// <param name="topic">The eMessenger topic.</param>
    /// <param name="contentType">The content type of the HTTP request.</param>
    /// <param name="headers">The HTTP headers of the request.</param>
    /// <param name="routeParameters">The HTTP route parameters extracted from the request path, mapped by parameter name.</param>
    /// <param name="api">The API used in outgoing requests.</param>
    /// <param name="endpointKey">The key of the endpoint config.</param>
    /// <param name="acceptedContentTypes">The content types (MIME types) accepted by the HTTP request.</param>
    /// <param name="query">The query parameters of the request.</param>
    public HttpMetadata(string? url, string? httpMethod, string? topic, string? contentType, Dictionary<string, StringValues>? headers = null, Dictionary<string, string>? routeParameters = null, string? api = null, string? endpointKey = null, string[]? acceptedContentTypes = null, Dictionary<string, StringValues>? query = null)
    {
        Url = url;
        HttpMethod = httpMethod;
        Topic = topic;
        ContentType = contentType;
        RouteParameters = routeParameters;
        Headers = headers;
        Api = api;
        EndpointKey = endpointKey;
        AcceptedContentTypes = acceptedContentTypes;
        Query = query;
    }

    /// <summary>
    /// Legacy constructor for <see cref="HttpMetadata"/>. Initializes a new instance of the <see cref="HttpMetadata"/> class.
    /// </summary>
    /// <param name="url">The URL of the HTTP request.</param>
    /// <param name="httpMethod">The HTTP method of the request.</param>
    /// <param name="topic">The eMessenger topic.</param>
    /// <param name="contentType">The content type of the HTTP request.</param>
    /// <param name="headers">The HTTP headers of the request.</param>
    /// <param name="api">The API used in outgoing requests.</param>
    [Obsolete("Use the constructor with route parameters and endpoint key.")]
    public HttpMetadata(string? url, string? httpMethod, string? topic, string? contentType, Dictionary<string, string>? headers, string? api)
    {
        Url = url;
        HttpMethod = httpMethod;
        Topic = topic;
        ContentType = contentType;
        Headers = headers?.ToDictionary(
            header => header.Key,
            header => new StringValues(header.Value));
        Api = api;
    }

    /// <summary>
    /// Converts the <see cref="HttpMetadata"/> to an <see cref="HttpConnectorEndpointConfig"/>.
    /// </summary>
    /// <returns>A new <see cref="HttpConnectorEndpointConfig"/> object.</returns>
    public HttpConnectorEndpointConfig ToEndpointConfig() => new()
    {
        Path = Url,
        HttpMethod = HttpMethod,
        Topic = Topic,
        Api = Api,
        ContentTypes = AcceptedContentTypes
    };
}

