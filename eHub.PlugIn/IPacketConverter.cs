using System.Text;
using eHub.PlugIn.UI;

namespace eHub.PlugIn;
/// <summary>
/// Converter interface for converting packet transfer data between the incoming, database, and UI-friendly formats.
/// </summary>
public interface IPacketConverter
{
    /// <summary>Gets the binary data stored in the packet transfer database from the incoming raw data.</summary>
    /// <param name="data">The incoming data received by the connector.</param>
    /// <param name="metadata">The metadata used to create the binary data.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The binary data of the packet stored.</returns>
    public ValueTask<ReadOnlyMemory<byte>> RawToBinaryDataConverter(ReadOnlyMemory<byte> data, string? metadata, CancellationToken cancellationToken = default)
        #pragma warning disable CS0618
        => RawToBinaryDataConverter(data, cancellationToken);
        #pragma warning restore CS0618

    /// <summary>Gets the raw data from the packet's binary data.</summary>
    /// <param name="packet">The packet used for the conversion.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The raw data of the packet.</returns>
    public ValueTask<ReadOnlyMemory<byte>> PacketToRawDataConverter(PacketData packet, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(packet.BinaryData);

    /// <summary>Gets the converted packet information for the UI.</summary>
    /// <param name="packet">The packet used for the conversion.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The converted packet information for the UI.</returns>
    public async ValueTask<UIConversionInfo> PacketToUIDataConverter(PacketData packet, CancellationToken cancellationToken = default)
    {
        #pragma warning disable CS0618 // For backwards compatibility
        var data = await BytesToUIDataConverter(packet.BinaryData);
        #pragma warning restore CS0618 // For backwards compatibility

        var dataPreview = data.Length < 20 ? data : data.Substring(0, 20);
        var dataType = UIDataTypes.Plaintext;

        return new UIConversionInfo(data, dataPreview, dataType);
    }

    /// <summary>Gets the binary data stored in the packet transfer database from the UI data.</summary>
    /// <param name="data">The UI data used for conversion.</param>
    /// <param name="metadata">The metadata used for conversion.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The binary data of the packet stored.</returns>
    public ValueTask<ReadOnlyMemory<byte>> UIToBinaryDataConverter(UIConversionInfo data, string? metadata, CancellationToken cancellationToken = default)
        #pragma warning disable CS0618
        => UIToBinaryDataConverter(data, cancellationToken);
        #pragma warning restore CS0618

    /// <summary>Gets the binary data of a packet as a string representing the UI-friendly data to be displayed.</summary>
    /// <param name="bytes">The binary data of the packet.</param>
    /// <returns>The packet data displayed in the UI as a string.</returns>
    [Obsolete("Use PacketToUIDataConverter instead")]
    public ValueTask<string> BytesToUIDataConverter(ReadOnlyMemory<byte> bytes)
        => ValueTask.FromResult(Encoding.UTF8.GetString(bytes.Span));

    /// <summary>Gets the binary data stored in the packet transfer database from the incoming raw data.</summary>
    /// <param name="data">The incoming data received by the connector.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The binary data of the packet stored.</returns>
    [Obsolete("Use with metadata instead")]
    public ValueTask<ReadOnlyMemory<byte>> RawToBinaryDataConverter(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(data);

    /// <summary>Gets the binary data stored in the packet transfer database from the UI data.</summary>
    /// <param name="data">The UI data used for conversion.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The binary data of the packet stored.</returns>
    [Obsolete("Use with metadata instead")]
    public ValueTask<ReadOnlyMemory<byte>> UIToBinaryDataConverter(UIConversionInfo data, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<ReadOnlyMemory<byte>>(Encoding.UTF8.GetBytes(data.Data).AsMemory());
}

/// <summary>Used for conversion between packet database binary data and UI data.</summary>
public struct UIConversionInfo
{
    /// <summary>The displayed data in the UI.</summary>
    public string Data { get; set; }
    /// <summary>The preview of the data displayed in the UI.</summary>
    public string DataPreview { get; set; }
    /// <summary>The data type.</summary>
    public string DataType { get; set; }

    /// <summary></summary>
    /// <param name="data">The displayed data in the UI.</param>
    /// <param name="dataPreview">The preview of the data displayed in the UI.</param>
    /// <param name="dataType">The data type.</param>
    public UIConversionInfo(string data, string dataPreview, string dataType)
    {
        Data = data;
        DataPreview = dataPreview;
        DataType = dataType;
    }
}
