using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Http;

namespace eHub.PlugIn.Communication;

/// <summary>
/// Provides extension methods for <see cref="ConnectorRequest"/>.
/// </summary>
public static class ConnectorRequestExtensions
{
    /// <summary>
    /// Converts a <see cref="ConnectorRequest"/> to a legacy <see cref="HttpConnectorRequestDto"/> instance for backward compatibility.
    /// </summary>
    /// <param name="request">The <see cref="ConnectorRequest"/> containing the content, path, and content type.</param>
    /// <returns>An <see cref="HttpConnectorRequestDto"/> constructed from the specified <see cref="ConnectorRequest"/>.</returns>
    [Obsolete("This method should only be used for backward compatibility. Use the updated ConnectorRequest structure for new implementations.")]
    public static HttpConnectorRequestDto ToHttpConnectorRequestDto(this ConnectorRequest request)
        => new(request.Content, request.Http?.Path, request.ContentType);
}

/// <summary>
/// Provides extension methods for <see cref="ConnectorResponse"/>.
/// </summary>
public static class ConnectorResponseExtensions
{
    /// <summary>
    /// Converts a <see cref="ConnectorResponse"/> to an <see cref="IResult"/> for HTTP response streaming.
    /// </summary>
    /// <param name="response">The <see cref="ConnectorResponse"/> to convert.</param>
    /// <returns>An <see cref="IResult"/> that streams the content of the <see cref="ConnectorResponse"/>.</returns>
    public static IResult ToIResult(this ConnectorResponse response) => new ConnectorResponseStreamResult(response);

    /// <summary>
    /// Converts a <see cref="ConnectorResponse"/> to a legacy <see cref="HttpConnectorResponseDto"/> instance for backward compatibility.
    /// </summary>
    /// <param name="response">The <see cref="ConnectorResponse"/> containing content, content type, and HTTP status code.</param>
    /// <returns>A new <see cref="HttpConnectorResponseDto"/> constructed from the specified <see cref="ConnectorResponse"/>.</returns>
    [Obsolete("This method should only be used for backward compatibility. Use the updated ConnectorResponse structure for new implementations.")]
    public static HttpConnectorResponseDto ToHttpConnectorResponseDto(this ConnectorResponse response)
        => new(response.Content, response.ContentType, response.Http?.StatusCode ?? 200);
}

/// <summary>
/// Represents an <see cref="IResult"/> implementation that streams the content of a <see cref="ConnectorResponse"/> to the HTTP response.
/// </summary>
internal class ConnectorResponseStreamResult(ConnectorResponse connectorResponse) : IResult
{
    private readonly ConnectorResponse _connectorResponse = connectorResponse;

    public async Task ExecuteAsync(HttpContext context)
    {
        context.Response.ContentType = _connectorResponse.ContentType;
        context.Response.ContentLength = _connectorResponse.Content.Length;

        if (_connectorResponse.Http?.StatusCode is { } statusCode)
        {
            context.Response.StatusCode = statusCode;
        }

        if (_connectorResponse.Http?.Headers is { } headers && headers.Count > 0)
        {
            foreach (var header in headers.SelectMany(header => header.Value.Where(value => !string.IsNullOrEmpty(value)).Select(value => (header.Key, value))))
            {
                if (header.Key is "Content-Type" or "Content-Length")
                {
                    continue;
                }

                context.Response.Headers.Append(header.Key, header.value);
            }
        }

        if (_connectorResponse.PacketTransfer is { } packetTransfer)
        {
            context.Response.Headers.Append(ConnectorHeaders.ProcessPacketStateHeader, packetTransfer.ProcessPacketState.ToString());
        }

        await context.Response.Body.WriteAsync(_connectorResponse.Content);
    }
}
