using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using eHub.PlugIn;
using eHub.PlugIn.UI;
using eController.ProjectToolbox.Tcp.Endpoints;
using eMessenger;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


namespace eHub.Playground.ScriptsPacketTransfer;

public class PacketConnector : IConnector, IPacketTransfer, IDisposable
{
    private readonly PacketConnectorConfig _options;
    private readonly ILogger<PacketConnector> _logger;
    private readonly PacketConnectorReceiver _packetConnectorReceiver;
    private readonly PacketConnectorSender _packetConnectorSender;
    private readonly IScopedMessenger _messenger;
    private readonly ITcpEndpointFactory _tcpEndpointFactory;

    public IPacketConverter Converter { get; set; }
    public IPacketRepository PacketRepository { get; set; } = default!;

    public event UpdateStatusDelegate? UpdateStatus;

    public PacketConnector(IOptions<PacketConnectorConfig> options, ILogger<PacketConnector> logger, IConfiguration appConfig, ILoggerFactory loggerFactory, IScopedMessenger messenger)
    {
        _tcpEndpointFactory = new TcpEndpointFactory(loggerFactory);

        Converter = new Converter();

        _packetConnectorSender = new PacketConnectorSender(options.Value, _tcpEndpointFactory, loggerFactory.CreateLogger($"{nameof(PacketConnector)}.Sender"));
        _packetConnectorSender.PacketReceived += OnPacketReceived; ;

        _packetConnectorReceiver = new PacketConnectorReceiver(options.Value, _tcpEndpointFactory, loggerFactory.CreateLogger($"{nameof(PacketConnector)}.Receiver"));
        _packetConnectorReceiver.PacketReceived += OnPacketReceived;

        _options = options.Value;
        _logger = logger;
        _messenger = messenger;
    }

    /// <summary>
    /// Starts the Connector
    /// </summary>
    public async Task Run(CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.WhenAll(
                    _packetConnectorReceiver.StartReceiver(cancellationToken),
                    _packetConnectorSender.StartSender(cancellationToken)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connector communications crashed");
            }
        }, cancellationToken);

        await ConnectorStartListen();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
            catch (Exception ex)
            {
                UpdateStatus?.Invoke("status", "disconnected.error");
                _logger.LogWarning(ex, "ConnectSocket failed");
            }
        }

        UpdateStatus?.Invoke("status", "disconnecting");
        _packetConnectorSender.Dispose();
        _packetConnectorReceiver.Dispose();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="binaryData"></param>
    /// <param name="channel"></param>
    /// <returns></returns>
    public async ValueTask<ProcessPacketState> ProcessPacket(PacketData packet, CancellationToken cancellationToken)
    {
        bool ret = true;

        // packet received from TCP/IP --> send packet to paired device
        if (packet.Channel == $"{_options.ConnectorName}To{_options.PairedConnectorName}_Data")
        {
            PacketStatus ackStatus = PacketStatus.Processed;
            string ack = "OK";
            try
            {
                // create data for paired device
                var packetData = new PacketMessageData(packet.BinaryData, $"{_options.ConnectorName}To{_options.PairedConnectorName}_Data");

                var resp = await _messenger.AskAsync<bool>(string.Format(PacketConnectorConst.TopicConnectorAliveCheck, _options.PairedConnectorName.ToLower()));

                if (!resp.Any())
                {
                    return ProcessPacketState.Retry;
                }

                // send data to paired device
                await _messenger.SendAsync(
                        string.Format(PacketConnectorConst.TopicConnectorPacketSend, _options.PairedConnectorName.ToLower()),
                        packetData);
            }
            catch (Exception ex)
            {
                ack = "NOK";
                _logger.LogError(ex, "ProcessPacket");
            }

            var ackBytes = Encoding.ASCII.GetBytes(ack);

            // send ack
            if (!await _packetConnectorReceiver.Send(ackBytes, cancellationToken))
            {
                ackStatus = PacketStatus.Enqueued;
            }

            // create acknowledge in database
            var encodedAck = new byte[ackBytes.Length + 1];
            encodedAck[0] = 0x00;
            ackBytes.CopyTo(encodedAck, 1);
            await PacketRepository.AddAsync(new(encodedAck, $"{_options.ConnectorName}To{_options.PairedConnectorName}_Ack", ackStatus, packet.Id), cancellationToken);

            // if something went wrong in processing packet then rise an exception so the packet can be marked with error status in database
            if (ack.Contains("NOK"))
            {
                throw new Exception("Error on processing packet.");
            }
        }

        //packet receiver from paired Device --> send packed on TCP/IP
        else if (packet.Channel == $"{_options.PairedConnectorName}To{_options.ConnectorName}_Data")
        {
            ret = await _packetConnectorSender.Send(packet.BinaryData, cancellationToken);
        }
        // packet is acknowledge --> send packet on TCP/IP
        else if (packet.Channel == $"{_options.ConnectorName}To{_options.PairedConnectorName}_Ack")
        {
            ret = await _packetConnectorReceiver.Send(packet.BinaryData, cancellationToken);
        }

        return ret ? ProcessPacketState.Success : ProcessPacketState.Error;
    }

    public async Task ConnectorStartListen()
    {
        await _messenger.ListenAsync<PacketMessageData>(
            string.Format(PacketConnectorConst.TopicConnectorPacketSend, _options.ConnectorName.ToLower()),
            OnPairedConnectorDataReceived);

        await _messenger.AnswerAsync(
            string.Format(PacketConnectorConst.TopicConnectorAliveCheck, _options.ConnectorName.ToLower()),
            OnPairedConnectorAnswer);
    }

    private async ValueTask OnPairedConnectorDataReceived(PacketMessageData packet)
    {
        await PacketRepository.AddAsync(new PacketData(packet.BinaryData, packet.Channel, PacketStatus.Enqueued));
    }

    private bool OnPairedConnectorAnswer()
    {
        return true;
    }

    /// <summary>
    /// Is called by receiver 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    protected async void OnPacketReceived(object? sender, PacketReceivedEventArgs args)
    {
        var metadata = "metadata";
        var binaryData = await Converter.RawToBinaryDataConverter(args.Binary , metadata);

        var p = new PacketData(binaryData, args.Channel, PacketStatus.Enqueued)
        {
            Metadata = metadata
        };

        PacketRepository.AddAsync(p).AsTask().Wait();
    }

    /// <summary>
    /// Dispose all resources
    /// </summary>
    public void Dispose()
    {
        _packetConnectorReceiver?.Dispose();
        _packetConnectorSender?.Dispose();
    }

    public ValueTask<UIViewConfig?> BuildUIViewConfig()
    {
        var viewConfig = new UIViewConfig()
        {
            Views = new Dictionary<string, ViewConfig>()
            {{
                "All", new ViewConfig()
                {
                    Name = "All",
                    DisplayName = new Dictionary<string, string>()
                    {
                        {"en", "All"},
                        {"de", "Alle"}
                    },
                    Channels =
                    [
                        new ChannelConfig()
                        {
                            Name = "D01 -> D02 Data",
                            DisplayName = new Dictionary<string, string>()
                            {
                                {"en", "D01 -> D02 Data"},
                                {"de", "D01 -> D02 Daten"}
                            },
                            Channel = "Device01ToDevice02_Data",
                            Position = new Position(1, 1)
                        },
                        new ChannelConfig()
                        {
                            Name = "D02 -> D01 Data",
                            DisplayName = new Dictionary<string, string>()
                            {
                                {"en", "D02 -> D01 Data"},
                                {"de", "D02 -> D01 Daten"}
                            },
                            Channel = "Device02ToDevice01_Data",
                            Position = new Position(1, 1)
                        },
                        new ChannelConfig()
                        {
                            Name = "D01 -> D02 Ack",
                            DisplayName = new Dictionary<string, string>()
                            {
                                {"en", "D01 -> D02 Ack"},
                                {"de", "D01 -> D02 Ack"}
                            },
                            Channel = "Device01ToDevice02_Ack",
                            Position = new Position(1, 1)
                        },
                        new ChannelConfig()
                        {
                            Name = "D02 -> D01 Ack",
                            DisplayName = new Dictionary<string, string>()
                            {
                                {"en", "D02 -> D01 Ack"},
                                {"de", "D02 -> D01 Ack"}
                            },
                            Channel = "Device02ToDevice01_Ack",
                            Position = new Position(1, 1)
                        }
                    ]
                }
            }}
        };

        return new ValueTask<UIViewConfig?>(viewConfig);
    }
}

/// <summary></summary>
/// <param name="BinaryData">The packet data.</param>
/// <param name="Channel">The channel that packet is received on.</param>
public record PacketMessageData(ReadOnlyMemory<byte> BinaryData, string Channel);
