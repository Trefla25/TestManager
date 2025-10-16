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
using System.Text;
using eMessenger;
using eMessenger.Tests;

namespace eHub.Tests.Connectors.Http;

[TestClass]
public class OutgoingPacketTests
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly List<PacketData> _packets = [];

    private HttpConnectorFeature _httpConnectorFeature = default!;
    private ConnectorMetadata _metadata = default!;
    private ConnectorTemplate _template = default!;
    private HttpOutgoing _httpOutgoingConfig = default!;

    private IHttpPacketTransfer _httpPacketTransfer = default!;
    private IPacketRepository _packetRepository = default!;
    private IPacketCreateBuilder _packetCreateBuilder = default!;
    private IPacketUpdateBuilder _packetUpdateBuilder = default!;
    private IPacketQueryBuilder _packetQueryBuilder = default!;
    private IServiceProvider _serviceProvider = default!;
    private ILoggerFactory _loggerFactory = default!;
    private ILogger<HttpConnectorFeature> _logger = default!;
    private IConfiguration _configuration = default!;
    private IScopedMessenger _messenger = default!;
    private IHttpClientFactory _httpClientFactory = default!;

    [TestInitialize]
    public void TestInitialize()
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

        _metadata = new ConnectorMetadata(new MessagingContext(), "TestHttpConnector", "TestHttpConnector");

        _httpOutgoingConfig = new HttpOutgoing { Endpoints = [] };

        _template = new ConnectorTemplate()
        {
            Type = "TestHttpConnector",
            Enabled = true,
            HttpOutgoing = _httpOutgoingConfig,
        };
    }

    [TestMethod]
    public async Task ProcessPacket_WhenResponseIs200_SetsProcessed()
    {
        ValueTask<ConnectorResponse> ExecuteOutgoingRequest(ConnectorRequest r, HttpConnectorEndpointConfig e, CancellationToken c)
           => ValueTask.FromResult(ConnectorResponses.HttpOk(_metadata.TemplateName));

        _template.HttpIncoming = null;
        var requestContent = "{\"test\":\"request-data\"}";
        var outgoingChannel = "TestOutgoingChannel";

        var response = await ProcessOutgoingPacketAsync(requestContent, outgoingChannel, ExecuteOutgoingRequest);

        // Verify the response
        response.Http.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(200);

        // Verify the packet was created correctly
        _packets.Should().ContainSingle();

        var packet = _packets.Single();
        packet.BinaryData.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(requestContent));
        packet.Channel.Should().Be(outgoingChannel);
        packet.Status.Should().Be(PacketStatus.Processed);
    }

    [TestMethod]
    public async Task ProcessPacket_WhenResponseIs400_SetsFatalError()
    {
        ValueTask<ConnectorResponse> ExecuteOutgoingRequest(ConnectorRequest r, HttpConnectorEndpointConfig e, CancellationToken c)
           => ValueTask.FromResult(ConnectorResponses.HttpBadRequest(_metadata.TemplateName, "Some message"));

        _template.HttpIncoming = null;
        var requestContent = "{\"test\":\"request-data\"}";
        var outgoingChannel = "TestOutgoingChannel";

        var response = await ProcessOutgoingPacketAsync(requestContent, outgoingChannel, ExecuteOutgoingRequest);

        // Verify the response
        response.Http.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(400);

        var expectedContent = "{\"type\":\"https://httpstatuses.com/400\",\"title\":\"Bad Request\",\"status\":400,\"detail\":\"Some message\"}";
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(expectedContent));

        // Verify the packet was created correctly
        _packets.Should().HaveCount(2);

        var packet = _packets.First();
        var childPacket = _packets[1];

        packet.BinaryData.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(requestContent));
        packet.Channel.Should().Be(outgoingChannel);
        packet.Status.Should().Be(PacketStatus.FatalError);

        var binaryData = $"Request failed with status code 400. Response Content: {expectedContent}";

        childPacket.BinaryData.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(binaryData));
        childPacket.ParentId.Should().Be(packet.Id);
        childPacket.Channel.Should().Be(outgoingChannel + ":Error");
        childPacket.Status.Should().Be(PacketStatus.FatalError);
    }

    [TestMethod]
    public async Task ProcessPacket_WhenResponseIs202AndRetry_KeepsEnqueued()
    {
        ValueTask<ConnectorResponse> ExecuteOutgoingRequest(ConnectorRequest r, HttpConnectorEndpointConfig e, CancellationToken c)
            => ValueTask.FromResult(ConnectorResponses.HttpPacketTransferState(_metadata.TemplateName, 202, ProcessPacketState.Retry));

        _template.HttpIncoming = null;
        var requestContent = "{\"test\":\"request-data\"}";
        var outgoingChannel = "TestOutgoingChannel";

        var response = await ProcessOutgoingPacketAsync(requestContent, outgoingChannel, ExecuteOutgoingRequest);

        // Verify the response
        response.Http.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(202);

        // Verify the packet was created correctly
        _packets.Should().ContainSingle();

        var packet = _packets.Single();
        packet.BinaryData.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(requestContent));
        packet.Channel.Should().Be(outgoingChannel);
        packet.Status.Should().Be(PacketStatus.Enqueued);
    }

    [TestMethod]
    public async Task ProcessPacket_WhenResponseIs406AndSuccess_SetsProcessed()
    {
        ValueTask<ConnectorResponse> ExecuteOutgoingRequest(ConnectorRequest r, HttpConnectorEndpointConfig e, CancellationToken c)
           => ValueTask.FromResult(ConnectorResponses.HttpPacketTransferState(_metadata.TemplateName, 406, ProcessPacketState.Success));

        _template.HttpIncoming = null;
        var requestContent = "{\"test\":\"request-data\"}";
        var outgoingChannel = "TestOutgoingChannel";

        var response = await ProcessOutgoingPacketAsync(requestContent, outgoingChannel, ExecuteOutgoingRequest);

        // Verify the response
        response.Http.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(406);

        _packets.Should().HaveCount(2);

        var packet = _packets.First();
        var childPacket = _packets[1];

        packet.BinaryData.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(requestContent));
        packet.Channel.Should().Be(outgoingChannel);
        packet.Status.Should().Be(PacketStatus.Processed);

        var binaryData = "Request failed with status code 406. Response Content: ";

        childPacket.BinaryData.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(binaryData));
        childPacket.ParentId.Should().Be(packet.Id);
        childPacket.Channel.Should().Be(outgoingChannel + ":Error");
        childPacket.Status.Should().Be(PacketStatus.FatalError);
    }


    [TestMethod]
    public async Task ProcessPacket_WhenResponseIs202AndError_SetsError()
    {
        ValueTask<ConnectorResponse> ExecuteOutgoingRequest(ConnectorRequest r, HttpConnectorEndpointConfig e, CancellationToken c)
           => ValueTask.FromResult(ConnectorResponses.HttpPacketTransferProblem("Some Title", _metadata.TemplateName, 202, ProcessPacketState.Error, "Some message"));

        _template.HttpIncoming = null;
        var requestContent = "{\"test\":\"request-data\"}";
        var outgoingChannel = "TestOutgoingChannel";

        var response = await ProcessOutgoingPacketAsync(requestContent, outgoingChannel, ExecuteOutgoingRequest);

        // Verify the response
        response.Http.Should().NotBeNull();
        response.Http!.StatusCode.Should().Be(202);

        // The response content will not be returned to the client
        response.Content.ToArray().Should().BeEmpty();
        response.ContentType.Should().Be(MediaTypeNames.Application.Octet);

        // Verify the packet was created correctly
        _packets.Should().HaveCount(2);

        var packet = _packets.First();
        var childPacket = _packets[1];

        packet.BinaryData.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(requestContent));
        packet.Channel.Should().Be(outgoingChannel);
        packet.Status.Should().Be(PacketStatus.Error);

        var binaryData = "Request failed with status code 202. Response Content: {\"type\":\"https://httpstatuses.com/202\",\"title\":\"Some Title\",\"status\":202,\"detail\":\"Some message\"}";
        childPacket.BinaryData.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(binaryData));
        childPacket.ParentId.Should().Be(packet.Id);
        childPacket.Channel.Should().Be(outgoingChannel + ":Error");
        childPacket.Status.Should().Be(PacketStatus.FatalError);
    }

    [TestMethod]
    public async Task ProcessPacket_WhenSuccessCodeAndError_SetsError()
    {
        ValueTask<ConnectorResponse> ExecuteOutgoingRequest(ConnectorRequest r, HttpConnectorEndpointConfig e, CancellationToken c)
           => ValueTask.FromResult(ConnectorResponses.HttpPacketTransferContent("Some Message", "plain/text", _metadata.TemplateName, 289, ProcessPacketState.Error));

        _template.HttpIncoming = null;
        var requestContent = "{\"test\":\"request-data\"}";
        var outgoingChannel = "TestOutgoingChannel";

        var response = await ProcessOutgoingPacketAsync(requestContent, outgoingChannel, ExecuteOutgoingRequest);

        // Verify the response
        response.Http.Should().NotBeNull();
        response.Http!.StatusCode.Should().Be(289);
        response.Content.ToArray().Should().BeEmpty();

        // Verify the packet was created correctly
        _packets.Should().HaveCount(2);

        var packet = _packets.First();
        var childPacket = _packets[1];

        packet.BinaryData.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(requestContent));
        packet.Channel.Should().Be(outgoingChannel);
        packet.Status.Should().Be(PacketStatus.Error);

        var binaryData = "Request failed with status code 289. Response Content: Some Message";

        childPacket.BinaryData.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(binaryData));
        childPacket.ParentId.Should().Be(packet.Id);
        childPacket.Channel.Should().Be(outgoingChannel + ":Error");
        childPacket.Status.Should().Be(PacketStatus.FatalError);
    }

    [TestMethod]
    public async Task ProcessPacket_WhenOverridden_UseCustomHandler()
    {
        // Initialize HttpConnectorFeature
        var endpoint = new HttpConnectorEndpointConfig { Path = "/api/test", HttpMethod = "POST", Topic = "TestTopic" };
        _httpOutgoingConfig.Endpoints.Add("TestEndpoint", endpoint);
        var request = new ConnectorRequest
        {
            ContentType = MediaTypeNames.Application.Json,
            Content = Encoding.UTF8.GetBytes("{\"test\":\"request-data\"}"),
            ConnectorName = "TestHttpConnector"
        };

        var outgoingChannel = "TestOutgoingChannel";
        var httpOutgoingHandlerWasInvoked = false;
        var options = new HttpPacketTransferOptions(outgoingChanel: outgoingChannel, processOutgoingPacketDelegate: (p, c) =>
        {
            httpOutgoingHandlerWasInvoked = true;
            return ValueTask.FromResult(ConnectorResponses.HttpOk("TestHttpConnector"));
        });

        _httpPacketTransfer.HttpPacketTransferOptions.Returns(options);
        _httpPacketTransfer.HttpConnectorOptions.Returns(options);

        _httpConnectorFeature = new HttpConnectorFeature(_serviceProvider, _logger, _loggerFactory, _configuration, _messenger, _httpClientFactory, _httpPacketTransfer, _metadata, _template);

        // Start the feature
        await _httpConnectorFeature.StartAsync(_cancellationTokenSource.Token);

        // Send outgoing connector request
        var responses = await _messenger.AskAsync<ConnectorRequest, ConnectorResponse>(endpoint.Topic, request);
        responses.Should().ContainSingle();

        // Verify the response
        httpOutgoingHandlerWasInvoked.Should().BeTrue();
        var response = responses.Single();
        response.Should().NotBeNull();
        response.Http.Should().NotBeNull();
        response.Http!.StatusCode.Should().Be(200);

        // Verify the packet was created correctly
        _packets.Should().ContainSingle();

        var packet = _packets.Single();
        packet.BinaryData.ToArray().Should().BeEquivalentTo(request.Content.ToArray());
        packet.Channel.Should().Be(outgoingChannel);
        packet.Status.Should().Be(PacketStatus.Processed);
    }

    private async Task<ConnectorResponse> ProcessOutgoingPacketAsync(string requestContent, string outgoingChanel, ExecuteOutgoingHttpRequestDelegate? handler = null)
    {
        // Set up your endpoint configuration
        var endpoint = new HttpConnectorEndpointConfig
        {
            Path = "/api/test",
            HttpMethod = "POST",
            Topic = "TestTopic"
        };

        _httpOutgoingConfig.Endpoints.Add("TestEndpoint", endpoint);

        var request = new ConnectorRequest
        {
            ContentType = MediaTypeNames.Application.Json,
            Content = Encoding.UTF8.GetBytes(requestContent),
            ConnectorName = _metadata.TemplateName
        };

        // Configure the options with the given handler delegate
        var options = new HttpPacketTransferOptions(
            outgoingChanel: outgoingChanel,
            outgoingHttpRequestHandler: handler
        );

        _httpPacketTransfer.HttpPacketTransferOptions.Returns(options);
        _httpPacketTransfer.HttpConnectorOptions.Returns(options);

        // Initialize the HttpConnectorFeature
        _httpConnectorFeature = new HttpConnectorFeature(
            _serviceProvider, _logger, _loggerFactory, _configuration, _messenger,
            _httpClientFactory, _httpPacketTransfer, _metadata, _template);

        // Start the feature
        await _httpConnectorFeature.StartAsync(_cancellationTokenSource.Token);

        var response = await _messenger.AskAsync<ConnectorRequest, ConnectorResponse>(endpoint.Topic, request);

        response.Should().ContainSingle();

        return response.First();
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
            .Returns(x => [.. _packets]);

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
            .Returns(x => [.. _packets]);

        _packetRepository.Create().Returns(_packetCreateBuilder);
        _packetRepository.Update().Returns(_packetUpdateBuilder);
        _packetRepository.Query().Returns(_packetQueryBuilder);
    }

    [TestCleanup]
    public async Task TestCleanup()
    {
        await _cancellationTokenSource.CancelAsync();
    }
}
