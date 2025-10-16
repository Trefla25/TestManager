using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using eHub.PlugIn;
using eHub.PlugIn.UI;

namespace eHub.Playground.ScriptsPacketTransfer;

public class Converter : IPacketConverter
{
    public ValueTask<ReadOnlyMemory<byte>> RawToBinaryDataConverter(ReadOnlyMemory<byte> data, string? metadata, CancellationToken cancellationToken = default)
    {
        var dataString = Encoding.UTF8.GetString(data.Span);

        if (metadata is not null)
        {
            var concatenatedData = dataString + "/" + metadata;

            return new ValueTask<ReadOnlyMemory<byte>>(Encoding.UTF8.GetBytes(concatenatedData).AsMemory());
        }

        return new ValueTask<ReadOnlyMemory<byte>>(Encoding.UTF8.GetBytes(dataString).AsMemory());

    }

    public ValueTask<UIConversionInfo> PacketToUIDataConverter(PacketData packet, CancellationToken cancellation = default)
    {
        var dataString = Encoding.UTF8.GetString(packet.BinaryData.Span);

        return ValueTask.FromResult(new UIConversionInfo(dataString, dataString, UIDataTypes.Plaintext));
    }
  
    public ValueTask<ReadOnlyMemory<byte>> UIToBinaryDataConverter(UIConversionInfo data, string? metadata, CancellationToken cancellationToken = default)
    {

        if (metadata is not null)
        {
            var dbData = data.Data + "/" + metadata;

            return new ValueTask<ReadOnlyMemory<byte>>(Encoding.UTF8.GetBytes(dbData).AsMemory());
        }
        
        return new ValueTask<ReadOnlyMemory<byte>>(Encoding.UTF8.GetBytes(data.Data).AsMemory());
    }

}
