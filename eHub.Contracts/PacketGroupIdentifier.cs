using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;

namespace eHub.Contracts;

[JsonConverter(typeof(PacketGroupIdentifierConverter))]
public readonly record struct PacketGroupIdentifier(string ConnectorName, string ChannelName)
{
    public override string ToString() => $"{ConnectorName}:{ChannelName}";
}

public sealed class PacketGroupIdentifierConverter : JsonConverter<PacketGroupIdentifier>
{
    public override PacketGroupIdentifier Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
    {
        var value = reader.GetString();
        var parts = value?.Split(':', 2);
        if (parts != null && parts.Length == 2)
        {
            return new PacketGroupIdentifier(parts[0], parts[1]);
        }
        throw new JsonException("Invalid format for PacketGroupIdentifier");
    }

    public override void Write(
        Utf8JsonWriter writer,
        PacketGroupIdentifier packetGroupIdentifier,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(packetGroupIdentifier.ToString());
    }

    public override void WriteAsPropertyName(
        Utf8JsonWriter writer,
        PacketGroupIdentifier packetGroupIdentifier,
        JsonSerializerOptions options)
    {
        writer.WritePropertyName(packetGroupIdentifier.ToString());
    }

    public override PacketGroupIdentifier ReadAsPropertyName(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return Read(ref reader, typeToConvert, options);
    }
}
