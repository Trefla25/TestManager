using eHub.PlugIn.Communication;
using eHub.PlugIn;
using FluentAssertions;
using System.Net.Mime;
using Microsoft.Extensions.Primitives;
using System.Text.Json;

namespace eHub.Tests.Connectors;

public class ConnectorResponseTests
{
    private const string ConnectorName = "MyConnector";
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [Fact]
    public void ParameterlessConstructor_WhenPropertiesSet_SetsAllValues()
    {
        // Arrange
        var content = new ReadOnlyMemory<byte>([7, 8, 9]);
        const string contentType = MediaTypeNames.Text.Plain;
        var http = new ConnectorResponse.HttpConnectorResponse(200);
        var packetTransfer = new ConnectorResponse.PacketTransferResponse(ProcessPacketState.Success);
        
        // Act
        var response = new ConnectorResponse
        {
            Content = content,
            ContentType = contentType,
            ConnectorName = ConnectorName,
            Http = http,
            PacketTransfer = packetTransfer
        };
        
        // Assert
        response.Content.Should().BeEquivalentTo(content);
        response.ContentType.Should().Be(contentType);
        response.ConnectorName.Should().Be(ConnectorName);
        response.Http.Should().Be(http);
        response.PacketTransfer.Should().Be(packetTransfer);
    }

    [Fact]
    public void Constructor_OnlyRequired_SetsValues()
    {
        // Arrange
        var content = new ReadOnlyMemory<byte>([7, 8, 9]);
        const string contentType = MediaTypeNames.Text.Plain;
        
        // Act
        var response = new ConnectorResponse(content, contentType, ConnectorName);
        
        // Assert
        response.Content.Should().BeEquivalentTo(content);
        response.ContentType.Should().Be(contentType);
        response.ConnectorName.Should().Be(ConnectorName);
        response.Http.Should().BeNull();
        response.PacketTransfer.Should().BeNull();
    }

    [Fact]
    public void Constructor_AllValues_SetsAllValues()
    {
        // Arrange
        var content = new ReadOnlyMemory<byte>([7, 8, 9]);
        const string contentType = MediaTypeNames.Text.Plain;
        var http = new ConnectorResponse.HttpConnectorResponse(200);
        var packetTransfer = new ConnectorResponse.PacketTransferResponse(ProcessPacketState.Success);
        
        // Act
        var response = new ConnectorResponse(content, contentType, ConnectorName, http, packetTransfer);
        
        // Assert
        response.Content.Should().BeEquivalentTo(content);
        response.ContentType.Should().Be(contentType);
        response.ConnectorName.Should().Be(ConnectorName);
        response.Http.Should().Be(http);
        response.PacketTransfer.Should().Be(packetTransfer);
    }

    [Fact]
    public void ParameterlessHttpConstructor_WhenPropertiesSet_SetsAllValues()
    {
        // Arrange
        const int statusCode = 200;
        var headers = new Dictionary<string, StringValues>() { { "X-Custom-Header", "CustomValue" } };
        
        // Act
        var response = new ConnectorResponse.HttpConnectorResponse
        {
            StatusCode = statusCode,
            Headers = headers
        };
        
        // Assert
        response.StatusCode.Should().Be(statusCode);
        response.Headers.Should().BeEquivalentTo(headers);
    }

    [Fact]
    public void HttpConstructor_OnlyRequired_SetsValues()
    {
        // Arrange
        const int statusCode = 200;
        
        // Act
        var response = new ConnectorResponse.HttpConnectorResponse(statusCode);
        
        // Assert
        response.StatusCode.Should().Be(statusCode);
        response.Headers.Should().BeNull();
    }

    [Fact]
    public void HttpConstructor_AllParameters_SetsAllValues()
    {
        // Arrange
        const int statusCode = 200;
        var headers = new Dictionary<string, StringValues>() { { "X-Custom-Header", "CustomValue" } };
        
        // Act
        var response = new ConnectorResponse.HttpConnectorResponse(statusCode, headers);
        
        // Assert
        response.StatusCode.Should().Be(statusCode);
        response.Headers.Should().BeEquivalentTo(headers);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(202)]
    [InlineData(251)]
    [InlineData(299)]
    public void HttpIsSuccessStatusCode_WithSuccessStatusCode_ReturnsTrue(int statusCode)
    {
        // Act
        var response = new ConnectorResponse.HttpConnectorResponse(statusCode);
        
        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Theory]
    [InlineData(199)]
    [InlineData(300)]
    [InlineData(400)]
    [InlineData(500)]
    public void HttpIsSuccessStatusCode_WithNonSuccessStatusCode_ReturnsFalse(int statusCode)
    {
        // Act
        var response = new ConnectorResponse.HttpConnectorResponse(statusCode);
        
        // Assert
        response.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public void ParameterlessPacketTransferConstructor_WhenPropertiesSet_SetsAllValues()
    {
        // Arrange
        const ProcessPacketState processPacketState = ProcessPacketState.Success;
        
        // Act
        var response = new ConnectorResponse.PacketTransferResponse
        {
            ProcessPacketState = processPacketState
        };
        
        // Assert
        response.ProcessPacketState.Should().Be(processPacketState);
    }

    [Fact]
    public void PacketTransferConstructor_AllParameters_SetsAllValues()
    {
        // Arrange
        const ProcessPacketState processPacketState = ProcessPacketState.Success;
        
        // Act
        var response = new ConnectorResponse.PacketTransferResponse(processPacketState);
        
        // Assert
        response.ProcessPacketState.Should().Be(processPacketState);
    }

    [Fact]
    public void JsonSerialize_FromConnectorResponse_SerializesResponse()
    {
        // Arrange
        var request = new ConnectorResponse(
            content: new ReadOnlyMemory<byte>([7, 8, 9]),
            contentType: MediaTypeNames.Text.Plain,
            connectorName: ConnectorName,
            http: new ConnectorResponse.HttpConnectorResponse(
                statusCode: 200,
                headers: new Dictionary<string, StringValues>() { { "Empty", "" }, { "Single", "value" }, { "Multiple", new(["value1", "value2"]) } }),
            packetTransfer: new ConnectorResponse.PacketTransferResponse(
                processPacketState: ProcessPacketState.Success));

        const string expectedJson = """
        {
          "Content": "BwgJ",
          "ContentType": "text/plain",
          "ConnectorName": "MyConnector",
          "Http": {
            "StatusCode": 200,
            "Headers": {
              "Empty": "",
              "Single": "value",
              "Multiple": [
                "value1",
                "value2"
              ]
            }
          },
          "PacketTransfer": {
            "ProcessPacketState": 0
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
    public void JsonDeserialize_FromJson_DeserializesResponse()
    {
        // Arrange
        const string json = """
        {
          "Content": "BwgJ",
          "ContentType": "text/plain",
          "ConnectorName": "MyConnector",
          "Http": {
            "StatusCode": 200,
            "Headers": {
              "Empty": "",
              "Single": "value",
              "Multiple": [
                "value1",
                "value2"
              ]
            }
          },
          "PacketTransfer": {
            "ProcessPacketState": 0
          }
        }
        """;

        var expectedRequest = new ConnectorResponse(
            content: new ReadOnlyMemory<byte>([7, 8, 9]),
            contentType: MediaTypeNames.Text.Plain,
            connectorName: ConnectorName,
            http: new ConnectorResponse.HttpConnectorResponse(
                statusCode: 200,
                headers: new Dictionary<string, StringValues>() { { "Empty", "" }, { "Single", "value" }, { "Multiple", new(["value1", "value2"]) } }),
            packetTransfer: new ConnectorResponse.PacketTransferResponse(
                processPacketState: ProcessPacketState.Success));
        
        // Act
        var request = JsonSerializer.Deserialize<ConnectorResponse>(json);
        
        // Assert
        request.Should().NotBeNull();
        request.Content.ToArray().Should().BeEquivalentTo(expectedRequest.Content.ToArray());
        request.ContentType.Should().Be(expectedRequest.ContentType);
        request.ConnectorName.Should().Be(expectedRequest.ConnectorName);
        request.Http.Should().NotBeNull();
        request.Http.StatusCode.Should().Be(expectedRequest.Http?.StatusCode);
        request.Http.Headers.Should().BeEquivalentTo(expectedRequest.Http?.Headers);
        request.PacketTransfer.Should().NotBeNull();
        request.PacketTransfer.ProcessPacketState.Should().Be(expectedRequest.PacketTransfer?.ProcessPacketState);
    }
}
