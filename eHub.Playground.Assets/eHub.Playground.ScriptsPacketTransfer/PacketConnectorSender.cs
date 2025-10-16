using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using eController.DataLib.Logging;
using eController.ProjectToolbox.Tcp;
using eController.ProjectToolbox.Tcp.Endpoints;
using Microsoft.Extensions.Logging;

namespace eHub.Playground.ScriptsPacketTransfer;
internal class PacketConnectorSender : IDisposable
{
    private readonly ILogger _logger;
    private readonly PacketConnectorConfig _options;
    private readonly ITcpEndpoint _tcpClient;

    public event EventHandler<PacketReceivedEventArgs>? PacketReceived;

    public PacketConnectorSender(PacketConnectorConfig options, ITcpEndpointFactory tcpEndpointFactory, ILogger logger)
    {
        _logger = logger;
        _options = options;

        _tcpClient = tcpEndpointFactory.CreateClient(new TcpEndpointOptions { IpAddress = options.IpAddress, Port = options.SendPort, TryParseMessage = MessageCallBack });

    }

    /// <summary>
    /// This method will be called when there is a new message from the tcp server.
    /// It handles the main business logic of telegram handling.
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

        _logger.LogInformation("Sender Data Recieved");
        return Task.FromResult(readTelegram);

    }

    protected virtual void OnPacketReceived(PacketReceivedEventArgs e)
    {
        PacketReceived?.Invoke(this, e);
    }

    /// <summary>
    /// Starts the ConnectorSender
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task StartSender(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                ConnectSocket(cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
            catch (Exception ex)
            {
                var msgObj = new LogMessageObject(LogMessageObject.EVENT_NAME.SOCKET_COM);
                msgObj.Message = $"No connection to {_options.IpAddress}:{_options.SendPort}";
                _logger.LogError(ex, msgObj);
            }
        }
    }

    /// <summary>
    /// Starts the socket of the Tcp-Client
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private void ConnectSocket(CancellationToken cancellationToken)
    {
        if (_tcpClient.ConnectionState == ConnectionState.Connected)
        {
            return;
        }

        _tcpClient.StartSocket(cancellationToken);

        if (_tcpClient.ConnectionState == ConnectionState.Connected)
        {
            _logger.LogInformation($"Socket Connection established: {_options.IpAddress}:{_options.SendPort}");
        }
    }

    /// <summary>
    /// Sends data on TCP/IP
    /// </summary>
    /// <param name="cancellationToken"></param>
    public async Task<bool> Send(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        try
        {
            await _tcpClient.Send(data, cancellationToken);

            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Send()");

            return false;
        }
    }

    public void Dispose()
    {
        _tcpClient.Dispose();
    }
}
