using eHub.PlugIn;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace eHub.Playground.Scripts2;
public class PacketTransferBogus : IPacketTransfer
{
    private readonly ILogger<PacketTransferBogus> _logger;
    private readonly IOptionsMonitor<Config> _config;
    private readonly PeriodicTimer _timerTemp = new(TimeSpan.FromSeconds(1));
    private readonly PeriodicTimer _timerWind = new(TimeSpan.FromSeconds(3));
    private readonly PeriodicTimer _timerUpdates = new(TimeSpan.FromSeconds(5));

    public IPacketConverter Converter { get; set; } = StringConverter.Utf8Text;
    public string? PacketsGetTopic { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public IPacketRepository PacketRepository { get; set; } = default!;

    public event UpdateStatusDelegate? UpdateStatus;

    public PacketTransferBogus(ILogger<PacketTransferBogus> logger, IOptionsMonitor<Config> config)
    {
        _logger = logger;
        _config = config;
    }

    public async ValueTask<ProcessPacketState> ProcessPacket(PacketData packet, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Process [{channel}] {Data}", packet.Channel, Encoding.ASCII.GetString(packet.BinaryData.Span));
        if (packet.Channel == "Wind")
        {
            // Wind is hard to calculate
            await Task.Delay(100, cancellationToken);
        }
        return ProcessPacketState.Success;
    }

    public async Task Run(CancellationToken cancellationToken) =>
        await Task.WhenAll(
            RunTemp(cancellationToken),
            RunWind(cancellationToken),
            RunUpdateTest(cancellationToken));

    public async Task RunTemp(CancellationToken cancellationToken)
    {
        while (await _timerTemp.WaitForNextTickAsync(cancellationToken))
        {
            if (!_config.CurrentValue.PushTemp)
            {
                continue;
            }

            var data = Encoding.ASCII.GetBytes($"Temperature: {(15 + Random.Shared.NextDouble() * 20):0.00}C");

            await PacketRepository.AddAsync(new PacketData(data, "Temp", PacketStatus.Enqueued), cancellationToken);
        }
    }

    public async Task RunWind(CancellationToken cancellationToken)
    {
        while (await _timerWind.WaitForNextTickAsync(cancellationToken))
        {
            if (!_config.CurrentValue.PushWind)
            {
                continue;
            }

            var data = Encoding.ASCII.GetBytes($"Wind strength: {(0 + Random.Shared.NextDouble() * 30):0.0} m/s");

            await PacketRepository.AddAsync(new PacketData(data, "Wind", PacketStatus.Enqueued), cancellationToken);
        }
    }

    public async Task RunUpdateTest(CancellationToken cancellationToken)
    {
        while (await _timerUpdates.WaitForNextTickAsync(cancellationToken))
        {
            var updatedRows = await PacketRepository.Update()
                .Set(x => x.RetryCount, 0)
                .Set(x => x.Channel, "lul")
                .Where(x => x.Status == PacketStatus.Processed)
                .ExecuteAsync(cancellationToken);

            // UpdatePackets!(
            //     new IPacketUpdateBuilder()
            //         .Where(x => x.Status == PacketStatus.Processed)
            //         .Set(x => x.RetryCount, 0)
            //         .Set(x => x.Channel, x => "lul" + x.Id));
        }
    }

    [ScriptOptions]
    public class Config
    {
        public bool PushTemp { get; set; } = false;
        public bool PushWind { get; set; } = false;
    }
}
