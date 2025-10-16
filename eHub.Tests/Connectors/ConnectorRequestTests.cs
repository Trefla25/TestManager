using eHub.PlugIn.Communication;
using FluentAssertions;
using Microsoft.Extensions.Primitives;
using System.Net.Mime;
using System.Text.Json;

namespace eHub.Tests.Connectors;

[TestClass]
public class ConnectorRequestTests
{
    private const string ConnectorName = "MyConnector";
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [TestMethod]
    public void ParameterlessConstructor_WhenPropertiesSet_SetsAllValues()
    {
        var content = new ReadOnlyMemory<byte>([7, 8, 9]);
        var contentType = MediaTypeNames.Text.Plain;
        var http = new ConnectorRequest.HttpConnectorRequest("/test", "POST");

        var request = new ConnectorRequest
        {
            Content = content,
            ContentType = contentType,
            ConnectorName = ConnectorName,
            Http = http
        };

        request.Content.Should().BeEquivalentTo(content);
        request.ContentType.Should().Be(contentType);
        request.ConnectorName.Should().Be(ConnectorName);
        request.Http.Should().Be(http);
    }

    [TestMethod]
    public void Constructor_WithoutHttp_SetsValuesAndLeavesHttpNull()
    {
        var content = new ReadOnlyMemory<byte>([7, 8, 9]);
        var contentType = MediaTypeNames.Text.Plain;

        var request = new ConnectorRequest(content, contentType, ConnectorName);

        request.Content.Should().BeEquivalentTo(content);
        request.ContentType.Should().Be(contentType);
        request.ConnectorName.Should().Be(ConnectorName);
        request.Http.Should().BeNull();
    }

    [TestMethod]
    public void Constructor_WithHttp_SetsAllValues()
    {
        var content = new ReadOnlyMemory<byte>([7, 8, 9]);
        var contentType = MediaTypeNames.Text.Plain;
        var http = new ConnectorRequest.HttpConnectorRequest("/test", "POST");

        var request = new ConnectorRequest(content, contentType, ConnectorName, http);

        request.Content.Should().BeEquivalentTo(content);
        request.ContentType.Should().Be(contentType);
        request.ConnectorName.Should().Be(ConnectorName);
        request.Http.Should().Be(http);
    }

    [TestMethod]
    public void ParameterlessHttpConstructor_WhenPropertiesSet_SetsAllValues()
    {
        var path = "/test/45";
        var method = "POST";
        var routeParameters = new Dictionary<string, string>() { { "id", "45" } };
        var headers = new Dictionary<string, StringValues>() { { "X-Custom-Header", "CustomValue" } };

        var request = new ConnectorRequest.HttpConnectorRequest
        {
            Path = path,
            Method = method,
            RouteParameters = routeParameters,
            Headers = headers
        };

        request.Path.Should().Be(path);
        request.Method.Should().Be(method);
        request.RouteParameters.Should().BeEquivalentTo(routeParameters);
        request.Headers.Should().BeEquivalentTo(headers);
    }

    [TestMethod]
    public void HttpConstructor_OnlyRequired_SetsValues()
    {
        var path = "/test";
        var method = "POST";

        var request = new ConnectorRequest.HttpConnectorRequest(path, method);

       request.Path.Should().Be(path);
        request.Method.Should().Be(method);
        request.RouteParameters.Should().BeNull();
        request.Headers.Should().BeNull();
    }

    [TestMethod]
    public void HttpConstructor_AllParameters_SetsAllValues()
    {
        var path = "/test/45";
        var method = "POST";
        var routeParameters = new Dictionary<string, string>() { { "id", "45" } };
        var headers = new Dictionary<string, StringValues>() { { "X-Custom-Header", "CustomValue" } };
        var query = new Dictionary<string, StringValues>() { { "search", "value" } };

        var request = new ConnectorRequest.HttpConnectorRequest(path, method, routeParameters, headers, query);

        request.Path.Should().Be(path);
        request.Method.Should().Be(method);
        request.RouteParameters.Should().BeEquivalentTo(routeParameters);
        request.Headers.Should().BeEquivalentTo(headers);
        request.Query.Should().BeEquivalentTo(query);
    }

    [TestMethod]
    public void JsonSerialize_FromConnectorRequest_SerializesRequest()
    {
        var request = new ConnectorRequest(
            content: new ReadOnlyMemory<byte>([7, 8, 9]),
            contentType: MediaTypeNames.Text.Plain,
            connectorName: ConnectorName,
            http: new ConnectorRequest.HttpConnectorRequest(
                path: "/test/45",
                method: "POST",
                routeParameters: new Dictionary<string, string>() { { "id", "45" } },
                headers: new Dictionary<string, StringValues>() { { "Empty", "" }, { "Single", "value" }, { "Multiple", new(["value1", "value2"]) } },
                query: new Dictionary<string, StringValues>() { { "single", "value" }, { "multiple", new(["value1", "value2"]) } }));

        var expectedJson = """
        {
          "Content": "BwgJ",
          "ContentType": "text/plain",
          "ConnectorName": "MyConnector",
          "Http": {
            "Path": "/test/45",
            "Method": "POST",
            "RouteParameters": {
              "id": "45"
            },
            "Headers": {
              "Empty": "",
              "Single": "value",
              "Multiple": [
                "value1",
                "value2"
              ]
            },
            "Query": {
              "single": "value",
              "multiple": [
                "value1",
                "value2"
              ]
            }
          }
        }
        """;

        var json = JsonSerializer.Serialize(request, _jsonOptions);

        json.Should().NotBeNull();
        json.Should().BeEquivalentTo(expectedJson);
    }

    [TestMethod]
    public void JsonDeserialize_FromJson_DeserializesRequest()
    {
        var json = """
        {
          "Content": "BwgJ",
          "ContentType": "text/plain",
          "ConnectorName": "MyConnector",
          "Http": {
            "Path": "/test/45",
            "Method": "POST",
            "RouteParameters": {
              "id": "45"
            },
            "Headers": {
              "Empty": "",
              "Single": "value",
              "Multiple": [
                "value1",
                "value2"
              ]
            },
            "Query": {
              "single": "value",
              "multiple": [
                "value1",
                "value2"
              ]
            }
          }
        }
        """;

        var expectedRequest = new ConnectorRequest(
            content: new ReadOnlyMemory<byte>([7, 8, 9]),
            contentType: MediaTypeNames.Text.Plain,
            connectorName: ConnectorName,
            http: new ConnectorRequest.HttpConnectorRequest(
                path: "/test/45",
                method: "POST",
                routeParameters: new Dictionary<string, string>() { { "id", "45" } },
                headers: new Dictionary<string, StringValues>() { { "Empty", "" }, { "Single", "value" }, { "Multiple", new(["value1", "value2"]) } },
                query: new Dictionary<string, StringValues>() { { "single", "value" }, { "multiple", new(["value1", "value2"]) } }));


        var request = JsonSerializer.Deserialize<ConnectorRequest>(json);

        request.Should().NotBeNull();
        request.Content.ToArray().Should().BeEquivalentTo(expectedRequest.Content.ToArray());
        request.ContentType.Should().Be(expectedRequest.ContentType);
        request.ConnectorName.Should().Be(expectedRequest.ConnectorName);
        request.Http.Should().NotBeNull();
        request.Http.Path.Should().Be(expectedRequest.Http?.Path);
        request.Http.Method.Should().Be(expectedRequest.Http?.Method);
        request.Http.RouteParameters.Should().BeEquivalentTo(expectedRequest.Http?.RouteParameters);
        request.Http.Headers.Should().BeEquivalentTo(expectedRequest.Http?.Headers);
        request.Http.Query.Should().BeEquivalentTo(expectedRequest.Http?.Query);
    }
}
