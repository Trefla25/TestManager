using eHub.Config;
using eHub.PlugIn;
using eHub.Scripting.Connectors.Features;
using eHub.Scripting.Connectors;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using eMessenger;
using Microsoft.AspNetCore.TestHost;
using NSubstitute;
using eMessenger.Tests;
using Microsoft.AspNetCore.Http;
using eHub.PlugIn.Communication;
using System.Net.Mime;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Primitives;
using eHub.Tests.Helper;

namespace eHub.Tests.Connectors.Http;

public class OutgoingTests : IAsyncLifetime
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private WebApplication? _server;
    private HttpConnectorFeature _httpConnectorFeature = null!;
    private ConnectorMetadata _metadata = null!;
    private ConnectorTemplate _template = null!;
    private HttpOutgoing _httpOutgoingConfig = null!;

    private IHttpConnector _httpConnector = null!;
    private IServiceProvider _serviceProvider = null!;
    private ILoggerFactory _loggerFactory = null!;
    private ILogger<HttpConnectorFeature> _logger = null!;
    private IConfiguration _configuration = null!;
    private IScopedMessenger _messenger = null!;
    private IHttpClientFactory _httpClientFactory = null!;

    public ValueTask InitializeAsync()
    {
        _httpConnector = Substitute.For<IHttpConnector>();
        _serviceProvider = Substitute.For<IServiceProvider>();
        _configuration = Substitute.For<IConfiguration>();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();

        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = _loggerFactory.CreateLogger<HttpConnectorFeature>();
        _messenger = Substitute.For<IScopedMessenger>();

        _metadata = new ConnectorMetadata(new MessagingContext(), "TestHttpConnector", "TestHttpConnectorType");

        _httpOutgoingConfig = new HttpOutgoing { Endpoints = [] };

        _template = new ConnectorTemplate()
        {
            Type = "TestHttpConnectorType",
            Enabled = true,
            HttpOutgoing = _httpOutgoingConfig
        };

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task StartAsync_WhenCalled_RegistersAllEndpoints()
    {
        // Arrange
        var endpoints = new HttpConnectorEndpointConfig[]
        {
            new() { Path = "/api/test", HttpMethod = "POST", Topic = "PostEndpoint" },
            new() { Path = "/api/test", HttpMethod = "GET", Topic = "GetEndpoint" },
            new() { Path = "/api/test", HttpMethod = "PUT", Topic = "PutEndpoint" },
            new() { Path = "/api/test", HttpMethod = "DELETE", Topic = "DeleteEndpoint" },
            new() { Path = "/api/test", HttpMethod = "PATCH", Topic = "PatchEndpoint" },
            new() { Topic = "EmptyEndpoint" },
        };
        
        _httpOutgoingConfig.Endpoints.AddRange(endpoints.Select(e => KeyValuePair.Create(e.Topic!, e)));

        _httpConnector.HttpConnectorOptions.Returns(new HttpConnectorOptions());

        _httpConnectorFeature = new HttpConnectorFeature(_serviceProvider, _logger, _loggerFactory, _configuration,
            _messenger, _httpClientFactory, _httpConnector, _metadata, _template);

        // Act
        await _httpConnectorFeature.StartAsync(_cancellationTokenSource.Token);
        
        // Assert
        // Verify that AnswerAsync was called for each expected topic
        foreach (var endpoint in endpoints)
        {
            await _messenger.Received(1).AnswerAsync(endpoint.Topic!,
                Arg.Any<Func<ConnectorRequest, ValueTask<ConnectorResponse>>>());
        }
    }

    [Fact]
    public async Task ExecuteRequest_OnPost_ReturnsExpectedResponse()
    {
        // Arrange
        var request = new ConnectorRequest(
            content: Encoding.UTF8.GetBytes("{\"test\":\"post-data\"}"),
            contentType: MediaTypeNames.Application.Json,
            connectorName: "MyConnector");
        
        // Act & Assert
        await TestOutgoingHttpRequestAsync(request, HttpMethods.Post);
    }

    [Fact]
    public async Task ExecuteRequest_OnGet_ReturnsExpectedResponse()
    {
        // Arrange
        var request = new ConnectorRequest(
            content: ReadOnlyMemory<byte>.Empty,
            contentType: MediaTypeNames.Application.Octet,
            connectorName: "MyConnector");
        
        // Act & Assert
        await TestOutgoingHttpRequestAsync(request, HttpMethods.Get);
    }

    [Fact]
    public async Task ExecuteRequest_OnPut_ReturnsExpectedResponse()
    {
        // Arrange
        var request = new ConnectorRequest(
            content: Encoding.UTF8.GetBytes("{\"test\":\"put-data\"}"),
            contentType: MediaTypeNames.Application.Json,
            connectorName: "MyConnector");
        
        // Act & Assert
        await TestOutgoingHttpRequestAsync(request, HttpMethods.Put);
    }

    [Fact]
    public async Task ExecuteRequest_OnDelete_ReturnsExpectedResponse()
    {
        // Arrange
        var request = new ConnectorRequest(
            content: ReadOnlyMemory<byte>.Empty,
            contentType: MediaTypeNames.Application.Octet,
            connectorName: "MyConnector");
        
        // Act & Assert
        await TestOutgoingHttpRequestAsync(request, HttpMethods.Delete);
    }

    [Fact]
    public async Task ExecuteRequest_OnPatch_ReturnsExpectedResponse()
    {
        // Arrange
        var request = new ConnectorRequest(
            content: Encoding.UTF8.GetBytes("{\"test\":\"patch-data\"}"),
            contentType: MediaTypeNames.Application.Json,
            connectorName: "MyConnector");
        
        // Act & Assert
        await TestOutgoingHttpRequestAsync(request, HttpMethods.Patch);
    }

    [Fact]
    public async Task ExecuteRequest_WithHeaders_AllAreForwarded()
    {
        // Arrange
        var headers = new Dictionary<string, StringValues>
        {
            { "Token", Guid.NewGuid().ToString() },
            { "Content-Type", "application/json" },
            { "X-Test-Header", "test-value" },
            { "X-Multiple-Header", new StringValues(["value1", "value2", "value3"]) },
        };
        
        var request = new ConnectorRequest(
            content: Encoding.UTF8.GetBytes("{\"test\":\"test-data\"}"),
            contentType: MediaTypeNames.Application.Json,
            connectorName: "MyConnector",
            http: new("/api/test", "POST", null, headers));
        
        // Act & Assert
        await TestOutgoingHttpRequestAsync(request, HttpMethods.Post);
    }

    [Fact]
    public async Task ExecuteRequest_WithQuery_AllAreForwarded()
    {
        // Arrange
        var query = new Dictionary<string, StringValues>
        {
            { "search", "example" },
            { "sort", "packets" },
            { "multi", new StringValues(["one", "two", "three"]) }
        };

        var request = new ConnectorRequest(
            content: Encoding.UTF8.GetBytes(@"{ ""test"": ""query-test"" }"),
            contentType: MediaTypeNames.Application.Json,
            connectorName: "MyConnector",
            http: new("/api/test", "POST", null, null, query)
        );
        
        // Act & Assert
        await TestOutgoingHttpRequestAsync(request, HttpMethods.Post);
    }

    [Fact]
    public async Task ExecuteRequest_WithRouteParameters_AllAreForwarded()
    {
        // Arrange
        var routeParameters = new Dictionary<string, string>()
        {
            { "param1", "2" },
            { "param2", "TestConnector" },
        };

        var request = new ConnectorRequest(
            content: Encoding.UTF8.GetBytes("{\"test\":\"test-data\"}"),
            contentType: MediaTypeNames.Application.Json,
            connectorName: "MyConnector",
            http: new("/api/test/2/TestConnector", "POST", routeParameters));
        
        // Act & Assert
        await TestOutgoingHttpRequestAsync(request, HttpMethods.Post, "/api/test/{param1}/{param2}");
    }

    [Fact]
    public async Task ExecuteRequest_WithCommunicationErrorResend_Returns202AndError()
    {
        // Arrange
        var connectorRequest = new ConnectorRequest(
            content: Encoding.UTF8.GetBytes("{\"test\":\"test-data\"}"),
            contentType: MediaTypeNames.Application.Json,
            connectorName: "TestHttpConnector");

        // Initialize HttpConnectorFeature
        var unusedPort = NetworkHelper.GetRandomUnusedPort();
        _httpOutgoingConfig.Endpoints.Add("TestEndpoint",
            new() { Topic = "TestTopic", Path = $"http://localhost:{unusedPort}/test", HttpMethod = "POST" });
        _httpOutgoingConfig.Endpoints["TestEndpoint"].ResendPacketsOnCommunicationError = true;

        var failingHttpClient = new HttpClient(new FailingHttpMessageHandler());
        _httpClientFactory.CreateClient(Arg.Any<string>()).Returns(failingHttpClient);
        _messenger = TestingMessenger.CreateScoped(_loggerFactory);
        _httpConnector.HttpConnectorOptions.Returns(new HttpConnectorOptions());
        _httpConnectorFeature = new HttpConnectorFeature(_serviceProvider, _logger, _loggerFactory, _configuration,
            _messenger, _httpClientFactory, _httpConnector, _metadata, _template);

        // Start the feature
        await _httpConnectorFeature.StartAsync(_cancellationTokenSource.Token);
        
        // Act
        // Send outgoing connector request
        var responses = await _messenger.AskAsync<ConnectorRequest, ConnectorResponse>("TestTopic", connectorRequest);
        
        // Assert
        responses.Should().ContainSingle();

        var response = responses.Single();
        response.Http.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(202);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Error);
    }

    [Fact]
    public async Task ExecuteRequest_WithoutCommunicationErrorResend_Returns502()
    {
        // Arrange
        var connectorRequest = new ConnectorRequest(
            content: Encoding.UTF8.GetBytes("{\"test\":\"test-data\"}"),
            contentType: MediaTypeNames.Application.Json,
            connectorName: "TestHttpConnector");

        // Initialize HttpConnectorFeature
        var unusedPort = NetworkHelper.GetRandomUnusedPort();
        _httpOutgoingConfig.Endpoints.Add("TestEndpoint",
            new() { Topic = "TestTopic", Path = $"http://localhost:{unusedPort}/test", HttpMethod = "POST" });
        _httpOutgoingConfig.Endpoints["TestEndpoint"].ResendPacketsOnCommunicationError = false;

        var failingHttpClient = new HttpClient(new FailingHttpMessageHandler());
        _httpClientFactory.CreateClient(Arg.Any<string>()).Returns(failingHttpClient);
        _messenger = TestingMessenger.CreateScoped(_loggerFactory);
        _httpConnector.HttpConnectorOptions.Returns(new HttpConnectorOptions());
        _httpConnectorFeature = new HttpConnectorFeature(_serviceProvider, _logger, _loggerFactory, _configuration,
            _messenger, _httpClientFactory, _httpConnector, _metadata, _template);

        // Start the feature
        await _httpConnectorFeature.StartAsync(_cancellationTokenSource.Token);
        
        // Act
        // Send outgoing connector request
        var responses = await _messenger.AskAsync<ConnectorRequest, ConnectorResponse>("TestTopic", connectorRequest);
        
        // Assert
        responses.Should().ContainSingle();

        var response = responses.Single();
        response.Http.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(502);
    }

    [Fact]
    public async Task ExecuteRequest_WhenOverridden_UseCustomHandler()
    {
        // Arrange
        var connectorRequest = new ConnectorRequest(
            content: Encoding.UTF8.GetBytes("{\"test\":\"test-data\"}"),
            contentType: MediaTypeNames.Application.Json,
            connectorName: "TestHttpConnector");

        // Initialize HttpConnectorFeature
        _httpOutgoingConfig.Endpoints.Add("TestEndpoint", new() { Topic = "TestTopic" });
        _messenger = TestingMessenger.CreateScoped(_loggerFactory);

        var httpOutgoingHandlerWasInvoked = false;
        _httpConnector.HttpConnectorOptions.Returns(new HttpConnectorOptions(outgoingHttpRequestHandler: (r, e, c) =>
        {
            httpOutgoingHandlerWasInvoked = true;
            return ValueTask.FromResult(ConnectorResponses.HttpOk("TestHttpConnector"));
        }));

        _httpConnectorFeature = new HttpConnectorFeature(_serviceProvider, _logger, _loggerFactory, _configuration,
            _messenger, _httpClientFactory, _httpConnector, _metadata, _template);

        // Start the feature
        await _httpConnectorFeature.StartAsync(_cancellationTokenSource.Token);
        
        // Act
        // Send outgoing connector request
        var responses = await _messenger.AskAsync<ConnectorRequest, ConnectorResponse>("TestTopic", connectorRequest);
        
        // Assert
        responses.Should().ContainSingle();

        var response = responses.Single();
        response.Should().NotBeNull();
        response.Http.Should().NotBeNull();

        // Verify the response
        httpOutgoingHandlerWasInvoked.Should().BeTrue();
        response.Http!.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task StartAsync_WithInterpolatedEndpoints_FillsPlaceholders()
    {
        // Arrange
        var endpoint = new HttpConnectorEndpointConfig
        {
            Path = "/api/{endpointKey}/{connectorName}",
            HttpMethod = HttpMethods.Post,
            Topic = "topic/{endpointKey}/{connectorName}"
        };

        _httpOutgoingConfig.Endpoints.Add("TestEndpoint", endpoint);
        _httpConnector.HttpConnectorOptions.Returns(new HttpConnectorOptions());

        // Initialize and start HttpConnectorFeature
        _httpConnectorFeature = new HttpConnectorFeature(_serviceProvider, _logger, _loggerFactory, _configuration,
            _messenger, _httpClientFactory, _httpConnector, _metadata, _template);
        
        // Act
        await _httpConnectorFeature.StartAsync(_cancellationTokenSource.Token);
        
        // Assert
        _httpOutgoingConfig.Endpoints["TestEndpoint"].Topic.Should().Be("topic/TestEndpoint/TestHttpConnector");
        _httpOutgoingConfig.Endpoints["TestEndpoint"].Path.Should().Be("api/TestEndpoint/TestHttpConnector");
    }

    [Fact]
    public async Task StartAsync_WithAllPlaceholders_FillsPlaceholders()
    {
        // Arrange
        var endpoint = new HttpConnectorEndpointConfig
        {
            Path = "/api/{endpointKey}/{topic}/{api}/{httpMethod}/{connectorName}/{connectorType}",
            HttpMethod = HttpMethods.Post,
            Topic = "topic/{endpointKey}/{api}/{httpMethod}/{connectorName}/{connectorType}",
            Api = "TestApi"
        };

        _httpOutgoingConfig.Endpoints.Add("TestEndpoint", endpoint);
        _httpConnector.HttpConnectorOptions.Returns(new HttpConnectorOptions());

        // Initialize and start HttpConnectorFeature
        _httpConnectorFeature = new HttpConnectorFeature(_serviceProvider, _logger, _loggerFactory, _configuration,
            _messenger, _httpClientFactory, _httpConnector, _metadata, _template);
        
        // Act
        await _httpConnectorFeature.StartAsync(_cancellationTokenSource.Token);
        
        // Assert
        _httpOutgoingConfig.Endpoints["TestEndpoint"].Topic.Should()
            .Be("topic/TestEndpoint/TestApi/POST/TestHttpConnector/TestHttpConnectorType");
        _httpOutgoingConfig.Endpoints["TestEndpoint"].Path.Should().Be(
            "api/TestEndpoint/topic/TestEndpoint/TestApi/POST/TestHttpConnector/TestHttpConnectorType/TestApi/POST/TestHttpConnector/TestHttpConnectorType");
    }

    private async Task TestOutgoingHttpRequestAsync(ConnectorRequest request, string httpMethod, string? path = null)
    {
        // Initialize test server
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        _server = builder.Build();

        const int responseStatus = StatusCodes.Status200OK;
        const string responseContentType = MediaTypeNames.Application.Json;
        const string responseContent = "{\"test\":\"response-data\"}";

        path ??= "/api/test";
        _server.MapMethods(path, [httpMethod], async (context) =>
        {
            using var memoryStream = new MemoryStream();
            await context.Request.Body.CopyToAsync(memoryStream);
            var contentBytes = memoryStream.ToArray();

            context.Request.ContentType.Should().Be(request.ContentType);
            contentBytes.Should().BeEquivalentTo(request.Content.ToArray());
            foreach (var header in request.Http?.Headers ?? new Dictionary<string, StringValues>())
            {
                context.Request.Headers[header.Key].Should().BeEquivalentTo(header.Value);
            }

            foreach (var routeParameter in request.Http?.RouteParameters ?? new Dictionary<string, string>())
            {
                context.Request.RouteValues[routeParameter.Key].Should().Be(routeParameter.Value);
            }

            foreach (var queryParameter in request.Http?.Query ?? new Dictionary<string, StringValues>())
            {
                context.Request.Query[queryParameter.Key].Should().BeEquivalentTo(queryParameter.Value);
            }

            context.Response.StatusCode = responseStatus;
            context.Response.ContentType = responseContentType;
            await context.Response.WriteAsync(responseContent);
        });

        // Start test server
        await _server.StartAsync(_cancellationTokenSource.Token);

        // Initialize HttpConnectorFeature
        _httpOutgoingConfig.Endpoints.Add($"{httpMethod}_Endpoint",
            new() { Path = path, HttpMethod = httpMethod, Topic = $"{httpMethod}_Topic" });

        _messenger = TestingMessenger.CreateScoped(_loggerFactory);
        _httpConnector.HttpConnectorOptions.Returns(new HttpConnectorOptions());
        _httpClientFactory.CreateClient(Arg.Any<string>()).Returns(_server.GetTestClient());

        _httpConnectorFeature = new HttpConnectorFeature(_serviceProvider, _logger, _loggerFactory, _configuration,
            _messenger, _httpClientFactory, _httpConnector, _metadata, _template);

        // Start HttpConnectorFeature
        await _httpConnectorFeature.StartAsync(_cancellationTokenSource.Token);

        // Send outgoing connector request
        var responses = await _messenger.AskAsync<ConnectorRequest, ConnectorResponse>($"{httpMethod}_Topic", request);
        responses.Should().ContainSingle();

        var response = responses.Single();
        response.Should().NotBeNull();
        response.Http.Should().NotBeNull();

        // Verify the response
        response.Http!.StatusCode.Should().Be(responseStatus);
        response.ContentType.Should().Be(responseContentType);
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(responseContent));
    }

    public async ValueTask DisposeAsync()
    {
        if (_server is not null)
        {
            await _server.StopAsync();
        }

        await _httpConnectorFeature.DisposeAsync();

        await _cancellationTokenSource.CancelAsync();
    }
}

public class FailingHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new HttpRequestException("Simulated failure.");
    }
}
