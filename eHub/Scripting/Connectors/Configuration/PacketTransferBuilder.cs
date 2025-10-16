using eHub.Config;
using eHub.PlugIn;
using eHub.PlugIn.Configuration;

namespace eHub.Scripting.Connectors.Configuration;

public class PacketTransferBuilder : IPacketTransferBuilder
{
    private readonly PacketTransfer _packetTransfer = new();

    public IPacketTransferBuilder AddChannelGroup(string name, Action<IChannelGroupBuilder> configure)
    {
        var channelGroupBuilder = new ChannelGroupBuilder();
        configure(channelGroupBuilder);
        _packetTransfer.ChannelGroups.Add(name, channelGroupBuilder.Build());

        return this;
    }

    public IPacketTransferBuilder SetDbPath(string path)
    {
        _packetTransfer.DbPath = path;
        return this;
    }

    public IPacketTransferBuilder AddSqlitePragma(string pragma, string value)
    {
        _packetTransfer.SqlitePragmas[pragma] = value;
        return this;
    }

    public PacketTransfer Build() => _packetTransfer;
}

public class ChannelGroupBuilder : IChannelGroupBuilder
{
    private readonly ChannelGroup _channelGroup = new();

    public IChannelGroupBuilder AddChannel(string channel)
    {
        _channelGroup.Channels.Add(channel);
        return this;
    }

    public IChannelGroupBuilder AddPacketRetentionRule(PacketStatus status, TimeSpan? retention)
    {
        var value = retention is null ? "Keep" : retention.Value.ToString();
        _channelGroup.PacketRetention.Add(status.ToString(), value);

        return this;
    }

    public IChannelGroupBuilder DisableResend()
    {
        _channelGroup.CanResend = false;
        return this;
    }

    public IChannelGroupBuilder SetChannels(IEnumerable<string> channels)
    {
        _channelGroup.Channels = channels.ToHashSet();
        return this;
    }

    public IChannelGroupBuilder SetCleanerInterval(TimeSpan interval)
    {
        _channelGroup.CleanerInterval = interval;
        return this;
    }

    public IChannelGroupBuilder SetDbPollInterval(TimeSpan interval)
    {
        _channelGroup.DbPollInterval = interval;
        return this;
    }

    public IChannelGroupBuilder SetDefaultPacketRetention(TimeSpan? retention)
    {
        _channelGroup.PacketRetention["Default"] = retention is null ? "Keep" : retention.Value.ToString();
        return this;
    }

    public IChannelGroupBuilder SetPacketsPerCycle(int count)
    {
        _channelGroup.PacketsPerCycle = count;
        return this;
    }

    public IChannelGroupBuilder UseConcurrentProcessing()
    {
        _channelGroup.Mode = ChannelMode.Concurrent;
        return this;
    }

    public IChannelGroupBuilder UseSequentialProcessing()
    {
        _channelGroup.Mode = ChannelMode.Sequential;
        return this;
    }

    public ChannelGroup Build() => _channelGroup;
}
