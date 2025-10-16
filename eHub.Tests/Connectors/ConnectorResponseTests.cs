using eHub.PlugIn.Communication;
using eHub.PlugIn;
using FluentAssertions;
using System.Net.Mime;
using Microsoft.Extensions.Primitives;
using System.Text.Json;

namespace eHub.Tests.Connectors;

[TestClass]
public class ConnectorResponseTests
{
    private const string ConnectorName = "MyConnector";
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [TestMethod]
    public void ParameterlessConstructor_WhenPropertiesSet_SetsAllValues()
    {
        var content = new ReadOnlyMemory<byte>([7, 8, 9]);
        var contentType = MediaTypeNames.Text.Plain;
        var http = new ConnectorResponse.HttpConnectorResponse(200);
        var packetTransfer = new ConnectorResponse.PacketTransferResponse(ProcessPacketState.Success);

        var response = new ConnectorResponse
        {
            Content = content,
            ContentType = contentType,
            ConnectorName = ConnectorName,
            Http = http,
            PacketTransfer = packetTransfer
        };

        response.Content.Should().BeEquivalentTo(content);
        response.ContentType.Should().Be(contentType);
        response.ConnectorName.Should().Be(ConnectorName);
        response.Http.Should().Be(http);
        response.PacketTransfer.Should().Be(packetTransfer);
    }

    [TestMethod]
    public void Constructor_OnlyRequired_SetsValues()
    {
        var content = new ReadOnlyMemory<byte>([7, 8, 9]);
        var contentType = MediaTypeNames.Text.Plain;

        var response = new ConnectorResponse(content, contentType, ConnectorName);

        response.Content.Should().BeEquivalentTo(content);
        response.ContentType.Should().Be(contentType);
        response.ConnectorName.Should().Be(ConnectorName);
        response.Http.Should().BeNull();
        response.PacketTransfer.Should().BeNull();
    }

    [TestMethod]
    public void Constructor_AllValues_SetsAllValues()
    {
        var content = new ReadOnlyMemory<byte>([7, 8, 9]);
        var contentType = MediaTypeNames.Text.Plain;
        var http = new ConnectorResponse.HttpConnectorResponse(200);
        var packetTransfer = new ConnectorResponse.PacketTransferResponse(ProcessPacketState.Success);

        var response = new ConnectorResponse(content, contentType, ConnectorName, http, packetTransfer);

        response.Content.Should().BeEquivalentTo(content);
        response.ContentType.Should().Be(contentType);
        response.ConnectorName.Should().Be(ConnectorName);
        response.Http.Should().Be(http);
        response.PacketTransfer.Should().Be(packetTransfer);
    }

    [TestMethod]
    public void ParameterlessHttpConstructor_WhenPropertiesSet_SetsAllValues()
    {
        var statusCode = 200;
        var headers = new Dictionary<string, StringValues>() { { "X-Custom-Header", "CustomValue" } };

        var response = new ConnectorResponse.HttpConnectorResponse
        {
            StatusCode = statusCode,
            Headers = headers
        };

        response.StatusCode.Should().Be(statusCode);
        response.Headers.Should().BeEquivalentTo(headers);
    }

    [TestMethod]
    public void HttpConstructor_OnlyRequired_SetsValues()
    {
        var statusCode = 200;

        var response = new ConnectorResponse.HttpConnectorResponse(statusCode);

        response.StatusCode.Should().Be(statusCode);
        response.Headers.Should().BeNull();
    }

    [TestMethod]
    public void HttpConstructor_AllParameters_SetsAllValues()
    {
        var statusCode = 200;
        var headers = new Dictionary<string, StringValues>() { { "X-Custom-Header", "CustomValue" } };

        var response = new ConnectorResponse.HttpConnectorResponse(statusCode, headers);

        response.StatusCode.Should().Be(statusCode);
        response.Headers.Should().BeEquivalentTo(headers);
    }

    [DataTestMethod]
    [DataRow(200)]
    [DataRow(202)]
    [DataRow(251)]
    [DataRow(299)]
    public void HttpIsSuccessStatusCode_WithSuccessStatusCode_ReturnsTrue(int statusCode)
    {
        var response = new ConnectorResponse.HttpConnectorResponse(statusCode);

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [DataTestMethod]
    [DataRow(199)]
    [DataRow(300)]
    [DataRow(400)]
    [DataRow(500)]
    public void HttpIsSuccessStatusCode_WithNonSuccessStatusCode_ReturnsFalse(int statusCode)
    {
        var response = new ConnectorResponse.HttpConnectorResponse(statusCode);

        response.IsSuccessStatusCode.Should().BeFalse();
    }

    [TestMethod]
    public void ParameterlessPacketTransferConstructor_WhenPropertiesSet_SetsAllValues()
    {
        var processPacketState = ProcessPacketState.Success;

        var response = new ConnectorResponse.PacketTransferResponse
        {
            ProcessPacketState = processPacketState
        };

        response.ProcessPacketState.Should().Be(processPacketState);
    }

    [TestMethod]
    public void PacketTransferConstructor_AllParameters_SetsAllValues()
    {
        var processPacketState = ProcessPacketState.Success;

        var response = new ConnectorResponse.PacketTransferResponse(processPacketState);

        response.ProcessPacketState.Should().Be(processPacketState);
    }

    [TestMethod]
    public void JsonSerialize_FromConnectorResponse_SerializesResponse()
    {
        var request = new ConnectorResponse(
            content: new ReadOnlyMemory<byte>([7, 8, 9]),
            contentType: MediaTypeNames.Text.Plain,
            connectorName: ConnectorName,
            http: new ConnectorResponse.HttpConnectorResponse(
                statusCode: 200,
                headers: new Dictionary<string, StringValues>() { { "Empty", "" }, { "Single", "value" }, { "Multiple", new(["value1", "value2"]) } }),
            packetTransfer: new ConnectorResponse.PacketTransferResponse(
                processPacketState: ProcessPacketState.Success));

        var expectedJson = """
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

        var json = JsonSerializer.Serialize(request, _jsonOptions);

        json.Should().NotBeNull();
        json.Should().BeEquivalentTo(expectedJson);
    }

    [TestMethod]
    public void JsonDeserialize_FromJson_DeserializesResponse()
    {
        var json = """
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

        var request = JsonSerializer.Deserialize<ConnectorResponse>(json);

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
