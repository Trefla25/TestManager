using System.Net.Mime;
using System.Text;

namespace eHub.PlugIn.Communication;

/// <summary>
/// Provides utility methods for handling encoding-related operations.
/// </summary>
public static class EncodingUtil
{
    /// <summary>
    /// Trims the Byte Order Mark (BOM) from the beginning of the byte sequence if it exists, based on the specified content type.
    /// </summary>
    /// <param name="bytes">The byte sequence to process.</param>
    /// <param name="contentType">
    /// The MIME type containing the character set used to determine the encoding (e.g., "application/json; charset=utf-8").
    /// If <c>null</c>, UTF-8 is assumed.
    /// </param>
    /// <returns>A <see cref="ReadOnlyMemory{T}"/> of <see cref="byte"/> representing the byte sequence with the BOM removed if it was present.</returns>
    public static ReadOnlyMemory<byte> TrimBom(ReadOnlyMemory<byte> bytes, string? contentType)
    {
        var encoding = TryGetEncoding(contentType) ?? Encoding.UTF8;
        var preamble = encoding.GetPreamble();

        if (bytes.Span.StartsWith(preamble))
        {
            return bytes.Slice(preamble.Length);
        }

        return bytes;
    }

    private static Encoding? TryGetEncoding(string? contentType)
    {
        if (contentType is null)
        {
            return null;
        }

        try
        {
            var charset = new ContentType(contentType).CharSet;
            if (charset is null)
            {
                return null;
            }

            return Encoding.GetEncoding(charset);
        }
        catch
        {
            return null;
        }
    }
}
