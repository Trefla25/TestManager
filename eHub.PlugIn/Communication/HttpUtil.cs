using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Net.Mime;
using System.Text.Json.Serialization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace eHub.PlugIn.Communication;

/// <summary>
/// Provides utility methods for handling HTTP requests and responses.
/// </summary>
public static class HttpUtil
{
    /// <summary>
    /// Converts an <see cref="IResult"/> to a <see cref="ConnectorResponse"/> by executing it within a temporary <see cref="DefaultHttpContext"/> and capturing the response.
    /// </summary>
    /// <param name="result">The <see cref="IResult"/> to be executed and converted.</param>
    /// <returns>A <see cref="ConnectorResponse"/> containing the content, status code, and headers from the executed <see cref="IResult"/>.</returns>
    /// <remarks>
    /// <b>IMPORTANT:</b> This method reads the stream from the specified <see cref="IResult"/> exactly once, making it unavailable for further use.  
    /// The <see cref="IResult"/> should <b>NOT</b> be accessed or read again after calling this method, as its underlying response stream may be consumed or closed.
    /// </remarks>
    public static async ValueTask<ConnectorResponse> GetConnectorResponseFromIResult(IResult result)
    {
        var tempContext = new DefaultHttpContext() { RequestServices = EmptyServiceProvider.Instance };

        await using var responseStream = new MemoryStream();
        tempContext.Response.Body = responseStream;

        await result.ExecuteAsync(tempContext);

        var headers = tempContext.Response.Headers
            .ToDictionary()
            .AsReadOnly();

        return new ConnectorResponse(
            responseStream.GetBuffer().AsMemory(0, (int)responseStream.Length),
            tempContext.Response.ContentType ?? MediaTypeNames.Application.Octet,
            "",
            new ConnectorResponse.HttpConnectorResponse(tempContext.Response.StatusCode, headers));
    }

    /// <summary>
    /// Converts an <see cref="HttpResponseMessage"/> to a <see cref="ConnectorResponse"/> by extracting its content, status code, and headers.
    /// </summary>
    /// <param name="response">The <see cref="HttpResponseMessage"/> to be converted.</param>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <returns>A <see cref="ConnectorResponse"/> containing the content, status code, and headers from the specified <see cref="HttpResponseMessage"/>.</returns>
    public static async ValueTask<ConnectorResponse> GetConnectorResponseFromHttpResponseMessage(HttpResponseMessage response, string connectorName)
    {
        await using var responseStream = new MemoryStream((int)(response.Content.Headers.ContentLength ?? 0));
        await response.Content.CopyToAsync(responseStream);

        var responseContent = (ReadOnlyMemory<byte>)responseStream.GetBuffer().AsMemory(0, (int)responseStream.Length);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? MediaTypeNames.Application.Octet;

        responseContent = EncodingUtil.TrimBom(responseContent, contentType);
        var statusCode = (int)response.StatusCode;

        var headers = response.Headers
            .ToDictionary(
                header => header.Key,
                header => new StringValues([.. header.Value]))
            .AsReadOnly();

        if (headers.TryGetValue(ConnectorHeaders.ProcessPacketStateHeader, out var stateHeader) && Enum.TryParse<ProcessPacketState>(stateHeader, out var processPacketState))
        {
            return new ConnectorResponse(responseContent, contentType, connectorName, new(statusCode, headers), new(processPacketState));
        }

        return new ConnectorResponse(responseContent, contentType, connectorName, new(statusCode, headers));
    }

    /// <summary>
    /// Converts an <see cref="HttpRequest"/> into a <see cref="ConnectorRequest"/> by capturing the request content, headers, and other properties.
    /// </summary>
    /// <param name="request">The <see cref="HttpRequest"/> to be converted.</param>
    /// <param name="connectorName">The name of the connector initiating this request.</param>
    /// <returns>A <see cref="ConnectorRequest"/> containing the request content, headers, content type, and connector name.</returns>
    public static async ValueTask<ConnectorRequest> GetConnectorRequestFromHttpRequest(HttpRequest request, string connectorName)
    {
        await using var memoryStream = new MemoryStream((int)(request.ContentLength ?? 0));
        await request.Body.CopyToAsync(memoryStream);
        var contentBytes = (ReadOnlyMemory<byte>)memoryStream.GetBuffer().AsMemory(0, (int)memoryStream.Length);

        contentBytes = EncodingUtil.TrimBom(contentBytes, request.ContentType);

        var routeParameters = request.RouteValues
            .ToDictionary(
                routeParam => routeParam.Key,
                routeParam => routeParam.Value?.ToString() ?? "")
            .AsReadOnly();

        var headers = request.Headers
            .Where(header => !ConnectorHeaders.SensitiveHeaders.Contains(header.Key))
            .ToDictionary(
                header => header.Key,
                header => new StringValues([.. header.Value]))
            .AsReadOnly();

        var query = request.Query.ToDictionary();

        return new ConnectorRequest(contentBytes, request.ContentType ?? MediaTypeNames.Application.Octet, connectorName, new(request.Path, request.Method, routeParameters, headers, query));
    }
}

/// <summary>
/// A JSON converter for serializing and deserializing collections of key-value pairs where values are of type <see cref="StringValues"/>.
/// Supports both single string and string array representations in JSON, and works for <see cref="IDictionary{TKey, TValue}"/>, 
/// <see cref="IReadOnlyDictionary{TKey, TValue}"/>, and <see cref="IEnumerable{KeyValuePair}"/>.
/// </summary>
public class StringValuesDictionaryJsonConverter : JsonConverter<object>
{
    /// <summary>
    /// Determines if the type can be converted by this converter.
    /// </summary>
    /// <param name="typeToConvert">The type to check.</param>
    /// <returns>True if the type is compatible; otherwise, false.</returns>
    public override bool CanConvert(Type typeToConvert) =>
        typeof(IDictionary<string, StringValues>).IsAssignableFrom(typeToConvert) ||
        typeof(IReadOnlyDictionary<string, StringValues>).IsAssignableFrom(typeToConvert) ||
        typeof(IEnumerable<KeyValuePair<string, StringValues>>).IsAssignableFrom(typeToConvert);

    /// <summary>
    /// Reads and converts JSON data into a collection of key-value pairs where the keys are strings and the values are <see cref="StringValues"/>.
    /// Handles both single strings and arrays of strings as valid JSON formats for each value.
    /// </summary>
    /// <param name="reader">The JSON reader from which to read the JSON data.</param>
    /// <param name="typeToConvert">The type being converted.</param>
    /// <param name="options">Options for the JSON serializer.</param>
    /// <returns>A <see cref="Dictionary{TKey, TValue}"/> populated with values from the JSON data.</returns>
    /// <exception cref="JsonException">Thrown when an unexpected JSON token is encountered.</exception>
    public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dictionary = new Dictionary<string, StringValues>();

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token.");
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return dictionary;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName token.");
            }

            var key = reader.GetString();

            if (string.IsNullOrEmpty(key))
            {
                throw new JsonException("PropertyName token should not be null or empty.");
            }

            reader.Read();

            if (reader.TokenType == JsonTokenType.String)
            {
                dictionary[key] = new StringValues(reader.GetString());
            }
            else if (reader.TokenType == JsonTokenType.StartArray)
            {
                var values = new List<string?>();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        values.Add(reader.GetString());
                    }
                    else if (reader.TokenType == JsonTokenType.EndArray)
                    {
                        break;
                    }
                }
                dictionary[key] = new StringValues([.. values]);
            }
            else
            {
                throw new JsonException("Unexpected token type.");
            }
        }

        throw new JsonException("Expected EndObject token.");
    }

    /// <summary>
    /// Writes a collection of key-value pairs to JSON. Each dictionary entry is written with its key as the property name.
    /// If <see cref="StringValues.Count"/> is 1, the value is written as a single JSON string; otherwise, it is written as a JSON array.
    /// </summary>
    /// <param name="writer">The JSON writer to which data will be written.</param>
    /// <param name="value">The collection of key-value pairs to serialize.</param>
    /// <param name="options">Options for the JSON serializer.</param>
    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        // Cast value to IEnumerable<KeyValuePair<string, StringValues>> to handle all compatible types
        var dictionary = (IEnumerable<KeyValuePair<string, StringValues>>)value;

        foreach (var kvp in dictionary)
        {
            writer.WritePropertyName(kvp.Key);

            if (kvp.Value.Count == 1)
            {
                writer.WriteStringValue(kvp.Value.ToString());
            }
            else
            {
                writer.WriteStartArray();
                foreach (var item in kvp.Value)
                {
                    writer.WriteStringValue(item);
                }
                writer.WriteEndArray();
            }
        }

        writer.WriteEndObject();
    }
}

internal class EmptyServiceProvider : IServiceProvider
{
    public static readonly EmptyServiceProvider Instance = new();
    public object? GetService(Type serviceType) => serviceType == typeof(ILoggerFactory) ? NullLoggerFactory.Instance : null;
}

