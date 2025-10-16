using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Text;
using System.Net.Mime;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using System.Text.Json.Serialization;
using eHub.PlugIn.Communication;
using eHub.PlugIn.UI;
using Microsoft.AspNetCore.WebUtilities;

namespace eHub.PlugIn;

/// <summary>
/// An <see cref="IPacketConverter"/> class for handling HTTP packet transformations.
/// </summary>
public class HttpPacketTransferConverter : IPacketConverter
{
    /// <summary>
    /// JSON serializer options for serializing and deserializing HTTP packets.
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    /// <summary>
    /// Builds the HTTP metadata string from given content and details.
    /// </summary>
    /// <param name="content">The HTTP content data.</param>
    /// <param name="url">The URL associated with the HTTP request.</param>
    /// <param name="httpMethod">The HTTP method (GET, POST, etc.).</param>
    /// <param name="topic">The topic associated with the packet.</param>
    /// <param name="contentType">The content type of the HTTP request.</param>
    /// <param name="headers">The headers of the HTTP request.</param>
    /// <param name="api">The API endpoint associated with the HTTP request.</param>
    /// <param name="routeValues">The route values of the HTTP request url.</param>
    /// <param name="query">The query parameters of the request.</param>
    /// <returns>A JSON string representing the HTTP metadata.</returns>
    public virtual string BuildHttpMetadata(ReadOnlyMemory<byte> content, string? url = null, string? httpMethod = null, string? topic = null, string? contentType = null, IHeaderDictionary? headers = null, string? api = null, RouteValueDictionary? routeValues = null, IQueryCollection? query = null)
    {
        var routeParams = routeValues?.Select(route => KeyValuePair.Create(route.Key, route.Value?.ToString() ?? ""));
        return BuildHttpMetadata(
            content: content,
            url: url,
            httpMethod: httpMethod,
            topic: topic,
            contentType: contentType,
            headers: headers,
            api: api,
            routeValues: routeParams,
            query: query?.ToDictionary());
    }

    /// <summary>
    /// Builds the HTTP metadata string from given content and details.
    /// </summary>
    /// <param name="content">The HTTP content data.</param>
    /// <param name="url">The URL associated with the HTTP request.</param>
    /// <param name="httpMethod">The HTTP method (GET, POST, etc.).</param>
    /// <param name="topic">The topic associated with the packet.</param>
    /// <param name="contentType">The content type of the HTTP request.</param>
    /// <param name="headers">The headers of the HTTP request.</param>
    /// <param name="api">The API endpoint associated with the HTTP request.</param>
    /// <param name="routeValues">The route values of the HTTP request url.</param>
    /// <param name="endpointKey">The key of the endpoint config.</param>
    /// <param name="acceptedContentTypes">The content types (MIME types) accepted by the HTTP request.</param>
    /// <param name="query">The query parameters of the request.</param>
    /// <returns>A JSON string representing the HTTP metadata.</returns>
    public virtual string BuildHttpMetadata(
        ReadOnlyMemory<byte> content,
        string? url = null,
        string? httpMethod = null,
        string? topic = null, string?
        contentType = null,
        IEnumerable<KeyValuePair<string, StringValues>>? headers = null,
        string? api = null,
        IEnumerable<KeyValuePair<string, string>>? routeValues = null,
        string? endpointKey = null, string[]? acceptedContentTypes = null,
        IEnumerable<KeyValuePair<string, StringValues>>? query = null)
    {
        var headersToStore = headers?.Where(header => !ConnectorHeaders.SensitiveHeaders.Contains(header.Key)).ToDictionary();
        var routeParameters = routeValues?.ToDictionary();
        var httpMetadata = new HttpMetadata(url, httpMethod, topic, contentType, headersToStore, routeParameters, api, endpointKey, acceptedContentTypes, query?.ToDictionary());
        var metadataString = JsonSerializer.Serialize(httpMetadata, JsonOptions);

        return metadataString;
    }

    /// <summary>Converts HTTP request content and metadata to database binary data format.</summary>
    /// <param name="content">The HTTP request content data.</param>
    /// <param name="metadata">The metadata of the packet.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public virtual ValueTask<ReadOnlyMemory<byte>> RawToBinaryDataConverter(ReadOnlyMemory<byte> content, string? metadata, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(content);

    /// <summary>Converts a packet to its raw data format.</summary>
    /// <param name="packet">The packet.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public virtual ValueTask<ReadOnlyMemory<byte>> PacketToRawDataConverter(PacketData packet, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(packet.BinaryData);

    /// <summary>Converts a packet to UI data format.</summary>
    /// <param name="packet">The packet data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public virtual ValueTask<UIConversionInfo> PacketToUIDataConverter(PacketData packet, CancellationToken cancellationToken = default)
    {
        if (packet.Channel.EndsWith(":Error"))
        {
            var data = Encoding.UTF8.GetString(packet.BinaryData.Span);
            var dataPreview = data;
            var dataType = UIDataTypes.Plaintext;
            return ValueTask.FromResult(new UIConversionInfo(data, dataPreview, dataType));
        }

        if (packet.Metadata is null)
        {
            throw new Exception(nameof(packet.Metadata));
        }

        var httpMetadata = GetHttpMetadata(packet.Metadata);

        var url = QueryHelpers.AddQueryString(httpMetadata.Url ?? string.Empty, httpMetadata.Query ?? []);

        return ValueTask.FromResult(new UIConversionInfo(
            Encoding.UTF8.GetString(packet.BinaryData.Span),
            $"{httpMetadata.HttpMethod} {url}",
            ContentTypeMapper.ToUIDataType(httpMetadata.ContentType)));
    }

    /// <summary>Converts UI data to binary data format.</summary>
    /// <param name="data">The UI conversion information.</param>
    /// <param name="metadata">The metadata of the packet.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public virtual ValueTask<ReadOnlyMemory<byte>> UIToBinaryDataConverter(UIConversionInfo data, string? metadata, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<ReadOnlyMemory<byte>>(Encoding.UTF8.GetBytes(data.Data).AsMemory());

    /// <summary>
    /// Retrieves an HTTP metadata object from a metadata string.
    /// </summary>
    /// <param name="metadata">The http metadata.</param>
    public virtual HttpMetadata GetHttpMetadata(string metadata)
    {
        var httpMetadata = JsonSerializer.Deserialize<HttpMetadata>(metadata, JsonOptions) ?? throw new Exception("Could not deserialize metadata");
        return httpMetadata;
    }
}

/// <summary>
/// Provides methods for mapping <see cref="MediaTypeNames"/> to and from <see cref="UIDataTypes"/>.
/// </summary>
public static class ContentTypeMapper
{
    /// <summary>
    /// Maps <see cref="MediaTypeNames"/> to <see cref="UIDataTypes"/>.
    /// </summary>
    /// <param name="contentType">The content type in <see cref="MediaTypeNames"/> format.</param>
    /// <returns>The corresponding <see cref="UIDataTypes"/>.</returns>
    public static string ToUIDataType(string? contentType) => contentType switch
    {
        MediaTypeNames.Application.Json => UIDataTypes.Json,
        MediaTypeNames.Application.Xml => UIDataTypes.Xml,
        _ => UIDataTypes.Plaintext
    };

    /// <summary>
    /// Maps <see cref="UIDataTypes"/> to <see cref="MediaTypeNames"/>.
    /// </summary>
    /// <param name="dataType">The data type in <see cref="UIDataTypes"/> format.</param>
    /// <returns>The corresponding <see cref="MediaTypeNames"/>.</returns>
    public static string FromUIDataType(string? dataType) => dataType switch
    {
        UIDataTypes.Json => MediaTypeNames.Application.Json,
        UIDataTypes.Xml => MediaTypeNames.Application.Xml,
        _ => MediaTypeNames.Text.Plain
    };
}
