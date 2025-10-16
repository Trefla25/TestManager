using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using eController.DataLib.Logging;
using eController.ProjectToolbox.Tcp;
using eController.ProjectToolbox.Tcp.Endpoints;
using Microsoft.Extensions.Logging;

namespace eHub.Playground.ScriptsPacketTransfer;

internal class PacketConnectorReceiver : IDisposable
{
    private readonly ILogger _logger;
    private readonly PacketConnectorConfig _options;
    private readonly ITcpEndpoint _tcpServer;

    public event EventHandler<PacketReceivedEventArgs>? PacketReceived;

    public PacketConnectorReceiver(PacketConnectorConfig options, ITcpEndpointFactory tcpEndpointFactory, ILogger logger)
    {
        _logger = logger;
        _options = options;

        _tcpServer = tcpEndpointFactory.CreateServer(new TcpEndpointOptions { IpAddress = options.IpAddress, Port = options.RecvPort, TryParseMessage = MessageCallBack });
    }

    /// <summary>
    /// Starts the tcp client socket
    /// </summary>
    /// <param name="obj"></param>
    public async Task StartReceiver(CancellationToken cancellationToken)
    {
        try
        {
            await _tcpServer.StartSocket(cancellationToken);

            if (_tcpServer.ConnectionState == ConnectionState.Connected)
            {
                _logger.LogInformation("Socket Connection established: {IpAddress}:{RecvPort}", _options.IpAddress, _options.RecvPort);
            }

            _tcpServer.ConnectionStateChanged += _tcpServer_ConnectionStateChanged;
        }
        catch (Exception e)
        {
            _logger.LogError(e, new LogMessageObject(LogMessageObject.EVENT_NAME.SOCKET_COM)
            {
                Message = $"No connection to {_options.IpAddress}:{_options.RecvPort}"
            });
        }
    }

    private void _tcpServer_ConnectionStateChanged(object sender, ConnectionStateChangedEventArgs e)
    {

    }

    /// <summary>
    /// Sends data on TCP/IP
    /// </summary>
    /// <param name="cancellationToken"></param>
    public async Task<bool> Send(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        try
        {
            await _tcpServer.Send(data, cancellationToken);

            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Send()");

            return false;
        }
    }

    /// <summary>
    /// Handles incomming messages.
    /// Contains business logic how to handle packets
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private Task<MessageParseResult> MessageCallBack(ReadOnlySequence<byte> message, CancellationToken cancellationToken)
    {
        var readTelegram = new MessageParseResult
        {
            BytesRead = (int)message.Length,
            Success = true
        };

        try
        {
            var args = new PacketReceivedEventArgs
            {
                Binary = message.ToArray(),
                Channel = $"{_options.ConnectorName}To{_options.PairedConnectorName}_Data"
            };

            OnPacketReceived(args);
            return Task.FromResult(readTelegram);
        }
        catch (Exception e)
        {
            _logger.LogError(e, new LogMessageObject(LogMessageObject.EVENT_NAME.GENERAL)
            {
                Topic = "MessageCallBack()"
            });

            return Task.FromResult(readTelegram);
        }
    }

    protected virtual void OnPacketReceived(PacketReceivedEventArgs e)
    {
        PacketReceived?.Invoke(this, e);
    }

    public void Dispose()
    {
        _tcpServer.Dispose();
    }
}
