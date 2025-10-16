using System.Text;
using System.Net.Mime;
using System.Text.Json;
using System.Collections.Immutable;
using Microsoft.Extensions.Primitives;

namespace eHub.PlugIn.Communication;

/// <summary>
/// Factory class for creating <see cref="ConnectorResponse"/> instances in different connector communication scenarios.
/// </summary>
public static class ConnectorResponses
{
    /// <summary>
    /// Creates an acknowledgment <see cref="ConnectorResponse"/> containing only the specified connector name. 
    /// Suitable for scenarios where no content is required.
    /// </summary>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <returns>A <see cref="ConnectorResponse"/> containing only the connector name, indicating acknowledgment of the action.</returns>
    public static ConnectorResponse Acknowledge(string connectorName)
        => new(ReadOnlyMemory<byte>.Empty, "", connectorName);

    /// <summary>
    /// Creates a <see cref="ConnectorResponse"/> with the specified content, content type, and connector name.
    /// </summary>
    /// <param name="content">The response content.</param>
    /// <param name="contentType">The MIME type of the content (e.g., "application/json").</param>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <returns>A <see cref="ConnectorResponse"/> containing the specified content, content type, and connector name.</returns>
    public static ConnectorResponse Content(string content, string contentType, string connectorName)
        => new(Encoding.UTF8.GetBytes(content), contentType, connectorName);

    /// <summary>
    /// Creates a <see cref="ConnectorResponse"/> with the specified binary content, content type, and connector name.
    /// </summary>
    /// <param name="content">The binary content as a byte array.</param>
    /// <param name="contentType">The MIME type of the content (e.g., "application/octet-stream").</param>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <returns>A <see cref="ConnectorResponse"/> containing the specified binary content, content type, and connector name.</returns>
    public static ConnectorResponse BinaryContent(ReadOnlyMemory<byte> content, string contentType, string connectorName)
        => new(content, contentType, connectorName);

    /// <summary>
    /// Creates a text <see cref="ConnectorResponse"/> with specified content and connector name.
    /// </summary>
    /// <param name="content">The response content.</param>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <returns>A <see cref="ConnectorResponse"/> containing the specified content, content type, and connector name.</returns>
    public static ConnectorResponse Text(string content, string connectorName)
        => new(Encoding.UTF8.GetBytes(content), MediaTypeNames.Text.Plain, connectorName);

    /// <summary>
    /// Creates a JSON <see cref="ConnectorResponse"/> with the specified object and connector name.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize as JSON.</typeparam>
    /// <param name="data">The object to serialize and use as the response content.</param>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <returns>A <see cref="ConnectorResponse"/> containing the JSON-encoded content, content type, and connector name.</returns>
    public static ConnectorResponse Json<T>(T data, string connectorName)
        => new(JsonSerializer.SerializeToUtf8Bytes(data), MediaTypeNames.Application.Json, connectorName);

    /// <summary>
    /// Creates a <see cref="ConnectorResponse"/> with the specified content, content type, connector name, and HTTP status code.
    /// </summary>
    /// <param name="content">The response content.</param>
    /// <param name="contentType">The MIME type of the content (e.g., "application/json").</param>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <param name="statusCode">The status code for the response, defaults to 200 (OK).</param>
    /// <returns>A <see cref="ConnectorResponse"/> containing the specified content, content type, connector name, and HTTP status code.</returns>
    public static ConnectorResponse HttpContent(string content, string contentType, string connectorName, int statusCode = 200)
        => new(Encoding.UTF8.GetBytes(content), contentType, connectorName, new(statusCode));

    /// <summary>
    /// Creates a <see cref="ConnectorResponse"/> with the specified binary content, content type, connector name, and HTTP status code.
    /// </summary>
    /// <param name="content">The binary content as a byte array.</param>
    /// <param name="contentType">The MIME type of the content (e.g., "application/octet-stream").</param>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <param name="statusCode">The status code for the response, defaults to 200 (OK).</param>
    /// <returns>A <see cref="ConnectorResponse"/> containing the specified binary content, content type, and connector name.</returns>
    public static ConnectorResponse HttpBinaryContent(ReadOnlyMemory<byte> content, string contentType, string connectorName, int statusCode = 200)
        => new(content, contentType, connectorName, new(statusCode));

    /// <summary>
    /// Creates a text <see cref="ConnectorResponse"/> with the specified content, connector name, and HTTP status code.
    /// </summary>
    /// <param name="content">The response content.</param>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <param name="statusCode">The status code for the response, defaults to 200 (OK).</param>
    /// <returns>A <see cref="ConnectorResponse"/> containing the specified content, content type, connector name, and HTTP status code.</returns>
    public static ConnectorResponse HttpText(string content, string connectorName, int statusCode = 200)
        => new(Encoding.UTF8.GetBytes(content), MediaTypeNames.Text.Plain, connectorName, new(statusCode));

    /// <summary>
    /// Creates a JSON <see cref="ConnectorResponse"/> with the specified object, connector name, and HTTP status code.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize as JSON.</typeparam>
    /// <param name="data">The object to serialize and use as the response content.</param>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <param name="statusCode">The status code for the response, defaults to 200 (OK).</param>
    /// <returns>A <see cref="ConnectorResponse"/> containing the JSON-encoded content, content type, connector name, and HTTP status code.</returns>
    public static ConnectorResponse HttpJson<T>(T data, string connectorName, int statusCode = 200)
        => new(JsonSerializer.SerializeToUtf8Bytes(data), MediaTypeNames.Application.Json, connectorName, new(statusCode));

    /// <summary>
    /// Creates a <see cref="ConnectorResponse"/> with the specified connector name and HTTP status code, but no content.
    /// </summary>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <param name="statusCode">The status code for the response.</param>
    /// <returns>A <see cref="ConnectorResponse"/> containing the specified connector name and HTTP status code.</returns>
    public static ConnectorResponse HttpStatus(string connectorName, int statusCode)
        => new(ReadOnlyMemory<byte>.Empty, "", connectorName, new(statusCode));

    /// <summary>
    /// Creates a <see cref="ConnectorResponse"/> with a 200 (OK) status code.
    /// </summary>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <returns>A <see cref="ConnectorResponse"/> with a 200 (OK) status code.</returns>
    public static ConnectorResponse HttpOk(string connectorName)
        => new(ReadOnlyMemory<byte>.Empty, "", connectorName, new(200));

    /// <summary>
    /// Creates a <see cref="ConnectorResponse"/> with a 201 (Created) status code, typically used after resource creation.
    /// </summary>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <returns>A <see cref="ConnectorResponse"/> with a 201 (Created) status code.</returns>
    public static ConnectorResponse HttpCreated(string connectorName)
        => new(ReadOnlyMemory<byte>.Empty, "", connectorName, new(201));

    /// <summary>
    /// Creates a <see cref="ConnectorResponse"/> with a 202 (Accepted) status code.
    /// </summary>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <returns>A <see cref="ConnectorResponse"/> with a 202 (Accepted) status code.</returns>
    public static ConnectorResponse HttpAccepted(string connectorName)
        => new(ReadOnlyMemory<byte>.Empty, "", connectorName, new(202));

    /// <summary>
    /// Creates a <see cref="ConnectorResponse"/> with a 400 (Bad Request) status code and an optional error message.
    /// </summary>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <param name="detail">Optional. A message describing the reason for the bad request.</param>
    /// <returns>A <see cref="ConnectorResponse"/> with a 400 (Bad Request) status code and an optional detail message.</returns>
    public static ConnectorResponse HttpBadRequest(string connectorName, string? detail = null)
        => HttpProblem("Bad Request", connectorName, 400, detail);

    /// <summary>
    /// Creates a <see cref="ConnectorResponse"/> with a 502 (Bad Gateway) status code, typically used by proxy connectors that can not forward the request.
    /// </summary>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <param name="detail">Optional. A message describing the reason for the bad gateway error.</param>
    /// <returns>A <see cref="ConnectorResponse"/> with a 502 (Bad Gateway) status code and an optional detail message.</returns>
    public static ConnectorResponse HttpBadGateway(string connectorName, string? detail = null)
        => HttpProblem("Bad Gateway", connectorName, 502, detail);

    /// <summary>
    /// Creates a redirect <see cref="ConnectorResponse"/> with a specified location URL, connector name, and HTTP status code.
    /// </summary>
    /// <param name="locationUrl">The URL to which the client should be redirected.</param>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <param name="statusCode">
    /// The status code for the redirect response. Use 302 (Found) for temporary redirects or 301 (Moved Permanently) for permanent redirects. 
    /// Defaults to 302.
    /// </param>
    /// <returns>A <see cref="ConnectorResponse"/> with a redirect status code and a Location header.</returns>
    public static ConnectorResponse HttpRedirect(string locationUrl, string connectorName, int statusCode = 302)
    {
        // Ensure status code is a valid redirect code
        if (statusCode != 301 && statusCode != 302 && statusCode != 303 && statusCode != 307 && statusCode != 308)
        {
            throw new ArgumentOutOfRangeException(nameof(statusCode), "Invalid status code for HTTP redirect.");
        }

        var headers = new Dictionary<string, StringValues>
        {
            { "Location", locationUrl }
        }.AsReadOnly();

        return new(ReadOnlyMemory<byte>.Empty, "", connectorName, new(statusCode, headers));
    }

    /// <summary>
    /// Creates a problem detail <see cref="ConnectorResponse"/> in JSON format, with the specified error message, connector name, and HTTP status code.
    /// </summary>
    /// <param name="title">A short, human-readable summary of the problem.</param>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <param name="statusCode">The HTTP status code for the problem, defaults to 500 (Internal Server Error).</param>
    /// <param name="detail">Optional. A human-readable explanation specific to this occurrence of the problem.</param>
    /// <returns>A <see cref="ConnectorResponse"/> containing the problem details in JSON format, content type, connector name, and HTTP status code.</returns>
    public static ConnectorResponse HttpProblem(string title, string connectorName, int statusCode = 500, string? detail = null)
    {
        var problemDetails = new
        {
            type = $"https://httpstatuses.com/{statusCode}",
            title,
            status = statusCode,
            detail
        };

        return new(JsonSerializer.SerializeToUtf8Bytes(problemDetails), MediaTypeNames.Application.Json, connectorName, new(statusCode));
    }

    /// <summary>
    /// Creates a <see cref="ConnectorResponse"/> with the specified content, content type, connector name, and packet processing state.
    /// </summary>
    /// <param name="content">The response content.</param>
    /// <param name="contentType">The MIME type of the content (e.g., "application/json").</param>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <param name="state">The packet processing state for the packet transfer response.</param>
    /// <returns>A <see cref="ConnectorResponse"/> containing the specified content, content type, connector name, and packet processing state.</returns>
    public static ConnectorResponse PacketTransferContent(string content, string contentType, string connectorName, ProcessPacketState state = ProcessPacketState.Success)
        => new(Encoding.UTF8.GetBytes(content), contentType, connectorName, null, new(state));

    /// <summary>
    /// Creates a <see cref="ConnectorResponse"/> with the specified binary content, content type, connector name, and packet processing state.
    /// </summary>
    /// <param name="content">The binary content as a byte array.</param>
    /// <param name="contentType">The MIME type of the content (e.g., "application/octet-stream").</param>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <param name="state">The packet processing state for the packet transfer response, defaults to Success.</param>
    /// <returns>A <see cref="ConnectorResponse"/> containing the specified binary content, content type, and connector name.</returns>
    public static ConnectorResponse PacketTransferBinaryContent(ReadOnlyMemory<byte> content, string contentType, string connectorName, ProcessPacketState state = ProcessPacketState.Success)
        => new(content, contentType, connectorName, null, new(state));

    /// <summary>
    /// Creates a text <see cref="ConnectorResponse"/> with specified content, connector name, and packet processing state.
    /// </summary>
    /// <param name="content">The response content.</param>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <param name="state">The packet processing state for the packet transfer response.</param>
    /// <returns>A <see cref="ConnectorResponse"/> containing the specified text content, connector name, and packet processing state.</returns>
    public static ConnectorResponse PacketTransferText(string content, string connectorName, ProcessPacketState state = ProcessPacketState.Success)
        => new(Encoding.UTF8.GetBytes(content), MediaTypeNames.Text.Plain, connectorName, null, new(state));

    /// <summary>
    /// Creates a JSON <see cref="ConnectorResponse"/> with the specified object, connector name, and packet processing state.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize as JSON.</typeparam>
    /// <param name="data">The object to serialize and use as the response content.</param>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <param name="state">The packet processing state for the packet transfer response.</param>
    /// <returns>A <see cref="ConnectorResponse"/> containing the JSON-encoded content, content type, connector name, and the packet processing state.</returns>
    public static ConnectorResponse PacketTransferJson<T>(T data, string connectorName, ProcessPacketState state = ProcessPacketState.Success)
        => new(JsonSerializer.SerializeToUtf8Bytes(data), MediaTypeNames.Application.Json, connectorName, null, new(state));

    /// <summary>
    /// Creates a generic packet transfer <see cref="ConnectorResponse"/> with a specified packet processing state.
    /// </summary>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <param name="state">The packet processing state for the packet transfer response.</param>
    /// <returns>A <see cref="ConnectorResponse"/> containing the connector name and the specified packet processing state.</returns>
    public static ConnectorResponse PacketTransferState(string connectorName, ProcessPacketState state)
        => new(ReadOnlyMemory<byte>.Empty, "", connectorName, null, new(state));

    /// <summary>
    /// Creates a <see cref="ConnectorResponse"/> with a <see cref="ProcessPacketState.Success"/> processing state.
    /// </summary>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <returns>A <see cref="ConnectorResponse"/> indicating <see cref="ProcessPacketState.Success"/> processing state.</returns>
    public static ConnectorResponse PacketTransferSuccess(string connectorName)
        => new(ReadOnlyMemory<byte>.Empty, "", connectorName, null, new(ProcessPacketState.Success));

    /// <summary>
    /// Creates a <see cref="ConnectorResponse"/> with a <see cref="ProcessPacketState.FatalError"/> processing state, indicating that no further processing should occur.
    /// </summary>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <param name="detail">Optional. A message describing the fatal error.</param>
    /// <returns>A <see cref="ConnectorResponse"/> indicating <see cref="ProcessPacketState.FatalError"/> processing state.</returns>
    public static ConnectorResponse PacketTransferFatalError(string connectorName, string? detail = null)
        => new(Encoding.UTF8.GetBytes(detail ?? ""), MediaTypeNames.Text.Plain, connectorName, null, new(ProcessPacketState.FatalError));

    /// <summary>
    /// Creates a <see cref="ConnectorResponse"/> with the specified content, content type, connector name, HTTP status, and packet processing state.
    /// </summary>
    /// <param name="content">The response content.</param>
    /// <param name="contentType">The MIME type of the content (e.g., "application/json").</param>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <param name="statusCode">The status code for the response, defaults to 200 (OK).</param>
    /// <param name="state">The packet processing state for the packet transfer response, defaults to Success.</param>
    /// <returns>A <see cref="ConnectorResponse"/> containing the specified content, content type, connector name, HTTP status code, and packet processing state.</returns>
    public static ConnectorResponse HttpPacketTransferContent(string content, string contentType, string connectorName, int statusCode = 200, ProcessPacketState state = ProcessPacketState.Success)
        => new(Encoding.UTF8.GetBytes(content), contentType, connectorName, new(statusCode), new(state));

    /// <summary>
    /// Creates a <see cref="ConnectorResponse"/> with the specified binary content, content type, connector name, HTTP status code, and packet processing state.
    /// </summary>
    /// <param name="content">The binary content as a byte array.</param>
    /// <param name="contentType">The MIME type of the content (e.g., "application/octet-stream").</param>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <param name="statusCode">The status code for the response, defaults to 200 (OK).</param>
    /// <param name="state">The packet processing state for the packet transfer response, defaults to Success.</param>
    /// <returns>A <see cref="ConnectorResponse"/> containing the specified binary content, content type, and connector name.</returns>
    public static ConnectorResponse HttpPacketTransferBinaryContent(ReadOnlyMemory<byte> content, string contentType, string connectorName, int statusCode = 200, ProcessPacketState state = ProcessPacketState.Success)
        => new(content, contentType, connectorName, new(statusCode), new(state));

    /// <summary>
    /// Creates a text <see cref="ConnectorResponse"/> with specified content, connector name, HTTP status code, and packet processing state.
    /// </summary>
    /// <param name="content">The response content.</param>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <param name="statusCode">The status code for the response, defaults to 200 (OK).</param>
    /// <param name="state">The packet processing state for the packet transfer response, defaults to Success.</param>
    /// <returns>A <see cref="ConnectorResponse"/> containing the specified text content, connector name, HTTP status code, and packet processing state.</returns>
    public static ConnectorResponse HttpPacketTransferText(string content, string connectorName, int statusCode = 200, ProcessPacketState state = ProcessPacketState.Success)
        => new(Encoding.UTF8.GetBytes(content), MediaTypeNames.Text.Plain, connectorName, new(statusCode), new(state));

    /// <summary>
    /// Creates a JSON <see cref="ConnectorResponse"/> with the specified object, connector name, HTTP status, and packet processing state.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize as JSON.</typeparam>
    /// <param name="data">The object to serialize and use as the response content.</param>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <param name="statusCode">The status code for the response, defaults to 200 (OK).</param>
    /// <param name="state">The packet processing state for the packet transfer response, defaults to Success.</param>
    /// <returns>A <see cref="ConnectorResponse"/> containing the JSON-encoded content, content type, connector name, HTTP status code, and packet processing state.</returns>
    public static ConnectorResponse HttpPacketTransferJson<T>(T data, string connectorName, int statusCode = 200, ProcessPacketState state = ProcessPacketState.Success)
        => new(JsonSerializer.SerializeToUtf8Bytes(data), MediaTypeNames.Application.Json, connectorName, new(statusCode), new(state));

    /// <summary>
    /// Creates a <see cref="ConnectorResponse"/> with the specified connector name, HTTP status and packet processing state, but no content.
    /// </summary>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <param name="statusCode">The HTTP status code for the response (e.g., 200, 202).</param>
    /// <param name="state">The packet processing state for the packet transfer response (e.g., Success, Retry).</param>
    /// <returns>A <see cref="ConnectorResponse"/> containing the specified HTTP status code and packet processing state, with no content.</returns>
    public static ConnectorResponse HttpPacketTransferState(string connectorName, int statusCode, ProcessPacketState state)
        => new(ReadOnlyMemory<byte>.Empty, "", connectorName, new(statusCode), new(state));

    /// <summary>
    /// Creates a problem detail <see cref="ConnectorResponse"/> in JSON format, with specified HTTP status and packet processing state.
    /// </summary>
    /// <param name="title">A short, human-readable summary of the problem.</param>
    /// <param name="connectorName">The name of the connector providing this response.</param>
    /// <param name="statusCode">The HTTP status code for the problem, defaults to 500 (Internal Server Error).</param>
    /// <param name="state">The packet processing state for the packet transfer response, defaults to FatalError.</param>
    /// <param name="detail">Optional. A message describing the specific occurrence of the problem.</param>
    /// <returns>A <see cref="ConnectorResponse"/> containing the problem details in JSON format, HTTP status code, and packet processing state.</returns>
    public static ConnectorResponse HttpPacketTransferProblem(string title, string connectorName, int statusCode = 500, ProcessPacketState state = ProcessPacketState.FatalError, string? detail = null)
    {
        var problemDetails = new
        {
            type = $"https://httpstatuses.com/{statusCode}",
            title,
            status = statusCode,
            detail
        };

        return new(JsonSerializer.SerializeToUtf8Bytes(problemDetails), MediaTypeNames.Application.Json, connectorName, new(statusCode), new(state));
    }
}
