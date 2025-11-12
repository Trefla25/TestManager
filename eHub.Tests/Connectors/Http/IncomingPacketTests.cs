using eHub.Config;
using eHub.PlugIn.Communication;
using eHub.PlugIn;
using eHub.Scripting.Connectors.Features;
using eHub.Scripting.Connectors;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Linq.Expressions;
using System.Net.Mime;
using System.Net;
using System.Text;
using eHub.Tests.Helper;
using eMessenger;
using eMessenger.Tests;

namespace eHub.Tests.Connectors.Http;

public class IncomingPacketTests : IAsyncLifetime
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly List<PacketData> _packets = [];

    private HttpConnectorFeature _httpConnectorFeature = null!;
    private ConnectorMetadata _metadata = null!;
    private ConnectorTemplate _template = null!;
    private HttpIncoming _httpIncomingConfig = null!;
    private Uri _connectorUrl = null!;

    private IHttpPacketTransfer _httpPacketTransfer = null!;
    private IPacketRepository _packetRepository = null!;
    private IPacketCreateBuilder _packetCreateBuilder = null!;
    private IPacketUpdateBuilder _packetUpdateBuilder = null!;
    private IPacketQueryBuilder _packetQueryBuilder = null!;
    private IServiceProvider _serviceProvider = null!;
    private ILoggerFactory _loggerFactory = null!;
    private ILogger<HttpConnectorFeature> _logger = null!;
    private IConfiguration _configuration = null!;
    private IScopedMessenger _messenger = null!;
    private IHttpClientFactory _httpClientFactory = null!;

    public ValueTask InitializeAsync()
    {
        _httpPacketTransfer = Substitute.For<IHttpPacketTransfer>();
        _serviceProvider = Substitute.For<IServiceProvider>();
        _configuration = Substitute.For<IConfiguration>();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();

        SubstituteForPacketRepository();
        _httpPacketTransfer.PacketRepository.Returns(_packetRepository);

        var packetConverter = new HttpPacketTransferConverter();
        _httpPacketTransfer.HttpPacketConverter.Returns(packetConverter);
        _httpPacketTransfer.Converter.Returns(packetConverter);

        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = _loggerFactory.CreateLogger<HttpConnectorFeature>();
        _messenger = TestingMessenger.CreateScoped(_loggerFactory);

        _connectorUrl = new Uri($"http://localhost:{NetworkHelper.GetRandomUnusedPort()}");
        _metadata = new ConnectorMetadata(new MessagingContext(), "TestHttpConnector", "TestHttpConnector");

        _httpIncomingConfig = new HttpIncoming
        {
            Endpoints = [],
            Auth = new() { Enabled = false },
            Kestrel = new ConfigurationBuilder().AddInMemoryCollection([
                KeyValuePair.Create<string, string?>("Kestrel:Endpoints:Http:Url", _connectorUrl.ToString())
            ]).Build().GetSection("Kestrel")
        };

        _template = new ConnectorTemplate()
        {
            Type = "TestHttpConnector",
            Enabled = true,
            HttpIncoming = _httpIncomingConfig
        };
        
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ProcessPacket_WhenResponseIs200_SetsProcessed()
    {
        // Arrange
        ValueTask<ConnectorResponse> ExecuteIncomingRequestAsync(ConnectorRequest r, HttpConnectorEndpointConfig e, CancellationToken c) => ValueTask.FromResult(ConnectorResponses.HttpOk(_metadata.TemplateName));
        
        const string requestContent = "{\"test\":\"request-data\"}";
        const string incomingChannel = "TestIncomingChannel";
        
        // Act
        var response = await ProcessIncomingPacketAsync(requestContent, incomingChannel, ExecuteIncomingRequestAsync);
        
        // Assert
        // Verify the response
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify the packet was created correctly
        _packets.Should().ContainSingle();

        var packet = _packets.Single();
        packet.BinaryData.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(requestContent));
        packet.Channel.Should().Be(incomingChannel);
        packet.Status.Should().Be(PacketStatus.Processed);
    }

    [Fact]
    public async Task ProcessPacket_WhenResponseIs400_SetsFatalError()
    {
        // Arrange
        ValueTask<ConnectorResponse> ExecuteIncomingRequestAsync(ConnectorRequest r, HttpConnectorEndpointConfig e, CancellationToken c)
            => ValueTask.FromResult(ConnectorResponses.HttpBadRequest(_metadata.TemplateName, "Some message"));
        
        const string requestContent = "{\"test\":\"request-data\"}";
        const string incomingChannel = "TestIncomingChannel";

        // Act
        var response = await ProcessIncomingPacketAsync(requestContent, incomingChannel, ExecuteIncomingRequestAsync);
        
        // Assert
        // Verify the response
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType.MediaType.Should().Be(MediaTypeNames.Application.Json);

        const string expectedContent = "{\"type\":\"https://httpstatuses.com/400\",\"title\":\"Bad Request\",\"status\":400,\"detail\":\"Some message\"}";
        var responseContent = await response.Content.ReadAsStringAsync(_cancellationTokenSource.Token);
        responseContent.Should().Be(expectedContent);

        _packets.Should().HaveCount(2);

        var packet = _packets.First();
        var childPacket = _packets[1];

        packet.BinaryData.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(requestContent));
        packet.Channel.Should().Be(incomingChannel);
        packet.Status.Should().Be(PacketStatus.FatalError);

        const string binaryData = $"Request failed with status code 400. Response Content: {expectedContent}";
        childPacket.BinaryData.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(binaryData));
        childPacket.ParentId.Should().Be(packet.Id);
        childPacket.Channel.Should().Be(incomingChannel + ":Error");
        childPacket.Status.Should().Be(PacketStatus.FatalError);
    }

    [Fact]
    public async Task ProcessPacket_WhenResponseIs202AndRetry_KeepsEnqueued()
    {
        // Arrange
        ValueTask<ConnectorResponse> ExecuteIncomingRequestAsync(ConnectorRequest r, HttpConnectorEndpointConfig e, CancellationToken c)
            => ValueTask.FromResult(ConnectorResponses.HttpPacketTransferState(_metadata.TemplateName, 202, ProcessPacketState.Retry));
        
        const string requestContent = "{\"test\":\"request-data\"}";
        const string incomingChannel = "TestIncomingChannel";

        // Act
        var response = await ProcessIncomingPacketAsync(requestContent, incomingChannel, ExecuteIncomingRequestAsync);
        
        // Assert
        // Verify the response
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Verify the packet was created correctly
        _packets.Should().ContainSingle();

        var packet = _packets.Single();
        packet.BinaryData.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(requestContent));
        packet.Channel.Should().Be(incomingChannel);
        packet.Status.Should().Be(PacketStatus.Enqueued);
    }

    [Fact]
    public async Task ProcessPacket_WhenResponseIs406AndSuccess_SetsProcessed()
    {
        // Arrange
        ValueTask<ConnectorResponse> ExecuteIncomingRequestAsync(ConnectorRequest r, HttpConnectorEndpointConfig e, CancellationToken c)
           => ValueTask.FromResult(ConnectorResponses.HttpPacketTransferState(_metadata.TemplateName, 406, ProcessPacketState.Success));
        
        const string requestContent = "{\"test\":\"request-data\"}";
        const string incomingChannel = "TestIncomingChannel";

        // Act
        var response = await ProcessIncomingPacketAsync(requestContent, incomingChannel, ExecuteIncomingRequestAsync);
        
        // Assert
        // Verify the response
        response.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);

        _packets.Should().HaveCount(2);

        var packet = _packets.First();
        var childPacket = _packets[1];

        packet.BinaryData.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(requestContent));
        packet.Channel.Should().Be(incomingChannel);
        packet.Status.Should().Be(PacketStatus.Processed);

        const string binaryData = "Request failed with status code 406. Response Content: ";
        childPacket.BinaryData.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(binaryData));
        childPacket.ParentId.Should().Be(packet.Id);
        childPacket.Channel.Should().Be(incomingChannel + ":Error");
        childPacket.Status.Should().Be(PacketStatus.FatalError);
    }

    [Fact]
    public async Task ProcessPacket_WhenUnauthorizedRequest_NoPacketCreated()
    {
        Assert.NotNull(_httpIncomingConfig.Auth);
        
        // Arrange
        _httpIncomingConfig.Auth.Enabled = true;
        _httpIncomingConfig.InsertUnauthorizedPackets = false;
        _httpIncomingConfig.Auth.Basic = new() { DummyUsers = [new() { Username = "api", Password = "test" }] };
        const string requestContent = "{\"test\":\"request-data\"}";
        const string incomingChannel = "TestIncomingChannel";
        
        // Act
        var response = await ProcessIncomingPacketAsync(requestContent, incomingChannel);
        
        // Assert
        // Verify the response
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Verify that no packet was added
        _packets.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessPacket_WhenInsertUnauthorizedRequestEnabled_AddsErrorPacket()
    {
        Assert.NotNull(_httpIncomingConfig.Auth);
        
        // Arrange
        _httpIncomingConfig.Auth.Enabled = true;
        _httpIncomingConfig.InsertUnauthorizedPackets = true;
        _httpIncomingConfig.Auth.Basic = new() { DummyUsers = [new() { Username = "api", Password = "test" }] };
        const string requestContent = "{\"test\":\"request-data\"}";
        const string incomingChannel = "TestIncomingChannel";
        
        // Act
        var response = await ProcessIncomingPacketAsync(requestContent, incomingChannel);
        
        // Assert
        // Verify the response
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Verify the packet was created correctly
        _packets.Should().HaveCount(2);

        var packet = _packets.First();
        var childPacket = _packets[1];

        packet.BinaryData.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(requestContent));
        packet.Channel.Should().Be(incomingChannel);
        packet.Status.Should().Be(PacketStatus.FatalError);

        const string binaryData = "Request failed with status code 401. Response Content: ";
        childPacket.BinaryData.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(binaryData));
        childPacket.ParentId.Should().Be(packet.Id);
        childPacket.Channel.Should().Be(incomingChannel + ":Error");
        childPacket.Status.Should().Be(PacketStatus.FatalError);
    }

    [Fact]
    public async Task ProcessPacket_WhenOverridden_UseCustomHandler()
    {
        // Arrange
        _httpIncomingConfig = new HttpIncoming
        {
            Endpoints = [],
            Auth = new() { Enabled = false },
            Kestrel = new ConfigurationBuilder().AddInMemoryCollection([
                KeyValuePair.Create<string, string?>("Kestrel:Endpoints:Http:Url", _connectorUrl.ToString())
            ]).Build().GetSection("Kestrel")
        };

        _template.HttpIncoming = _httpIncomingConfig;

        // Initialize HttpConnectorFeature
        var endpoint = new HttpConnectorEndpointConfig { Path = "/api/test", HttpMethod = "POST", Topic = "TestTopic" };
        _httpIncomingConfig.Endpoints.Add("TestEndpoint", endpoint);

        const string requestContent = "{\"test\":\"request-data\"}";
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/test")
        {
            Content = new StringContent(requestContent, Encoding.UTF8, MediaTypeNames.Application.Json)
        };

        const string incomingChannel = "TestIncomingChannel";
        var httpIncomingHandlerWasInvoked = false;
        var options = new HttpPacketTransferOptions(incomingChannel: incomingChannel, processIncomingPacketDelegate: (_, _) =>
        {
            httpIncomingHandlerWasInvoked = true;
            return ValueTask.FromResult(ConnectorResponses.HttpOk("TestHttpConnector"));
        });

        _httpPacketTransfer.HttpPacketTransferOptions.Returns(options);
        _httpPacketTransfer.HttpConnectorOptions.Returns(options);

        _httpConnectorFeature = new HttpConnectorFeature(_serviceProvider, _logger, _loggerFactory, _configuration, _messenger, _httpClientFactory, _httpPacketTransfer, _metadata, _template);

        // Start HttpConnectorFeature
        await _httpConnectorFeature.StartAsync(_cancellationTokenSource.Token);

        // Send HTTP request
        var httpClient = new HttpClient() { BaseAddress = _connectorUrl };
        // Act
        using var response = await httpClient.SendAsync(request, _cancellationTokenSource.Token);
        // Assert
        // Verify the response
        httpIncomingHandlerWasInvoked.Should().BeTrue();
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify the packet was created correctly
        _packets.Should().ContainSingle();

        var packet = _packets.Single();
        packet.BinaryData.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(requestContent));
        packet.Channel.Should().Be(incomingChannel);
        packet.Status.Should().Be(PacketStatus.Processed);
    }
    private async Task<HttpResponseMessage> ProcessIncomingPacketAsync(string requestContent, string incomingChannel, ExecuteIncomingHttpRequestDelegate? handler = null)
    {
        // Set up your endpoint configuration
        var endpoint = new HttpConnectorEndpointConfig
        {
            Path = "/api/test",
            HttpMethod = "POST",
            Topic = "TestTopic"
        };
        _httpIncomingConfig.Endpoints.Add("TestEndpoint", endpoint);

        // Create the request
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/test")
        {
            Content = new StringContent(requestContent, Encoding.UTF8, MediaTypeNames.Application.Json)
        };

        // Configure the options using the provided handler
        var options = new HttpPacketTransferOptions(
            incomingChannel: incomingChannel,
            incomingHttpRequestHandler: handler
        );

        _httpPacketTransfer.HttpPacketTransferOptions.Returns(options);
        _httpPacketTransfer.HttpConnectorOptions.Returns(options);

        // Initialize and start the HttpConnectorFeature
        _httpConnectorFeature = new HttpConnectorFeature(
            _serviceProvider, _logger, _loggerFactory, _configuration,
            _messenger, _httpClientFactory, _httpPacketTransfer, _metadata, _template);

        await _httpConnectorFeature.StartAsync(_cancellationTokenSource.Token);

        // Send the HTTP request
        var httpClient = new HttpClient() { BaseAddress = _connectorUrl };
        var response = await httpClient.SendAsync(request, _cancellationTokenSource.Token);

        return response;
    }

    private void SubstituteForPacketRepository()
    {
        _packetRepository = Substitute.For<IPacketRepository>();
        _packetCreateBuilder = Substitute.For<IPacketCreateBuilder>();
        _packetUpdateBuilder = Substitute.For<IPacketUpdateBuilder>();
        _packetQueryBuilder = Substitute.For<IPacketQueryBuilder>();

        // Mock packet repository create
        _packetCreateBuilder
            .Add(Arg.Any<PacketData>())
            .Returns(_packetCreateBuilder)
            .AndDoes(x => _packets.Add(x.Arg<PacketData>()));
        _packetCreateBuilder
            .CreateAsync(Arg.Any<CancellationToken>())
            .Returns(_ => [.. _packets]);

        // Mock packet repository update
        _packetUpdateBuilder
            .Set(Arg.Any<Expression<Func<PacketData, PacketStatus>>>(), Arg.Any<PacketStatus>())
            .Returns(_packetUpdateBuilder)
            .AndDoes(x => _packets.ForEach(p => p.Status = x.Arg<PacketStatus>()));
        _packetUpdateBuilder
            .Where(Arg.Any<Expression<Func<PacketData, bool>>>())
            .Returns(_packetUpdateBuilder);
        _packetUpdateBuilder
            .ExecuteAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        // Mock packet repository query
        _packetQueryBuilder
            .Where(Arg.Any<Expression<Func<PacketData, bool>>>())
            .Returns(_packetQueryBuilder);
        _packetQueryBuilder
            .GetAsync(Arg.Any<CancellationToken>())
            .Returns(_ => [.. _packets]);

        _packetRepository.Create().Returns(_packetCreateBuilder);
        _packetRepository.Update().Returns(_packetUpdateBuilder);
        _packetRepository.Query().Returns(_packetQueryBuilder);
    }

    public async ValueTask DisposeAsync() => await _cancellationTokenSource.CancelAsync();
}
