using eHub.PlugIn.Communication;
using FluentAssertions;
using Microsoft.Extensions.Primitives;
using System.Net.Mime;
using System.Text.Json;

namespace eHub.Tests.Connectors;

public class ConnectorRequestTests
{
    private const string ConnectorName = "MyConnector";
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [Fact]
    public void ParameterlessConstructor_WhenPropertiesSet_SetsAllValues()
    {
        // Arrange
        var content = new ReadOnlyMemory<byte>([7, 8, 9]);
        const string contentType = MediaTypeNames.Text.Plain;
        var http = new ConnectorRequest.HttpConnectorRequest("/test", "POST");
        
        // Act
        var request = new ConnectorRequest
        {
            Content = content,
            ContentType = contentType,
            ConnectorName = ConnectorName,
            Http = http
        };
        
        // Assert
        request.Content.Should().BeEquivalentTo(content);
        request.ContentType.Should().Be(contentType);
        request.ConnectorName.Should().Be(ConnectorName);
        request.Http.Should().Be(http);
    }

    [Fact]
    public void Constructor_WithoutHttp_SetsValuesAndLeavesHttpNull()
    {
        // Arrange
        var content = new ReadOnlyMemory<byte>([7, 8, 9]);
        const string contentType = MediaTypeNames.Text.Plain;
        
        // Act
        var request = new ConnectorRequest(content, contentType, ConnectorName);
        
        // Assert
        request.Content.Should().BeEquivalentTo(content);
        request.ContentType.Should().Be(contentType);
        request.ConnectorName.Should().Be(ConnectorName);
        request.Http.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithHttp_SetsAllValues()
    {
        // Arrange
        var content = new ReadOnlyMemory<byte>([7, 8, 9]);
        const string contentType = MediaTypeNames.Text.Plain;
        var http = new ConnectorRequest.HttpConnectorRequest("/test", "POST");
        
        // Act
        var request = new ConnectorRequest(content, contentType, ConnectorName, http);
        
        // Assert
        request.Content.Should().BeEquivalentTo(content);
        request.ContentType.Should().Be(contentType);
        request.ConnectorName.Should().Be(ConnectorName);
        request.Http.Should().Be(http);
    }

    [Fact]
    public void ParameterlessHttpConstructor_WhenPropertiesSet_SetsAllValues()
    {
        // Arrange
        const string path = "/test/45";
        const string method = "POST";
        var routeParameters = new Dictionary<string, string>() { { "id", "45" } };
        var headers = new Dictionary<string, StringValues>() { { "X-Custom-Header", "CustomValue" } };
        
        // Act
        var request = new ConnectorRequest.HttpConnectorRequest
        {
            Path = path,
            Method = method,
            RouteParameters = routeParameters,
            Headers = headers
        };
        
        // Assert
        request.Path.Should().Be(path);
        request.Method.Should().Be(method);
        request.RouteParameters.Should().BeEquivalentTo(routeParameters);
        request.Headers.Should().BeEquivalentTo(headers);
    }

    [Fact]
    public void HttpConstructor_OnlyRequired_SetsValues()
    {
        // Arrange
        const string path = "/test";
        const string method = "POST";

        // Act
        var request = new ConnectorRequest.HttpConnectorRequest(path, method);

        // Assert
        request.Path.Should().Be(path);
        request.Method.Should().Be(method);
        request.RouteParameters.Should().BeNull();
        request.Headers.Should().BeNull();
    }

    [Fact]
    public void HttpConstructor_AllParameters_SetsAllValues()
    {
        // Arrange
        const string path = "/test/45";
        const string method = "POST";
        var routeParameters = new Dictionary<string, string>() { { "id", "45" } };
        var headers = new Dictionary<string, StringValues>() { { "X-Custom-Header", "CustomValue" } };
        var query = new Dictionary<string, StringValues>() { { "search", "value" } };
        
        // Act
        var request = new ConnectorRequest.HttpConnectorRequest(path, method, routeParameters, headers, query);
        
        // Assert
        request.Path.Should().Be(path);
        request.Method.Should().Be(method);
        request.RouteParameters.Should().BeEquivalentTo(routeParameters);
        request.Headers.Should().BeEquivalentTo(headers);
        request.Query.Should().BeEquivalentTo(query);
    }

    [Fact]
    public void JsonSerialize_FromConnectorRequest_SerializesRequest()
    {
        // Arrange
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

        const string expectedJson = """
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
        
        // Act
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        
        // Assert
        json.Should().NotBeNull();
        var normalizedJson = json.Replace("\r\n", "\n");
        var normalizedExpected = expectedJson.Replace("\r\n", "\n");
        normalizedJson.Should().Be(normalizedExpected);
    }

    [Fact]
    public void JsonDeserialize_FromJson_DeserializesRequest()
    {
        // Arrange
        const string json = """
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
        
        // Act
        var request = JsonSerializer.Deserialize<ConnectorRequest>(json);
        
        // Assert
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
