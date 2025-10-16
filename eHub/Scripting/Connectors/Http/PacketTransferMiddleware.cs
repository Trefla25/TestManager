using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using eHub.Config;
using eHub.PlugIn;
using eHub.PlugIn.Communication;
using eHub.Scripting.Connectors.Cryptography;

namespace eHub.Scripting.Connectors.Http;

public class PacketTransferMiddleware(
    RequestDelegate next,
    ILoggerFactory loggerFactory,
    IHttpPacketTransfer httpPacketTransfer,
    HmacSigner signer,
    HttpIncoming httpConfig,
    ConnectorMetadata metadata)
{
    private readonly RequestDelegate _next = next;
    private readonly IHttpPacketTransfer _httpPacketTransfer = httpPacketTransfer;
    private readonly HmacSigner _signer = signer;
    private readonly HttpIncoming _httpConfig = httpConfig;
    private readonly ILogger _logger = loggerFactory.CreateLogger(metadata.TemplateName);

    public async Task InvokeAsync(HttpContext context)
    {
        if (_httpConfig.ExcludedEndpointsFromPacketTransfer.Contains(context.Request.Path.Value?.TrimEnd('/')))
        {
            await _next(context);
            return;
        }

        var originalRequestStream = context.Request.Body;
        await using var requestStream = new MemoryStream((int)(context.Request.ContentLength ?? 0));
        await context.Request.Body.CopyToAsync(requestStream);
        requestStream.Seek(0, SeekOrigin.Begin);

        context.Request.Body = requestStream;

        PacketData? packet = null;
        var isNewPacket = true;
        try
        {
            ReadOnlyMemory<byte> contentBytes = requestStream.GetBuffer().AsMemory(0, (int)requestStream.Length);
            requestStream.Seek(0, SeekOrigin.Begin);

            if (TryGetSignedPacketId(contentBytes, context.Request.Headers, out var packetId))
            {
                packet = await _httpPacketTransfer.PacketRepository.GetPacketByIdAsync(packetId);
                isNewPacket = false;
            }

            if (packet is null)
            {
                contentBytes = EncodingUtil.TrimBom(contentBytes, context.Request.ContentType);

                var contentMediaType = context.Request.ContentType is { } ? new ContentType(context.Request.ContentType).MediaType : null;
                var metadata = _httpPacketTransfer.HttpPacketConverter.BuildHttpMetadata(
                    content: contentBytes,
                    url: context.Request.Path.Value?.TrimEnd('/'),
                    httpMethod: context.Request.Method,
                    contentType: contentMediaType,
                    headers: context.Request.Headers,
                    routeValues: context.Request.RouteValues,
                    query: context.Request.Query);

                var binaryData = await _httpPacketTransfer.Converter.RawToBinaryDataConverter(contentBytes, metadata);
                packet = new PacketData(binaryData, _httpPacketTransfer.HttpPacketTransferOptions.IncomingChannel!, PacketStatus.InProgress) { Metadata = metadata };

                packet = await _httpPacketTransfer.PacketRepository.AddAsync(packet) ?? throw new Exception("Could not create packet");
            }

            context.Items.Add(nameof(PacketData), packet);

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process packet {id}.", packet?.Id);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            if(context.Response.Body.Length == 0)
            {
                var problemDetails = new
                {
                    type = $"https://httpstatuses.com/500",
                    title = "Internal Server Error",
                    status = 500,
                    detail = ex.Message
                };

                var response = JsonSerializer.SerializeToUtf8Bytes(problemDetails);

                await context.Response.Body.WriteAsync(response);
                context.Response.ContentLength = context.Response.Body.Length;
                context.Response.ContentType = MediaTypeNames.Application.Json;
            }
        }
        finally
        {
            context.Request.Body = originalRequestStream;

            if (packet is { } && isNewPacket) // Only update the packet status if it was created in this middleware
            {
                await UpdatePacketStatus(packet, context.Response);
            }
        }
    }

    private bool TryGetSignedPacketId(ReadOnlyMemory<byte> content, IHeaderDictionary headers, [NotNullWhen(true)] out int packetId)
    {
        if (headers.TryGetValue(ConnectorHeaders.PacketIdHeader, out var packetIdHeader) &&
            headers.TryGetValue(ConnectorHeaders.SignatureHeader, out var signatureHeader) &&
            int.TryParse(packetIdHeader, out var signedPacketId))
        {
            var headersString = string.Join(";", headers.Where(h => h.Key != ConnectorHeaders.PacketIdHeader && h.Key != ConnectorHeaders.SignatureHeader).Select(kvp => $"{kvp.Key}:{kvp.Value}"));
            var data = packetIdHeader + headersString + Encoding.UTF8.GetString(content.Span);

            if (!_signer.VerifyFromBase64(Encoding.UTF8.GetBytes(data), signatureHeader.ToString()))
            {
                throw new Exception("Could not validate request signature");
            }

            packetId = signedPacketId;
            return true;
        }

        packetId = 0;
        return false;
    }

    private async Task UpdatePacketStatus(PacketData packet, HttpResponse response)
    {
        var dbPacket = await _httpPacketTransfer.PacketRepository.GetPacketByIdAsync(packet.Id);
        var isSuccessStatusCode = response.StatusCode >= 200 && response.StatusCode < 300;
       
        if(dbPacket.Status == PacketStatus.InProgress)
        {
            // The packet status has not been updated by another process

            var processPacketState = response.Headers.TryGetValue(ConnectorHeaders.ProcessPacketStateHeader, out var stateHeader)
                && Enum.TryParse<ProcessPacketState>(stateHeader, out var state) ? (ProcessPacketState?)state : null;

            var status = processPacketState switch
            {
                ProcessPacketState.Success => PacketStatus.Processed,
                ProcessPacketState.Retry => PacketStatus.Enqueued,
                ProcessPacketState.Error => PacketStatus.Error,
                ProcessPacketState.FatalError => PacketStatus.FatalError,
                ProcessPacketState.InProgress => PacketStatus.InProgress,
                ProcessPacketState.RetryUnchanged => PacketStatus.Enqueued,
                null => packet.Status == PacketStatus.InProgress ? (isSuccessStatusCode ? PacketStatus.Processed : PacketStatus.FatalError) : packet.Status
            };

            await _httpPacketTransfer.PacketRepository.UpdatePacketStatusAsync(packet.Id, status);
        }
        
        if(!isSuccessStatusCode)
        {
            response.Body.Seek(0, SeekOrigin.Begin);

            using var memoryStream = response.ContentLength.HasValue
                ? new MemoryStream(new byte[response.ContentLength.Value])
                : new MemoryStream();

            await response.Body.CopyToAsync(memoryStream);
            var contentBytes = memoryStream.ToArray();
            var responseContent = Encoding.UTF8.GetString(contentBytes);

            var errorMessage = $"Request failed with status code {response.StatusCode}. Response Content: {responseContent}";
            await _httpPacketTransfer.PacketRepository.AddAsync(new PacketData(
                Encoding.UTF8.GetBytes(errorMessage),
                $"{_httpPacketTransfer.HttpPacketTransferOptions.IncomingChannel}:Error",
                PacketStatus.FatalError,
                packet.Id));
        }
    }
}
