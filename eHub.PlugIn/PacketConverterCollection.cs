using System.Text;
using eHub.PlugIn.UI;

namespace eHub.PlugIn;

/// <summary>
/// Simple to string converter
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="StringConverter"/> class with the specified encoding and display type.
/// </remarks>
/// <param name="encoding">The encoding to be used for converting strings to bytes and vice versa.</param>
/// <param name="displayType">The display type of the converter.</param>
public class StringConverter(Encoding encoding, string displayType) : IPacketConverter
{
    /// <summary>Converter for generic UTF-8 encoded text.</summary>
    public static StringConverter Utf8Text { get; } = new(Encoding.UTF8);
    /// <summary>Converter for generic UTF-8 encoded json.</summary>
    public static StringConverter Utf8Json { get; } = new(Encoding.UTF8, UIDataTypes.Json);
    /// <summary>Converter for generic ASCII encoded text.</summary>
    public static StringConverter AsciiText { get; } = new(Encoding.ASCII);

    /// <summary>Gets the encoding used for converting strings to bytes and vice versa.</summary>
    public Encoding Encoding { get; } = encoding;

    /// <summary>Gets the UI display type of the converter. Refer to <see cref="UIDataTypes"/> for well-known types."/></summary>
    public string DisplayType { get; } = displayType;

    /// <summary>Gets or sets the maximum length of the preview text. Longer text will be truncated.</summary>
    public int? MaxPreviewLength { get; init; } = 20;

    /// <summary>Initializes a new instance of the <see cref="StringConverter"/> class with the default UTF-8 encoding.</summary>
    public StringConverter() : this(Encoding.UTF8) { }

    /// <summary>Initializes a new instance of the <see cref="StringConverter"/> class with the specified encoding.</summary>
    /// <param name="encoding">The encoding to be used for converting strings to bytes and vice versa.</param>
    public StringConverter(Encoding encoding) : this(encoding, UIDataTypes.Plaintext) { }

    /// <inheritdoc/>
    ValueTask<ReadOnlyMemory<byte>> IPacketConverter.RawToBinaryDataConverter(ReadOnlyMemory<byte> data, string? metadata, CancellationToken cancellationToken)
        => ValueTask.FromResult(data);

    /// <inheritdoc/>
    ValueTask<ReadOnlyMemory<byte>> IPacketConverter.PacketToRawDataConverter(PacketData packet, CancellationToken cancellationToken)
        => ValueTask.FromResult(packet.BinaryData);

    /// <inheritdoc/>
    ValueTask<UIConversionInfo> IPacketConverter.PacketToUIDataConverter(PacketData packet, CancellationToken cancellationToken)
    {
        var text = Encoding.GetString(packet.BinaryData.Span);
        var preview = text;
        if (MaxPreviewLength is { } maxLength && preview.Length > maxLength)
        {
            preview = preview.Substring(0, maxLength);
        }

        return ValueTask.FromResult(new UIConversionInfo(text, preview, DisplayType));
    }

    /// <inheritdoc/>
    ValueTask<ReadOnlyMemory<byte>> IPacketConverter.UIToBinaryDataConverter(UIConversionInfo data, string? metadata, CancellationToken cancellationToken)
        => ValueTask.FromResult<ReadOnlyMemory<byte>>(Encoding.GetBytes(data.Data));
}
