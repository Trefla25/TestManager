using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;
using eHub.PlugIn.Communication;
using Microsoft.AspNetCore.Http;

namespace eHub.PlugIn;
/// <summary>
/// 
/// </summary>
/// <param name="Content"></param>
/// <param name="Path"></param>
/// <param name="ContentType"></param>
[Obsolete("HttpConnectorRequestDto is obsolete. Consider using the new ConnectorRequest class.")]
public record HttpConnectorRequestDto(
    ReadOnlyMemory<byte> Content,
    [StringSyntax("Route")] string? Path,
    string? ContentType = null);

/// <summary>
/// 
/// </summary>
/// <param name="Content"></param>
/// <param name="ContentType"></param>
/// <param name="StatusCode"></param>
[Obsolete("HttpConnectorResponseDto is obsolete. Consider using the new ConnectorResponse class.")]
public record HttpConnectorResponseDto(
    ReadOnlyMemory<byte> Content,
    string? ContentType,
    int StatusCode = StatusCodes.Status200OK);

/// <summary>
/// 
/// </summary>
[Obsolete("HttpConnectorDto classes are obsolete. Consider using the new connector request/response classes.")]
public static class HttpConnectorDtoExtension
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="response"></param>
    /// <returns></returns>
    [Obsolete("HttpConnectorDto classes are obsolete. Consider using the new connector request/response classes.")]
    public static IResult GetResult(this HttpConnectorResponseDto response)
        => TypedResults.Text(response.Content.Span, response.ContentType, response.StatusCode);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="response"></param>
    /// <returns></returns>
    [Obsolete("HttpConnectorDto classes are obsolete. Consider using the new connector request/response classes.")]
    public static bool IsSuccessStatusCode(this HttpConnectorResponseDto response)
        => response.StatusCode >= 200 && response.StatusCode < 300;

    /// <summary>
    /// Converts a legacy <see cref="HttpConnectorRequestDto"/> to a <see cref="ConnectorRequest"/> instance for backward compatibility.
    /// </summary>
    /// <param name="request">The legacy HTTP connector request DTO containing the request content, path, and content type.</param>
    /// <returns>A <see cref="ConnectorRequest"/> instance constructed from the specified <see cref="HttpConnectorRequestDto"/> for use in modern workflows.</returns>
    [Obsolete("HttpConnectorDto classes are obsolete. Consider using the new connector request/response classes.")]
    public static ConnectorRequest ToConnectorRequest(this HttpConnectorRequestDto request)
    {
        var http = request.Path is { } ? new ConnectorRequest.HttpConnectorRequest(request.Path, "GET") : null;

        return new(request.Content, request.ContentType ?? MediaTypeNames.Application.Octet, "", http);
    }

    /// <summary>
    /// Converts a legacy <see cref="HttpConnectorResponseDto"/> to a <see cref="ConnectorResponse"/> instance for backward compatibility.
    /// </summary>
    /// <param name="response">The legacy HTTP connector response DTO containing the response content, content type and status code.</param>
    /// <returns>A <see cref="ConnectorResponse"/> instance constructed from the specified <see cref="HttpConnectorResponseDto"/> for use in modern workflows.</returns>
    [Obsolete("HttpConnectorDto classes are obsolete. Consider using the new connector request/response classes.")]
    public static ConnectorResponse ToConnectorResponse(this HttpConnectorResponseDto response)
        => new(response.Content, response.ContentType ?? MediaTypeNames.Application.Octet, "", new ConnectorResponse.HttpConnectorResponse(response.StatusCode));
}
