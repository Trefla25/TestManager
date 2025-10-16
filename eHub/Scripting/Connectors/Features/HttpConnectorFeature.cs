using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using eController.Util;
using eHub.Authentication;
using eHub.Config;
using eHub.PlugIn;
using eHub.PlugIn.Communication;
using eHub.Scripting.Connectors.Cryptography;
using eHub.Scripting.Connectors.Http;
using eMessenger;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace eHub.Scripting.Connectors.Features;

public class HttpConnectorFeature : IConnectorFeature
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HttpConnectorFeature> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _configuration;
    private readonly IMessenger _messenger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpConnector _httpConnector;
    private readonly IHttpPacketTransfer? _httpPacketTransfer;
    private readonly ConnectorMetadata _metadata;
    private readonly ConnectorTemplate _connectorTemplate;
    private readonly HashSet<string> _urls = [];
    private readonly HmacSigner _signer = new();

    private CancellationTokenSource? _cancellationTokenSource;
    private IRegistrationToken _regToken = NullRegistrationToken.Instance;

    private WebApplication? _host;

    private readonly HttpIncoming? _httpIncomingConfig;
    private readonly HttpOutgoing? _httpOutgoingConfig;

    public bool RequiresAuthorization => _httpIncomingConfig?.Auth is { Enabled: true } auth && (auth.Basic is { } || auth.JWT is { } || auth.Schemes is { Count: > 0 });
    private CancellationToken CancellationToken => _cancellationTokenSource?.Token ?? default;

    public HttpConnectorFeature(
        IServiceProvider serviceProvider,
        ILogger<HttpConnectorFeature> logger,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IScopedMessenger messenger,
        IHttpClientFactory httpClientFactory,
        IHttpConnector httpConnector,
        ConnectorMetadata metadata,
        ConnectorTemplate connectorTemplate)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _configuration = configuration;
        _messenger = messenger;
        _httpClientFactory = httpClientFactory;
        _httpConnector = httpConnector;
        _metadata = metadata;
        _connectorTemplate = connectorTemplate;

        _httpIncomingConfig = _connectorTemplate.HttpIncoming;
        _httpOutgoingConfig = _connectorTemplate.HttpOutgoing;

        _httpPacketTransfer = httpConnector as IHttpPacketTransfer;
    }
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_cancellationTokenSource is { })
        {
            throw new InvalidOperationException("Http Connector feature already started");
        }

        InterpolateHttpEndpointConfigs();
        SetHttpConnectorOptionDefaults();
        SetHttpPacketTransferOptionDefaults();

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await StartConnectorHttpListeners();
        await StartConnectorEMessengerListeners();

        await HttpConnectorStarted();
    }

    private async Task HttpConnectorStarted()
    {
        if (_httpPacketTransfer is { })
        {
            var incomingInProgressPacketsStatus = _httpIncomingConfig?.ResendInProgressPacketsOnInterrupt == true ? PacketStatus.Enqueued : PacketStatus.FatalError;
            var outgoingInProgressPacketsStatus = _httpOutgoingConfig?.ResendInProgressPacketsOnInterrupt == true ? PacketStatus.Enqueued : PacketStatus.FatalError;

            await _httpPacketTransfer.PacketRepository.Update()
                .Set(p => p.Status, incomingInProgressPacketsStatus)
                .Where(p => p.Status == PacketStatus.InProgress && p.Channel == _httpPacketTransfer.HttpPacketTransferOptions.IncomingChannel)
                .ExecuteAsync(CancellationToken);

            await _httpPacketTransfer.PacketRepository.Update()
                .Set(p => p.Status, outgoingInProgressPacketsStatus)
                .Where(p => p.Status == PacketStatus.InProgress && p.Channel == _httpPacketTransfer.HttpPacketTransferOptions.OutgoingChannel)
                .ExecuteAsync(CancellationToken);
        }
    }

    private async ValueTask<ProcessPacketState> ProcessPacket(PacketData packet, CancellationToken cancellationToken)
    {
        if (_httpPacketTransfer is null)
        {
            throw new Exception("Connector does not implement IHttpPacketTransfer interface.");
        }

        try
        {
            var response = packet.Channel switch
            {
                _ when packet.Channel == _httpPacketTransfer.HttpPacketTransferOptions.IncomingChannel => await ResendHttpIncomingPacket(packet, cancellationToken),
                _ when packet.Channel == _httpPacketTransfer.HttpPacketTransferOptions.OutgoingChannel => await _httpPacketTransfer.HttpPacketTransferOptions.ProcessOutgoingPacket(packet, cancellationToken),
                _ => throw new Exception($"Channel not recognized: {packet.Channel}.")
            };

            var processPacketState = response.PacketTransfer?.ProcessPacketState ?? (response.Http is null || response.Http.IsSuccessStatusCode ? ProcessPacketState.Success : ProcessPacketState.FatalError);

            if (processPacketState == ProcessPacketState.FatalError)
            {
                var responseContent = Encoding.UTF8.GetString(response.Content.Span);
                var errorMessage = $"Request failed with status code {response.Http?.StatusCode.ToString() ?? "<NO STATUS CODE>"}. Response Content: {responseContent}";
                throw new Exception(errorMessage);
            }

            return processPacketState;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process packet {id}.", packet.Id);

            await _httpPacketTransfer.PacketRepository.AddAsync(new PacketData(Encoding.UTF8.GetBytes(ex.Message), $"{packet.Channel}:Error", PacketStatus.FatalError, packet.Id), cancellationToken);

            return ProcessPacketState.FatalError;
        }
    }

    private async ValueTask<ConnectorResponse> ResendHttpIncomingPacket(PacketData packet, CancellationToken cancellationToken)
    {
        if (_httpPacketTransfer is null)
        {
            throw new Exception("Connector does not implement IHttpPacketTransfer interface.");
        }

        if (packet.Metadata is null)
        {
            throw new Exception("Metadata can not be null");
        }

        var rawData = await _httpPacketTransfer.HttpPacketConverter.PacketToRawDataConverter(packet, cancellationToken);
        var httpMetadata = _httpPacketTransfer.HttpPacketConverter.GetHttpMetadata(packet.Metadata);

        if (httpMetadata.HttpMethod is null)
        {
            throw new Exception("HTTP method can not be null");
        }

        using var httpClient = _httpClientFactory.CreateClient();
        using var content = new ReadOnlyMemoryContent(rawData);

        if (!string.IsNullOrEmpty(httpMetadata.ContentType))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue(httpMetadata.ContentType);
        }

        content.Headers.ContentLength = rawData.Length;

        var baseUri = new UriBuilder(_urls.First()) { Host = "localhost" }.Uri;
        var requestUri = new Uri(baseUri, httpMetadata.Url);

        using var httpRequestMessage = new HttpRequestMessage(new HttpMethod(httpMetadata.HttpMethod), requestUri);
        httpRequestMessage.Content = content;

        if (httpMetadata.Headers is { } headers)
        {
            foreach (var header in headers.SelectMany(header => header.Value.Where(value => !string.IsNullOrEmpty(value)).Select(value => (header.Key, value))))
            {
                if (ConnectorHeaders.ContentHeaders.Contains(header.Key))
                {
                    continue;
                }

                httpRequestMessage.Headers.Add(header.Key, header.value);
            }
        }

        if (httpMetadata.Headers != null)
        {
            httpMetadata.Headers["Content-Length"] = rawData.Length.ToString();
        }

        var packetId = packet.Id.ToString();
        var headersString = string.Join(";", httpMetadata.Headers?.Select(kvp => $"{kvp.Key}:{kvp.Value.Join(", ")}") ?? []);
        var signData = packetId + headersString + Encoding.UTF8.GetString(rawData.Span);
        var signature = _signer.SignToBase64(Encoding.UTF8.GetBytes(signData));

        httpRequestMessage.Headers.Add(ConnectorHeaders.PacketIdHeader, packet.Id.ToString());
        httpRequestMessage.Headers.Add(ConnectorHeaders.SignatureHeader, signature);

        using var httpResponse = await httpClient.SendAsync(httpRequestMessage, cancellationToken);
        return await HttpUtil.GetConnectorResponseFromHttpResponseMessage(httpResponse, _metadata.TemplateName);
    }

    private async ValueTask<ConnectorResponse> ProcessIncomingPacket(PacketData packet, CancellationToken cancellationToken)
    {
        if (_httpIncomingConfig is null)
        {
            throw new Exception("Missing httpIncoming config");
        }

        if (_httpPacketTransfer is null)
        {
            throw new Exception("Connector does not implement IHttpPacketTransfer interface.");
        }

        if (packet.Metadata is null)
        {
            throw new Exception("Metadata can not be null");
        }

        var rawData = await _httpPacketTransfer.HttpPacketConverter.PacketToRawDataConverter(packet, cancellationToken);
        var httpMetadata = _httpPacketTransfer.HttpPacketConverter.GetHttpMetadata(packet.Metadata);

        var request = new ConnectorRequest()
        {
            Content = rawData,
            ContentType = httpMetadata.ContentType ?? MediaTypeNames.Application.Octet,
            ConnectorName = _metadata.TemplateName,
            Http = new(
                httpMetadata.Url ?? "",
                httpMetadata.HttpMethod ?? "",
                httpMetadata.RouteParameters,
                httpMetadata.Headers,
                httpMetadata.Query)
        };

        HttpConnectorEndpointConfig endpoint;
        if (httpMetadata.EndpointKey == null)
        {
            endpoint = httpMetadata.ToEndpointConfig();
        }
        else
        {
            endpoint = ObjUtil.Clone(_httpIncomingConfig.Endpoints[httpMetadata.EndpointKey]);
            endpoint.Path = httpMetadata.Url;
        }

        return await _httpPacketTransfer.HttpPacketTransferOptions.ExecuteIncomingHttpRequest(request, endpoint, cancellationToken);
    }

    private async ValueTask<ConnectorResponse> ProcessOutgoingPacket(PacketData packet, CancellationToken cancellationToken)
    {
        if (_httpPacketTransfer is null)
        {
            throw new Exception("Connector does not implement IHttpPacketTransfer interface.");
        }

        if (_httpOutgoingConfig is null)
        {
            throw new Exception("Missing httpOutgoing config");
        }

        if (packet.Metadata is null)
        {
            throw new Exception("Metadata can not be null");
        }

        var rawData = await _httpPacketTransfer.HttpPacketConverter.PacketToRawDataConverter(packet, cancellationToken);
        var httpMetadata = _httpPacketTransfer.HttpPacketConverter.GetHttpMetadata(packet.Metadata);

        var request = new ConnectorRequest() {
            Content = rawData,
            ContentType = httpMetadata.ContentType ?? MediaTypeNames.Application.Octet,
            ConnectorName = _metadata.TemplateName,
            Http = new(
                httpMetadata.Url ?? "",
                httpMetadata.HttpMethod ?? "",
                httpMetadata.RouteParameters,
                httpMetadata.Headers,
                httpMetadata.Query)
            };

        var endpoint = httpMetadata.EndpointKey is null ? httpMetadata.ToEndpointConfig() : _httpOutgoingConfig.Endpoints[httpMetadata.EndpointKey];

        return await _httpPacketTransfer.HttpPacketTransferOptions.ExecuteOutgoingHttpRequest(request, endpoint, cancellationToken);
    }

    private async ValueTask<ConnectorResponse> ExecuteIncomingHttpRequest(ConnectorRequest request, HttpConnectorEndpointConfig endpoint, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(endpoint.Api))
        {
            return await _httpConnector.HttpConnectorOptions.ExecuteOutgoingHttpRequest(request, endpoint, cancellationToken);
        }

        if (string.IsNullOrEmpty(endpoint.Topic))
        {
            return ConnectorResponses.HttpBadGateway(_metadata.TemplateName, $"No eMessenger topic was configured for the {endpoint.Path} incoming endpoint.");
        }

        var responses = await _messenger.AskAsync<ConnectorRequest, ConnectorResponse>(endpoint.Topic, request);

        if (responses.Count == 0)
        {
            return ConnectorResponses.HttpBadGateway(_metadata.TemplateName, $"Could not communicate with paired connector on topic \"{endpoint.Topic}\".");
        }

        if (responses.Count > 1)
        {
            return ConnectorResponses.HttpBadGateway(_metadata.TemplateName, $"Too many connectors responded on topic \"{endpoint.Topic}\" ({responses.Count}), expected 1.");

        }

        return responses.First();
    }

    private async ValueTask<ConnectorResponse> ExecuteOutgoingHttpRequest(ConnectorRequest request, HttpConnectorEndpointConfig endpoint, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(endpoint.HttpMethod))
        {
            return ConnectorResponses.HttpBadGateway(_metadata.TemplateName, $"No HTTP method was configured for the {endpoint.Topic} outgoing endpoint.");
        }

        using var httpClient = _httpClientFactory.CreateClient(endpoint.Api ?? string.Empty);
        using var content = new ReadOnlyMemoryContent(request.Content);

        if (!string.IsNullOrEmpty(request.ContentType))
        {
            var contentType = new ContentType(request.ContentType);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType.MediaType, contentType.CharSet);
        }

        content.Headers.ContentLength = request.Content.Length;

        var requestUri = endpoint.Path;
        if (requestUri is { } && request.Http?.RouteParameters is { Count: > 0 } routeParameters)
        {
            foreach (var parameter in routeParameters)
            {
                requestUri = requestUri.Replace($"{{{parameter.Key}}}", parameter.Value, StringComparison.OrdinalIgnoreCase);
            }
        }

        if (requestUri is { } && request.Http?.Query is { Count: > 0 } queryParams)
        {
            requestUri = QueryHelpers.AddQueryString(requestUri, queryParams);
        }

        using var httpRequestMessage = new HttpRequestMessage(new HttpMethod(endpoint.HttpMethod), requestUri);
        httpRequestMessage.Content = content;

        if (request.Http?.Headers is { } headers)
        {
            foreach (var header in headers.SelectMany(header => header.Value.Where(value => !string.IsNullOrEmpty(value)).Select(value => (header.Key, value))))
            {
                if (ConnectorHeaders.ContentHeaders.Contains(header.Key))
                {
                    continue;
                }

                httpRequestMessage.Headers.Add(header.Key, header.value);
            }
        }

        try
        {
            using var response = await httpClient.SendAsync(httpRequestMessage, cancellationToken);
            return await HttpUtil.GetConnectorResponseFromHttpResponseMessage(response, _metadata.TemplateName);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Could not send a {httpMethod} HTTP Request to {url}.", endpoint.HttpMethod, httpRequestMessage.RequestUri);

            var resendPacketsOnError = (endpoint.ResendPacketsOnCommunicationError ?? _httpOutgoingConfig!.ResendPacketsOnCommunicationError) == true;

            return resendPacketsOnError
                ? ConnectorResponses.HttpPacketTransferProblem("Communication Error", _metadata.TemplateName, StatusCodes.Status202Accepted, ProcessPacketState.Error, $"Could not send a {endpoint.HttpMethod} HTTP Request to {httpRequestMessage.RequestUri}.")
                : ConnectorResponses.HttpBadGateway(_metadata.TemplateName, $"Could not send a {endpoint.HttpMethod} HTTP Request to {httpRequestMessage.RequestUri}.");
        }
    }

    private async Task StartConnectorHttpListeners()
    {
        if (_httpIncomingConfig is null)
        {
            return;
        }

        var builder = WebApplication.CreateSlimBuilder();
        builder.Configuration.Sources.Clear();

        ConfigureKestrel(builder);
        ConfigureAuthentication(builder);

        builder.Services.AddSingleton(_loggerFactory);
        builder.Services.AddSingleton(_signer);
        builder.Services.AddSingleton(_httpIncomingConfig);
        builder.Services.AddSingleton(_metadata);

        if (_httpPacketTransfer is { })
        {
            builder.Services.AddSingleton(_httpPacketTransfer);
        }

        _host = builder.Build();

        _host.UseMiddleware<ConnectorLoggingMiddleware>(_metadata.TemplateName);

        if (_httpIncomingConfig?.InsertUnauthorizedPackets == true && _httpPacketTransfer is { })
        {
            _host.UseMiddleware<PacketTransferMiddleware>();
        }

        if (RequiresAuthorization)
        {
            _host.UseAuthentication();
            _host.UseAuthorization();
        }

        if (_httpIncomingConfig?.InsertUnauthorizedPackets != true && _httpPacketTransfer is { })
        {
            _host.UseMiddleware<PacketTransferMiddleware>();
        }

        RegisterConnectorHttpEndpoints();

        await _host.StartAsync();

        foreach (var url in _urls)
        {
            _logger.LogInformation("{connector} connector is now listening on {url}", _metadata.TemplateName, url);
        }
    }

    private void RegisterConnectorHttpEndpoints()
    {
        if (_host is null)
        {
            _logger.LogWarning("Connector host not instanciated!");
            return;
        }

        if (_httpIncomingConfig?.Endpoints.Values is null)
        {
            return;
        }

        _httpConnector.HttpSetup(_host);

        var registeredEndpoints = ((IEndpointRouteBuilder)_host).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToDictionary(
                e => e.RoutePattern.RawText?.Trim('/') ?? "",
                e => e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.ToHashSet() ?? []);

        // Map http connector routes
        foreach (var endpoint in _httpIncomingConfig.Endpoints)
        {
            var path = endpoint.Value.Path?.Trim('/');
            if (string.IsNullOrEmpty(path))
            {
                _logger.LogWarning("Incoming endpoint path is not configured.");
                continue;
            }

            // if the endpoint is already mapped on this route pattern and method, skip it
            if (registeredEndpoints.TryGetValue(path, out var methods)
                && (!string.IsNullOrEmpty(endpoint.Value.HttpMethod) && methods.Contains(endpoint.Value.HttpMethod) || methods.Count == 0))
            {
                _logger.LogWarning("Skipping registration of endpoint '{path}' with method '{method}' as it is already programmatically mapped via HttpSetup.", path, endpoint.Value.HttpMethod);
                continue;
            }

            async Task Handle(HttpContext context)
            {
                ConnectorResponse response;
                try
                {
                    if (_httpPacketTransfer is { })
                    {
                        response = await ProcessPacketTransferIncomingHttpRequest(context, endpoint.Key, endpoint.Value);
                    }
                    else
                    {
                        var request = await HttpUtil.GetConnectorRequestFromHttpRequest(context.Request, _metadata.TemplateName);
                        response = await _httpConnector.HttpConnectorOptions.ExecuteIncomingHttpRequest(request, endpoint.Value, CancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to handle incoming HTTP request.");
                    response = ConnectorResponses.HttpProblem("Failed to handle incoming HTTP request", _metadata.TemplateName, StatusCodes.Status500InternalServerError, ex.Message);
                }

                await response.ToIResult().ExecuteAsync(context);
            }

            var routeBuilder = endpoint.Value.HttpMethod != null
                ? _host.MapMethods(path, [endpoint.Value.HttpMethod], Handle)
                : _host.Map(path, Handle);

            if (RequiresAuthorization && endpoint.Value.Authorize != false)
            {
                routeBuilder.RequireAuthorization();
            }
            if (endpoint.Value.ContentTypes is { Length: > 0 } contentTypes)
            {
                var primaryContentType = contentTypes[0];
                var otherContentTypes = contentTypes.Skip(1).ToArray();
                routeBuilder.WithMetadata(new ConsumesAttribute(primaryContentType, otherContentTypes));
            }
            if (endpoint.Value.RequestSizeLimit != null)
            {
                routeBuilder.WithMetadata(new RequestSizeLimitAttribute(endpoint.Value.RequestSizeLimit.Value));
            }
        }
    }

    private void ConfigureKestrel(WebApplicationBuilder builder)
    {
        if (_httpIncomingConfig is null)
        {
            return;
        }
        var useKestrelConfig = _httpIncomingConfig.Kestrel.GetSection("Endpoints").GetChildren().Count() > 0;

        builder.WebHost.UseKestrel(_httpIncomingConfig.ApplyKestrelTo);

        var endpointsSection = _httpIncomingConfig.Kestrel.GetSection("Endpoints");

        foreach (var endpoint in endpointsSection.GetChildren())
        {
            var url = endpoint["Url"];
            if (url is { })
            {
                _urls.Add(url);
            }
        }
    }

    private void ConfigureAuthentication(WebApplicationBuilder builder)
    {
        if (_httpIncomingConfig is null)
        {
            return;
        }

        var authConfig = _httpIncomingConfig.Auth;
        authConfig ??= _configuration.GetSection(AuthenticationConfig.DefaultKey).Get<AuthenticationConfig>();

        if (authConfig is null)
        {
            return;
        }

        if (RequiresAuthorization)
        {
            _httpIncomingConfig.Auth = authConfig;

            builder.Services.Configure<AuthenticationConfig>(options =>
            {
                // TODO: Find better way of passing authConfig as IOptions
                options.Basic = _httpIncomingConfig.Auth.Basic;
                options.JWT = _httpIncomingConfig.Auth.JWT;
            });

            var signatureAuthScheme = new AuthenticationSchemeConfig
            {
                SchemeName = SignatureAuthenticationHandler.SchemeName,
            };

            signatureAuthScheme.SetHandler<SignatureAuthenticationHandler, AuthenticationSchemeOptions>();
            _httpIncomingConfig.Auth.Schemes.Add(signatureAuthScheme);

            builder.Services.AddEHubAuthentication(_httpIncomingConfig.Auth);
        }
    }

    private async Task StartConnectorEMessengerListeners()
    {
        if (_httpOutgoingConfig is null || _httpOutgoingConfig.Endpoints is null)
        {
            return;
        }

        foreach (var kvp in _httpOutgoingConfig.Endpoints)
        {
            var endpointKey = kvp.Key;
            var endpoint = kvp.Value;

            if (string.IsNullOrEmpty(endpoint.Topic))
            {
                _logger.LogWarning("Outgoing endpoint topic is not configured.");
                continue;
            }

            if (endpoint.Path?.StartsWith('/') == true)
            {
                endpoint.Path = endpoint.Path.TrimStart('/');
            }

            _regToken += await _messenger.AnswerAsync<ConnectorRequest, ConnectorResponse>(
                endpoint.Topic,
                async (request) =>
                {
                    try
                    {
                        ConnectorResponse response;
                        if (_httpPacketTransfer is { } && !_httpOutgoingConfig.ExcludedEndpointsFromPacketTransfer.Contains(endpoint.Topic))
                        {
                            response = await ProcessPacketTransferOutgoingHttpRequest(request, endpointKey, endpoint);
                        }
                        else
                        {
                            response = await _httpConnector.HttpConnectorOptions.ExecuteOutgoingHttpRequest(request, endpoint, CancellationToken);
                        }

                        return response;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to handle outgoing HTTP request.");
                        return ConnectorResponses.HttpProblem("Failed to handle outgoing HTTP request", _metadata.TemplateName, StatusCodes.Status500InternalServerError, ex.Message);
                    }
                });
        }
    }


    private async ValueTask<ConnectorResponse> ProcessPacketTransferIncomingHttpRequest(HttpContext context, string endpointKey, HttpConnectorEndpointConfig endpoint)
    {
        if (_httpPacketTransfer is null)
        {
            throw new Exception("Connector does not implement IHttpPacketTransfer interface.");
        }

        if (_httpIncomingConfig is null)
        {
            throw new Exception("Missing httpIncoming config");
        }

        if (!context.Items.TryGetValue(nameof(PacketData), out var packetObject))
        {
            var request = await HttpUtil.GetConnectorRequestFromHttpRequest(context.Request, _metadata.TemplateName);
            return await _httpConnector.HttpConnectorOptions.ExecuteIncomingHttpRequest(request, endpoint, CancellationToken);
        }

        var packet = packetObject as PacketData ?? throw new Exception("Could not get HTTP request packet.");

        if (packet.Metadata is null)
        {
            throw new Exception("Metadata can not be null");
        }

        var metadata = _httpPacketTransfer.HttpPacketConverter.GetHttpMetadata(packet.Metadata);

        var storeMode = endpoint.StoreMode ?? _httpIncomingConfig.StoreMode;
        if (storeMode == StoreMode.Persistent)
        {
            metadata.Api = endpoint.Api;
            metadata.Topic = endpoint.Topic;
            metadata.AcceptedContentTypes = endpoint.ContentTypes;
            packet.Metadata = JsonSerializer.Serialize(metadata, HttpPacketTransferConverter.JsonOptions);
        }
        else
        {
            metadata.EndpointKey = endpointKey;
            packet.Metadata = JsonSerializer.Serialize(metadata, HttpPacketTransferConverter.JsonOptions);
        }

        await _httpPacketTransfer.PacketRepository.Update()
            .Set(p => p.Metadata, packet.Metadata)
            .Where(p => p.Id == packet.Id)
            .ExecuteAsync();

        var response = await _httpPacketTransfer.HttpPacketTransferOptions.ProcessIncomingPacket(packet, CancellationToken);
        return response;
    }

    private async ValueTask<ConnectorResponse> ProcessPacketTransferOutgoingHttpRequest(ConnectorRequest request, string endpointKey, HttpConnectorEndpointConfig endpoint)
    {
        if (_httpPacketTransfer is null)
        {
            throw new Exception("Connector does not implement IHttpPacketTransfer interface.");
        }

        if (_httpOutgoingConfig is null)
        {
            throw new Exception("Missing httpOutgoing config");
        }

        var storeMode = endpoint.StoreMode ?? _httpOutgoingConfig.StoreMode;

        // ignore the host header for outgoing requests
        var headers = request.Http?.Headers?.Where(h => h.Key != Microsoft.Net.Http.Headers.HeaderNames.Host);

        var metadata = storeMode == StoreMode.Dynamic
            ? _httpPacketTransfer.HttpPacketConverter.BuildHttpMetadata(
                content: request.Content,
                endpointKey: endpointKey,
                url: endpoint.Path, // store path as well for the UI preview
                httpMethod: endpoint.HttpMethod, // store the HTTP method as well for the UI preview
                contentType: request.ContentType,
                headers: headers,
                routeValues: request.Http?.RouteParameters,
                query: request.Http?.Query)
            : _httpPacketTransfer.HttpPacketConverter.BuildHttpMetadata(
                content: request.Content,
                url: endpoint.Path,
                httpMethod: endpoint.HttpMethod,
                topic: endpoint.Topic,
                contentType: request.ContentType,
                headers: headers,
                routeValues: request.Http?.RouteParameters,
                api: endpoint.Api,
                acceptedContentTypes: endpoint.ContentTypes,
                query: request.Http?.Query);

        var binaryData = await _httpPacketTransfer.Converter.RawToBinaryDataConverter(request.Content, metadata, CancellationToken);
        var packet = new PacketData(binaryData, _httpPacketTransfer.HttpPacketTransferOptions.OutgoingChannel!, PacketStatus.InProgress) { Metadata = metadata };

        packet = await _httpPacketTransfer.PacketRepository.AddAsync(packet, CancellationToken) ?? throw new Exception("Could not create packet");

        try
        {
            var response = await _httpPacketTransfer.HttpPacketTransferOptions.ProcessOutgoingPacket(packet, CancellationToken);

            var isSuccessStatusCode = response.Http?.IsSuccessStatusCode != false;
            var status = response.PacketTransfer?.ProcessPacketState switch
            {
                ProcessPacketState.Success => PacketStatus.Processed,
                ProcessPacketState.Retry => PacketStatus.Enqueued,
                ProcessPacketState.Error => PacketStatus.Error,
                ProcessPacketState.FatalError => PacketStatus.FatalError,
                ProcessPacketState.RetryUnchanged => PacketStatus.Enqueued,
                _ => isSuccessStatusCode ? PacketStatus.Processed : PacketStatus.FatalError
            };

            await _httpPacketTransfer.PacketRepository.UpdatePacketStatusAsync(packet.Id, status, CancellationToken);

            if (!isSuccessStatusCode || status is PacketStatus.Error or PacketStatus.FatalError)
            {
                var responseContent = Encoding.UTF8.GetString(response.Content.Span);
                var errorMessage = $"Request failed with status code {response.Http?.StatusCode.ToString() ?? "<NO STATUS CODE>"}. Response Content: {responseContent}";

                await _httpPacketTransfer.PacketRepository.AddAsync(new PacketData(Encoding.UTF8.GetBytes(errorMessage), $"{packet.Channel}:Error", PacketStatus.FatalError, packet.Id), CancellationToken);
            }

            // Remove the response error message if returning a successful status.
            if (isSuccessStatusCode && status == PacketStatus.Error)
            {
                response = new ConnectorResponse(ReadOnlyMemory<byte>.Empty, MediaTypeNames.Application.Octet, response.ConnectorName, response.Http, response.PacketTransfer);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process packet {id}.", packet.Id);

            await _httpPacketTransfer.PacketRepository.AddAsync(new PacketData(Encoding.UTF8.GetBytes(ex.Message), $"{packet.Channel}:Error", PacketStatus.FatalError, packet.Id), CancellationToken);
            await _httpPacketTransfer.PacketRepository.UpdatePacketStatusAsync(packet.Id, PacketStatus.FatalError, CancellationToken);

            return ConnectorResponses.HttpProblem($"Failed to process packet {packet.Id}.", _metadata.TemplateName, StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    private void SetHttpConnectorOptionDefaults()
    {
        if (_httpConnector.HttpConnectorOptions.IncomingHttpRequestHandler is null)
        {
            _httpConnector.HttpConnectorOptions.SetIncomingHttpRequestHandler(ExecuteIncomingHttpRequest);
        }

        if (_httpConnector.HttpConnectorOptions.OutgoingHttpRequestHandler is null)
        {
            _httpConnector.HttpConnectorOptions.SetOutgoingHttpRequestHandler(ExecuteOutgoingHttpRequest);
        }
    }

    private void SetHttpPacketTransferOptionDefaults()
    {
        if (_httpPacketTransfer is null)
        {
            return;
        }

        _httpPacketTransfer.HttpPacketTransferOptions.IncomingChannel ??= "Incoming";
        _httpPacketTransfer.HttpPacketTransferOptions.OutgoingChannel ??= "Outgoing";

        if (_httpPacketTransfer.HttpPacketTransferOptions.PacketProcessingHandler is null)
        {
            _httpPacketTransfer.HttpPacketTransferOptions.SetPacketProcessingHandler(ProcessPacket);
        }

        if (_httpPacketTransfer.HttpPacketTransferOptions.IncomingPacketProcessingHandler is null)
        {
            _httpPacketTransfer.HttpPacketTransferOptions.SetIncomingPacketProcessingHandler(ProcessIncomingPacket);
        }

        if (_httpPacketTransfer.HttpPacketTransferOptions.OutgoingPacketProcessingHandler is null)
        {
            _httpPacketTransfer.HttpPacketTransferOptions.SetOutgoingPacketProcessingHandler(ProcessOutgoingPacket);
        }
    }

    private void InterpolateHttpEndpointConfigs()
    {
        if (_httpIncomingConfig?.Endpoints is not null)
        {
            foreach (var (key, endpoint) in _httpIncomingConfig.Endpoints)
            {
                if (!string.IsNullOrEmpty(endpoint.Topic))
                {
                    endpoint.Topic = InterpolateValue(endpoint.Topic, key, endpoint);
                }

                if (!string.IsNullOrEmpty(endpoint.Path))
                {
                    endpoint.Path = InterpolateValue(endpoint.Path, key, endpoint);
                }
            }
        }

        if (_httpOutgoingConfig?.Endpoints is not null)
        {
            foreach (var (key, endpoint) in _httpOutgoingConfig.Endpoints)
            {
                if (!string.IsNullOrEmpty(endpoint.Topic))
                {
                    endpoint.Topic = InterpolateValue(endpoint.Topic, key, endpoint);
                }

                if (!string.IsNullOrEmpty(endpoint.Path))
                {
                    endpoint.Path = InterpolateValue(endpoint.Path, key, endpoint);
                }
            }
        }
    }

    private string InterpolateValue(string value, string endpointKey, HttpConnectorEndpointConfig endpoint) => value
        .Replace("{path}", endpoint.Path ?? "", StringComparison.OrdinalIgnoreCase)
        .Replace("{topic}", endpoint.Topic ?? "", StringComparison.OrdinalIgnoreCase)
        .Replace("{httpMethod}", endpoint.HttpMethod ?? "", StringComparison.OrdinalIgnoreCase)
        .Replace("{api}", endpoint.Api ?? "", StringComparison.OrdinalIgnoreCase)
        .Replace("{endpointKey}", endpointKey ?? "", StringComparison.OrdinalIgnoreCase)
        .Replace("{connectorName}", _metadata.TemplateName, StringComparison.OrdinalIgnoreCase)
        .Replace("{connectorType}", _connectorTemplate.Type, StringComparison.OrdinalIgnoreCase);


    public async ValueTask DisposeAsync()
    {
        if (_cancellationTokenSource is null)
        {
            return;
        }

        await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = null;

        await _regToken.DisposeAsync();

        if (_host is { })
        {
            await _host.StopAsync();
            await _host.DisposeAsync();
        }
    }
}
