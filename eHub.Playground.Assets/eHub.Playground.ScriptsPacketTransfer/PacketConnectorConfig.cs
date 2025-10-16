using System;
using eHub.PlugIn;

namespace eHub.Playground.ScriptsPacketTransfer;

[ScriptOptions]
public class PacketConnectorConfig
{
    public required string ConnectorName { get; init; }
    public required string PairedConnectorName { get; init; }

    public required string IpAddress { get; init; }
    public int SendPort { get; init; }
    public int RecvPort { get; init; }
    public TimeSpan StatusInterval => TimeSpan.FromSeconds(StatusIntervalSeconds);
    public int StatusIntervalSeconds { get; init; }
    public bool AliveTimeout { get; init; }
    public int ReceiveTimout { get; set; }
    public bool IgnoreLowerTelId { get; set; }
    public int MaxRepeatCount { get; set; }
}
