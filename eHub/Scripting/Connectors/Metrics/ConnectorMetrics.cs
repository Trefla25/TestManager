using System.Diagnostics.Metrics;
using eHub.PlugIn;

namespace eHub.Scripting.Connectors.Metrics;

public class ConnectorMetrics : IDisposable
{
    private static readonly InstrumentAdvice<double> DefaultBuckets = new()
    {
        HistogramBucketBoundaries =
        [
            .. Enumerable.Range(-3, 100)
                .SelectMany(n => new[] { 0.25, 0.5, 0.75, 1 }
                    .Select(f => Math.Round(f * Math.Pow(10, n), 4)))
                .TakeWhile(n => n < 10)
        ]
    };

    private readonly Meter _meter;
    private readonly Counter<long> _packetsInsertedTotal;
    private readonly Histogram<double> _processCallTime;
    private readonly Histogram<double> _runCleanupTime;

    public ConnectorMetrics(IMeterFactory meterFactory, ConnectorMetadata metadata)
    {
        _meter = meterFactory.Create("eHub", null, [new("connector", metadata.TemplateName)]);
        _packetsInsertedTotal = _meter.CreateCounter<long>("ehub_packets_inserted_total",
            description: "Total number of packets inserted into the connector");
        _processCallTime = _meter.CreateHistogram<double>("ehub_process_packet_time", "seconds",
            "Distribution of processing time for packets of a channel and result", advice: DefaultBuckets);
        _runCleanupTime = _meter.CreateHistogram<double>("ehub_cleanup_time", "seconds",
            "Distribution of cleanup time for a channel group", advice: DefaultBuckets);
    }

    public void TrackPacketCreated(string channel, PacketStatus status, int count = 1)
    {
        _packetsInsertedTotal.Add(count, [
            new("channel", channel),
            new("status", status)
        ]);
    }

    public void TrackProcessPacketCall(string channel, ProcessPacketState processResult, TimeSpan time)
    {
        _processCallTime.Record(time.TotalSeconds, [
            new("channel", channel),
            new("processResult", processResult)
        ]);
    }

    public void TrackCleanupCycle(string channelGroup, TimeSpan time)
    {
        _runCleanupTime.Record(time.TotalSeconds, [
            new("channelGroup", channelGroup)
        ]);
    }

    public void Dispose() => _meter.Dispose();
}
