using eHub.PlugIn.Communication;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.Net;
using System.Text.Json;
using System.Text;
using System.Net.Mime;

namespace eHub.Tests.Connectors.Http;

[TestClass]
public class HttpUtilTests
{
    private const string ConnectorName = "MyConnector";

    [TestMethod]
    public async Task GetConnectorResponseFromIResult_WithTextResult_ReturnsExpectedConnectorResponse()
    {
        var content = "some text response";
        var result = Results.Text(content, MediaTypeNames.Text.Plain, statusCode: 201);

        var connectorResponse = await HttpUtil.GetConnectorResponseFromIResult(result);

        connectorResponse.Should().NotBeNull();
        connectorResponse.Http.Should().NotBeNull();
        Encoding.UTF8.GetString(connectorResponse.Content.Span).Should().Be(content);
        connectorResponse.ContentType.Should().Be(MediaTypeNames.Text.Plain);
        connectorResponse.Http.StatusCode.Should().Be(201);
    }

    [TestMethod]
    public async Task GetConnectorResponseFromIResult_WithHeaders_ReturnsExpectedConnectorResponse()
    {
        var headers = new Dictionary<string, StringValues>
        {
            { "X-Custom-Header", "CustomValue" },
            { "X-Another-Header", new(["Value1", "Value2"]) }
        };

        var result = new HeadersResult(headers, 202);

        var connectorResponse = await HttpUtil.GetConnectorResponseFromIResult(result);

        connectorResponse.Should().NotBeNull();
        connectorResponse.Http.Should().NotBeNull();
        connectorResponse.Http.StatusCode.Should().Be(202);
        connectorResponse.Http.Headers.Should().NotBeNull();
        connectorResponse.Http.Headers["X-Custom-Header"].ToString().Should().Be("CustomValue");
        connectorResponse.Http.Headers["X-Another-Header"].ToArray().Should().BeEquivalentTo("Value1", "Value2");
    }

    [TestMethod]
    public async Task GetConnectorResponseFromIResult_WithEmptyResult_ReturnsEmptyConnectorResponse()
    {
        var result = Results.Empty;

        var response = await HttpUtil.GetConnectorResponseFromIResult(result);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEmpty();
        response.ContentType.Should().Be(MediaTypeNames.Application.Octet);
        response.Http.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(200);
        response.Http.Headers.Should().NotBeNull();
        response.Http.Headers.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GetConnectorResponseFromHttpResponseMessage_WithContent_ReturnsExpectedConnectorResponse()
    {
        var content = "some text response";
        var messageResponse = new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent(content, Encoding.UTF8, MediaTypeNames.Text.Plain)
        };

        var response = await HttpUtil.GetConnectorResponseFromHttpResponseMessage(messageResponse, ConnectorName);

        response.Should().NotBeNull();
        Encoding.UTF8.GetString(response.Content.Span).Should().Be(content);
        response.ContentType.Should().Be(MediaTypeNames.Text.Plain);
        response.ConnectorName.Should().Be(ConnectorName);
        response.Http.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(201);
    }

    [TestMethod]
    public async Task GetConnectorResponseFromHttpResponseMessage_WithHeaders_ReturnsExpectedConnectorResponse()
    {
        var messageResponse = new HttpResponseMessage(HttpStatusCode.Accepted);

        messageResponse.Headers.Add("X-Custom-Header", "CustomValue");
        messageResponse.Headers.Add("X-Another-Header", [ "Value1", "Value2" ]);

        var response = await HttpUtil.GetConnectorResponseFromHttpResponseMessage(messageResponse, ConnectorName);

        response.Should().NotBeNull();
        response.ConnectorName.Should().Be(ConnectorName);
        response.Http.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(202);
        response.Http.Headers.Should().NotBeNull();
        response.Http.Headers["X-Custom-Header"].ToString().Should().Be("CustomValue");
        response.Http.Headers["X-Another-Header"].ToArray().Should().BeEquivalentTo("Value1", "Value2");
    }


    [TestMethod]
    public async Task GetConnectorResponseFromHttpResponseMessage_WithEmptyContent_ReturnsEmptyConnectorResponse()
    {
        var messageResponse = new HttpResponseMessage();

        var response = await HttpUtil.GetConnectorResponseFromHttpResponseMessage(messageResponse, ConnectorName);

        response.Should().NotBeNull();
        response.Content.IsEmpty.Should().BeTrue();
        response.ContentType.Should().Be(MediaTypeNames.Application.Octet);
        response.ConnectorName.Should().Be(ConnectorName);
        response.Http.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(200);
        response.Http.Headers.Should().NotBeNull();
        response.Http.Headers.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GetConnectorRequestFromHttpRequest_WhenEmpty_ReturnsEmptyConnectorRequest()
    {
        var ctx = new DefaultHttpContext();
        var req = ctx.Request;
        req.Body = new MemoryStream();

        var connectorRequest = await HttpUtil.GetConnectorRequestFromHttpRequest(req, ConnectorName);

        connectorRequest.Content.IsEmpty.Should().BeTrue();
        connectorRequest.ContentType.Should().Be(MediaTypeNames.Application.Octet);
        connectorRequest.ConnectorName.Should().Be(ConnectorName);
        connectorRequest.Http.Should().NotBeNull();
        connectorRequest.Http.Method.Should().BeEmpty();
        connectorRequest.Http.Path.Should().BeEmpty();
        connectorRequest.Http.RouteParameters.Should().BeEmpty();
        connectorRequest.Http.Headers.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GetConnectorRequestFromHttpRequest_WhenPopulated_ReturnsExpectedConnectorRequest()
    {
        var content = "request body";
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Body = new MemoryStream(Encoding.UTF8.GetBytes(content));
        request.ContentType = MediaTypeNames.Text.Plain;
        request.Method = HttpMethods.Post;
        request.Path = "/test-path/42";
        request.RouteValues["id"] = 42;
        request.Headers["X-Custom-Header"] = "CustomValue";
        request.Headers["X-Another-Header"] = new StringValues(["Value1", "Value2"]);

        var connectorRequest = await HttpUtil.GetConnectorRequestFromHttpRequest(request, ConnectorName);

        connectorRequest.Should().NotBeNull();
        connectorRequest.Http.Should().NotBeNull();
        connectorRequest.Http.Headers.Should().NotBeNull();
        connectorRequest.Http.RouteParameters.Should().NotBeNull();
        Encoding.UTF8.GetString(connectorRequest.Content.Span).Should().Be(content);
        connectorRequest.ContentType.Should().Be(MediaTypeNames.Text.Plain);
        connectorRequest.ConnectorName.Should().Be(ConnectorName);
        connectorRequest.Http!.Method.Should().Be(HttpMethods.Post);
        connectorRequest.Http.Path.Should().Be("/test-path/42");
        connectorRequest.Http.RouteParameters["id"].Should().Be("42");
        connectorRequest.Http.Headers["X-Custom-Header"].ToString().Should().Be("CustomValue");
        connectorRequest.Http.Headers["X-Another-Header"].ToArray().Should().BeEquivalentTo("Value1", "Value2");
    }

    private class HeadersResult(Dictionary<string, StringValues> headers, int statusCode) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = statusCode;
            foreach (var header in headers)
            {
                httpContext.Response.Headers.TryAdd(header.Key, header.Value);
            }

            return Task.CompletedTask;
        }
    }
}
