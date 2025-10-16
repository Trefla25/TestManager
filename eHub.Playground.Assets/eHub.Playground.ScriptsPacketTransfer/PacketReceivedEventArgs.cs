using System;

namespace eHub.Playground.ScriptsPacketTransfer;
public class PacketReceivedEventArgs : EventArgs
{
    public ReadOnlyMemory<byte> Binary { get; set; }
    public string Channel { get; set; }
}
