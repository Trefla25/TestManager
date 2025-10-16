using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using eHub.PlugIn;
using eHub.PlugIn.Communication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace eHub.Playground.ScriptsHttpConnector;
public class HttpConnector(ILogger<HttpConnector> logger, IHttpClientFactory httpClientFactory) : IHttpPacketTransfer
{
    public HttpPacketTransferConverter HttpPacketConverter { get; set; } = new();
    public IPacketRepository PacketRepository { get; set; } = default!;
    public HttpPacketTransferOptions HttpPacketTransferOptions { get; set; } = new("eManagerToEHub", "eHubToEManager");

    public event UpdateStatusDelegate? UpdateStatus;

    public void HttpSetup(IEndpointRouteBuilder routeBuilder)
    {
        routeBuilder.MapGet("/test/{id}", async (int id, HttpContext context) =>
        {
            await Task.Delay(1000);

            var packet = context.Items[nameof(PacketData)] as PacketData;

            packet.Status = PacketStatus.Processed;

            return Results.Ok("Ok");
        });
    }

    public async Task Run(CancellationToken cancellationToken)
    {
        //HttpPacketTransferOptions.HandleIncomingEndpoint = LegacyIncomingEndpoint;
        //HttpPacketTransferOptions.HandleOutgoingEndpoint = LegacyOutgoingEndpoint;
        //HttpPacketTransferOptions.HandleProcessIncomingPacket = LegacyIncomingPacket;
        //HttpPacketTransferOptions.HandleProcessOutgoingPacket = LegacyOutgoingPacket;

        //HttpPacketTransferOptions.SetIncomingHttpRequestHandler(CustomExecuteIncomingHttpRequest);
        //HttpPacketTransferOptions.SetIncomingPacketProcessingHandler(CustomProcessIncomingPacket);

        //HttpPacketTransferOptions.SetOutgoingHttpRequestHandler(CustomExecuteOutgoingHttpRequest);
        //HttpPacketTransferOptions.SetOutgoingPacketProcessingHandler(CustomProcessOutgoingPacket);

        //HttpPacketTransferOptions.SetPacketProcessingHandler(ProcessPacket1);
    }

    public ValueTask<ConnectorResponse> CustomExecuteIncomingHttpRequest(ConnectorRequest request, HttpConnectorEndpointConfig endpoint, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(ConnectorResponses.HttpBadRequest("HttpConnector", "Some message 1"));
    }

    public ValueTask<ConnectorResponse> CustomExecuteOutgoingHttpRequest(ConnectorRequest request, HttpConnectorEndpointConfig endpoint, CancellationToken cancellationToken)
    {
        var response = new ConnectorResponse(Encoding.UTF8.GetBytes("Some response"), "text/plain", "HttpConnector", new(202), new(ProcessPacketState.Error));
        return ValueTask.FromResult(response);
    }

    public async ValueTask<ConnectorResponse> CustomProcessIncomingPacket(PacketData packet, CancellationToken cancellationToken)
    {
        var httpMetadata = HttpPacketConverter.GetHttpMetadata(packet.Metadata);
        var request = new ConnectorRequest(packet.BinaryData, httpMetadata.ContentType, "HttpConnector");

        var response = await HttpPacketTransferOptions.ExecuteIncomingHttpRequest(request, httpMetadata.ToEndpointConfig(), cancellationToken);

        return response;
    }
    public async ValueTask<ConnectorResponse> CustomProcessOutgoingPacket(PacketData packet, CancellationToken cancellationToken)
    {
        var httpMetadata = HttpPacketConverter.GetHttpMetadata(packet.Metadata);
        var request = new ConnectorRequest(packet.BinaryData, httpMetadata.ContentType, "HttpConnector");

        var response = await HttpPacketTransferOptions.ExecuteOutgoingHttpRequest(request, httpMetadata.ToEndpointConfig(), cancellationToken);

        return response;
    }

    public async ValueTask<ProcessPacketState> ProcessPacket1(PacketData packet, CancellationToken cancellationToken)
    {
        if(packet.Channel == "eHubToEManager")
        {
            await HttpPacketTransferOptions.ProcessOutgoingPacket(packet, cancellationToken);
        }

        return ProcessPacketState.Success;
    }

    public async ValueTask<IResult> LegacyIncomingEndpoint(ReadOnlyMemory<byte> data, HttpConnectorEndpointConfig endpoint, CancellationToken cancellationToken)
    {
        return Results.Ok("Some random message from legacy incoming endpoint");
    }

    public async ValueTask<HttpConnectorResponseDto> LegacyOutgoingEndpoint(HttpConnectorRequestDto request, HttpConnectorEndpointConfig endpoint, CancellationToken cancellationToken)
    {
        return new HttpConnectorResponseDto(Encoding.UTF8.GetBytes("Some random message from legacy outgoing endpoint"), "text/plain", 201);
    }

    public async ValueTask<IResult> LegacyIncomingPacket(PacketData packet, CancellationToken cancellationToken)
    {
        //return Results.Accepted("Some random message from legacy incoming packet");

        var metadata = HttpPacketConverter.GetHttpMetadata(packet.Metadata);

        return await HttpPacketTransferOptions.HandleIncomingEndpoint(packet.BinaryData, metadata.ToEndpointConfig(), cancellationToken);
    }

    public async ValueTask<HttpConnectorResponseDto> LegacyOutgoingPacket(PacketData packet, CancellationToken cancellationToken)
    {
        //return new HttpConnectorResponseDto(Encoding.UTF8.GetBytes("Some random message from legacy outgoing packet"), "text/plain", 203);

        var metadata = HttpPacketConverter.GetHttpMetadata(packet.Metadata);
        var request = new HttpConnectorRequestDto(packet.BinaryData, metadata.Url, metadata.ContentType);

        return await HttpPacketTransferOptions.HandleOutgoingEndpoint(request, metadata.ToEndpointConfig(), cancellationToken);
    }
}
