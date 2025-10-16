using System.Collections.Frozen;
using Microsoft.Net.Http.Headers;

namespace eHub.PlugIn.Communication;

/// <summary>
/// Contains constants and predefined collections for HTTP headers used in eHub's HTTP connectors.
/// </summary>
public static class ConnectorHeaders
{
    /// <summary>
    /// HTTP header for identifying a specific packet to be re-requested by eHub.
    /// </summary>
    public const string PacketIdHeader = "X-eHub-PacketId";

    /// <summary>
    /// HTTP header for signing requests made by eHub towards itself.
    /// </summary>
    public const string SignatureHeader = "X-eHub-Signature";

    /// <summary>
    /// HTTP response header for indicating the state of the packet processing.
    /// </summary>
    public const string ProcessPacketStateHeader = "X-eHub-ProcessPacketState";

    /// <summary>
    /// A set of HTTP headers considered sensitive, which should typically be excluded from logging or storage to protect sensitive information.
    /// </summary>
    /// <remarks>
    /// This collection includes headers related to authentication, authorization, cookies, server information, user tracking, 
    /// and other headers that may expose personally identifiable information or security details.
    /// </remarks>
    public static readonly FrozenSet<string> SensitiveHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        HeaderNames.Authorization,
        HeaderNames.ProxyAuthorization,
        HeaderNames.Cookie,
        HeaderNames.SetCookie,
        HeaderNames.ProxyAuthenticate,
        HeaderNames.WWWAuthenticate,
        HeaderNames.XPoweredBy,
        HeaderNames.Server,
        "X-Real-IP",
        "X-Forwarded-For",
        "X-Client-IP",
        PacketIdHeader,
        SignatureHeader
    }.ToFrozenSet();

    /// <summary>
    /// A collection of HTTP headers specifically intended for use with <see cref="HttpContent"/> objects, 
    /// such as <see cref="ReadOnlyMemoryContent"/>. These headers control aspects of the HTTP message content, 
    /// including MIME type, encoding, language, and content length.
    /// </summary>
    public static readonly FrozenSet<string> ContentHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        HeaderNames.ContentType,
        HeaderNames.ContentLength,
        HeaderNames.ContentEncoding,
        HeaderNames.ContentLanguage,
        HeaderNames.ContentLocation,
        HeaderNames.Allow,
        HeaderNames.Expires,
        HeaderNames.LastModified,
        HeaderNames.ContentDisposition,
        HeaderNames.ContentMD5,
        HeaderNames.ContentRange
    }.ToFrozenSet();
}

