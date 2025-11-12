using eHub.PlugIn;
using eHub.PlugIn.Communication;
using FluentAssertions;
using System.Net.Mime;
using System.Text;
using System.Text.Json;

namespace eHub.Tests.Connectors;

public class ConnectorResponsesTests
{
    private const string ConnectorName = "MyConnector";

    [Fact]
    public void Acknowledge_WhenCalled_ReturnsEmptyResponse()
    {
        // Act
        var response = ConnectorResponses.Acknowledge(ConnectorName);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.IsEmpty.Should().BeTrue();
        response.ContentType.Should().BeEmpty();
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [Fact]
    public void Content_WhenCalled_ReturnsContentResponse()
    {
        // Arrange
        const string json = "{\"message\":\"test\"}";
        
        // Act
        var response = ConnectorResponses.Content(json, MediaTypeNames.Application.Json, ConnectorName);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(json));
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [Fact]
    public void BinaryContent_WhenCalled_ReturnsContentResponse()
    {
        // Arrange
        var bytes = new byte[] { 0x01, 0xFF, 0x42 };
        
        // Act
        var response = ConnectorResponses.BinaryContent(bytes, MediaTypeNames.Application.Octet, ConnectorName);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(bytes);
        response.ContentType.Should().Be(MediaTypeNames.Application.Octet);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [Fact]
    public void Text_WhenCalled_ReturnsPlainTextResponse()
    {
        // Arrange
        const string text = "hello!";
        
        // Act
        var response = ConnectorResponses.Text(text, ConnectorName);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(text));
        response.ContentType.Should().Be(MediaTypeNames.Text.Plain);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [Fact]
    public void Json_WhenCalled_ReturnsJsonResponse()
    {
        // Arrange
        var obj = new { Foo = "bar", Count = 123 };
        var expectedJson = JsonSerializer.SerializeToUtf8Bytes(obj);
        
        // Act
        var response = ConnectorResponses.Json(obj, ConnectorName);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(expectedJson);
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [Fact]
    public void HttpContent_WithoutStatus_Returns200StatusContentResponse()
    {
        // Arrange
        const string body = "{\"ok\":true}";
        const int statusCode = 200;
        
        // Act
        var response = ConnectorResponses.HttpContent(body, MediaTypeNames.Application.Json, ConnectorName);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(body));
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.ConnectorName.Should().Be(ConnectorName);
        response.Http!.StatusCode.Should().Be(statusCode);
    }

    [Fact]
    public void HttpContent_WithStatus_ReturnsCustomStatusContentResponse()
    {
        // Arrange
        const string body = "created!";
        const int customCode = 201;
        
        // Act
        var response = ConnectorResponses.HttpContent(body, MediaTypeNames.Text.Plain, ConnectorName, customCode);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(body));
        response.ContentType.Should().Be(MediaTypeNames.Text.Plain);
        response.ConnectorName.Should().Be(ConnectorName);
        response.Http!.StatusCode.Should().Be(customCode);
    }

    [Fact]
    public void HttpBinaryContent_WithoutStatus_Returns200StatusBinaryResponse()
    {
        // Arrange
        var bytes = new byte[] { 0x10, 0x20, 0x30 };
        
        // Act
        var response = ConnectorResponses.HttpBinaryContent(bytes, MediaTypeNames.Application.Octet, ConnectorName);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(bytes);
        response.ContentType.Should().Be(MediaTypeNames.Application.Octet);
        response.ConnectorName.Should().Be(ConnectorName);
        response.Http!.StatusCode.Should().Be(200);
    }

    [Fact]
    public void HttpBinaryContent_WithStatus_ReturnsCustomStatusBinaryResponse()
    {
        // Arrange
        var bytes = new byte[] { 0x10, 0x20, 0x30 };
        
        // Act
        var response = ConnectorResponses.HttpBinaryContent(bytes, MediaTypeNames.Application.Octet, ConnectorName, 206);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(bytes);
        response.ContentType.Should().Be(MediaTypeNames.Application.Octet);
        response.ConnectorName.Should().Be(ConnectorName);
        response.Http!.StatusCode.Should().Be(206);
    }

    [Fact]
    public void HttpText_WithoutStatus_Returns200StatusPlainTextResponse()
    {
        // Arrange
        const string msg = "all green";
        
        // Act
        var response = ConnectorResponses.HttpText(msg, ConnectorName);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(msg));
        response.ContentType.Should().Be(MediaTypeNames.Text.Plain);
        response.ConnectorName.Should().Be(ConnectorName);
        response.Http!.StatusCode.Should().Be(200);
    }

    [Fact]
    public void HttpText_WithStatus_ReturnsCustomStatusPlainTextResponse()
    {
        // Arrange
        const string msg = "all green";
        
        // Act
        var response = ConnectorResponses.HttpText(msg, ConnectorName, 202);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(msg));
        response.ContentType.Should().Be(MediaTypeNames.Text.Plain);
        response.ConnectorName.Should().Be(ConnectorName);
        response.Http!.StatusCode.Should().Be(202);
    }

    [Fact]
    public void HttpJson_WithoutStatus_Returns200StatusJsonResponse()
    {
        // Arrange
        var payload = new { x = 1, y = 2 };
        var expected = JsonSerializer.SerializeToUtf8Bytes(payload);
        
        // Act
        var response = ConnectorResponses.HttpJson(payload, ConnectorName);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(expected);
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.ConnectorName.Should().Be(ConnectorName);
        response.Http!.StatusCode.Should().Be(200);
    }

    [Fact]
    public void HttpJson_WithStatus_ReturnsCustomStatusJsonResponse()
    {
        // Arrange
        var payload = new { x = 1, y = 2 };
        var expected = JsonSerializer.SerializeToUtf8Bytes(payload);
        
        // Act
        var response = ConnectorResponses.HttpJson(payload, ConnectorName, 220);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(expected);
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.ConnectorName.Should().Be(ConnectorName);
        response.Http!.StatusCode.Should().Be(220);
    }

    [Theory]
    [InlineData(102)]
    [InlineData(204)]
    [InlineData(302)]
    [InlineData(418)]
    [InlineData(509)]
    public void HttpStatus_WhenCalled_ReturnsStatusOnlyResponse(int status)
    {
        // Act
        var response = ConnectorResponses.HttpStatus(ConnectorName, status);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.IsEmpty.Should().BeTrue();
        response.ContentType.Should().BeEmpty();
        response.ConnectorName.Should().Be(ConnectorName);
        response.Http!.StatusCode.Should().Be(status);
    }

    [Theory]
    [InlineData(nameof(ConnectorResponses.HttpOk), 200)]
    [InlineData(nameof(ConnectorResponses.HttpCreated), 201)]
    [InlineData(nameof(ConnectorResponses.HttpAccepted), 202)]
    public void KnownHttpSuccessStatusMethods_WhenCalled_ReturnExpectedStatus(string methodName, int expectedStatus)
    {
        // Arrange
        var method = typeof(ConnectorResponses).GetMethod(methodName)!;
        
        // Act
        var response = (ConnectorResponse)method.Invoke(null, [ConnectorName])!;
        
        // Assert
        response.Should().NotBeNull();
        response.Http!.StatusCode.Should().Be(expectedStatus);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [Fact]
    public void HttpBadRequest_WithoutDetail_Returns400ProblemResponse()
    {
        // Act
        var response = ConnectorResponses.HttpBadRequest(ConnectorName);
        
        // Assert
        response.Should().NotBeNull();
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.Http!.StatusCode.Should().Be(400);
        response.ConnectorName.Should().Be(ConnectorName);

        var json = JsonDocument.Parse(response.Content);
        json.RootElement.GetProperty("title").GetString().Should().Be("Bad Request");
    }

    [Fact]
    public void HttpBadRequest_WithDetail_Returns400ProblemResponse()
    {
        // Arrange
        const string detail = "Missing field";
        
        // Act
        var response = ConnectorResponses.HttpBadRequest(ConnectorName, detail);
        
        // Assert
        response.Should().NotBeNull();
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.Http!.StatusCode.Should().Be(400);
        response.ConnectorName.Should().Be(ConnectorName);

        var json = JsonDocument.Parse(response.Content);
        json.RootElement.GetProperty("title").GetString().Should().Be("Bad Request");
        json.RootElement.GetProperty("detail").GetString().Should().Be(detail);
    }

    [Fact]
    public void HttpBadGateway_WithoutDetail_Returns502ProblemResponse()
    {
        // Act
        var response = ConnectorResponses.HttpBadGateway(ConnectorName);
        
        // Assert
        response.Should().NotBeNull();
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.Http!.StatusCode.Should().Be(502);
        response.ConnectorName.Should().Be(ConnectorName);

        var json = JsonDocument.Parse(response.Content);
        json.RootElement.GetProperty("title").GetString().Should().Be("Bad Gateway");
    }

    [Fact]
    public void HttpBadGateway_WithDetail_Returns502ProblemResponse()
    {
        // Arrange
        const string detail = "Communication error";
        
        // Act
        var response = ConnectorResponses.HttpBadGateway(ConnectorName, detail);
        
        // Assert
        response.Should().NotBeNull();
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.Http!.StatusCode.Should().Be(502);
        response.ConnectorName.Should().Be(ConnectorName);

        var json = JsonDocument.Parse(response.Content);
        json.RootElement.GetProperty("title").GetString().Should().Be("Bad Gateway");
        json.RootElement.GetProperty("detail").GetString().Should().Be(detail);
    }

    [Fact]
    public void HttpRedirect_WithoutStatus_Returns302StatusWithLocationHeaderResponse()
    {
        // Arrange
        const string location = "https://example.com/next";
        
        // Act
        var response = ConnectorResponses.HttpRedirect(location, ConnectorName);
        
        // Assert
        response.Should().NotBeNull();
        response.Http!.StatusCode.Should().Be(302);
        response.Http.Headers!["Location"].ToString().Should().Be(location);
        response.Content.IsEmpty.Should().BeTrue();
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [Fact]
    public void HttpRedirect_WithStatus_ReturnsCustomStatusWithLocationHeaderResponse()
    {
        // Arrange
        const string location = "https://example.com/next";
        
        // Act
        var response = ConnectorResponses.HttpRedirect(location, ConnectorName, 301);
        
        // Assert
        response.Should().NotBeNull();
        response.Http!.StatusCode.Should().Be(301);
        response.Http.Headers!["Location"].ToString().Should().Be(location);
        response.Content.IsEmpty.Should().BeTrue();
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [Fact]
    public void HttpProblem_WithoutStatus_Returns500ProblemPayload()
    {
        // Arrange
        const string title = "Internal Oops";
        
        // Act
        var response = ConnectorResponses.HttpProblem(title, ConnectorName);
        
        // Assert
        response.Should().NotBeNull();
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.Http!.StatusCode.Should().Be(500);
        response.ConnectorName.Should().Be(ConnectorName);

        var doc = JsonDocument.Parse(response.Content);
        doc.RootElement.GetProperty("title").GetString().Should().Be(title);
    }

    [Fact]
    public void HttpProblem_WithStatus_ReturnsCustomStatusProblemPayload()
    {
        // Arrange
        const string title = "Internal Oops";
        
        // Act
        var response = ConnectorResponses.HttpProblem(title, ConnectorName, 505);
        
        // Assert
        response.Should().NotBeNull();
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.Http!.StatusCode.Should().Be(505);
        response.ConnectorName.Should().Be(ConnectorName);

        var doc = JsonDocument.Parse(response.Content);
        doc.RootElement.GetProperty("title").GetString().Should().Be(title);
    }

    [Fact]
    public void HttpProblem_WithDetail_ReturnsProblemPayloadWithDetail()
    {
        // Arrange
        const string title = "Internal Oops";
        const string detail = "Stack overflow";
        
        // Act
        var response = ConnectorResponses.HttpProblem(title, ConnectorName, detail: detail);
        
        // Assert
        response.Should().NotBeNull();
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.Http.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(500);
        response.ConnectorName.Should().Be(ConnectorName);

        var doc = JsonDocument.Parse(response.Content);
        doc.RootElement.GetProperty("title").GetString().Should().Be(title);
        doc.RootElement.GetProperty("detail").GetString().Should().Be(detail);
    }

    [Fact]
    public void PacketTransferContent_WithoutState_ReturnsSuccessStateContentResponse()
    {
        // Arrange
        const string body = "<ok />";
        
        // Act
        var response = ConnectorResponses.PacketTransferContent(body, MediaTypeNames.Text.Xml, ConnectorName);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(body));
        response.ContentType.Should().Be(MediaTypeNames.Text.Xml);
        response.ConnectorName.Should().Be(ConnectorName);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Success);
        response.Http.Should().BeNull();
    }

    [Fact]
    public void PacketTransferContent_WithState_ReturnsCustomStateContentResponse()
    {
        // Arrange
        const string body = "<ok />";
        
        // Act
        var response = ConnectorResponses.PacketTransferContent(body, MediaTypeNames.Text.Xml, ConnectorName, ProcessPacketState.Retry);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(body));
        response.ContentType.Should().Be(MediaTypeNames.Text.Xml);
        response.ConnectorName.Should().Be(ConnectorName);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Retry);
        response.Http.Should().BeNull();
    }

    [Fact]
    public void PacketTransferBinaryContent_WithoutState_ReturnsSuccessStateBinaryResponse()
    {
        // Arrange
        var bytes = new byte[] { 0xFE, 0xED, 0xFA, 0xCE };
        
        // Act
        var response = ConnectorResponses.PacketTransferBinaryContent(bytes, MediaTypeNames.Application.Octet, ConnectorName);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(bytes);
        response.ContentType.Should().Be(MediaTypeNames.Application.Octet);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Success);
        response.Http.Should().BeNull();
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [Fact]
    public void PacketTransferBinaryContent_WithState_ReturnsCustomStateBinaryResponse()
    {
        // Arrange
        var bytes = new byte[] { 0xFE, 0xED, 0xFA, 0xCE };
        
        // Act
        var response = ConnectorResponses.PacketTransferBinaryContent(bytes, MediaTypeNames.Application.Octet, ConnectorName, ProcessPacketState.Error);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(bytes);
        response.ContentType.Should().Be(MediaTypeNames.Application.Octet);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Error);
        response.Http.Should().BeNull();
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [Fact]
    public void PacketTransferText_WithoutState_ReturnsSuccessStatePlainTextResponse()
    {
        // Arrange
        const string text = "Some text";
        
        // Act
        var response = ConnectorResponses.PacketTransferText(text, ConnectorName);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(text));
        response.ContentType.Should().Be(MediaTypeNames.Text.Plain);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Success);
        response.Http.Should().BeNull();
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [Fact]
    public void PacketTransferText_WithState_ReturnsCustomStatePlainTextResponse()
    {
        // Arrange
        const string text = "Some text";
        
        // Act
        var response = ConnectorResponses.PacketTransferText(text, ConnectorName, ProcessPacketState.InProgress);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(text));
        response.ContentType.Should().Be(MediaTypeNames.Text.Plain);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.InProgress);
        response.Http.Should().BeNull();
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [Fact]
    public void PacketTransferJson_WithoutState_ReturnsSuccessStateJsonResponse()
    {
        // Arrange
        var dto = new { ok = true };
        var expected = JsonSerializer.SerializeToUtf8Bytes(dto);
        
        // Act
        var response = ConnectorResponses.PacketTransferJson(dto, ConnectorName);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(expected);
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Success);
        response.Http.Should().BeNull();
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [Fact]
    public void PacketTransferJson_WithState_ReturnsCustomStateJsonResponse()
    {
        // Arrange
        var dto = new { ok = true };
        var expected = JsonSerializer.SerializeToUtf8Bytes(dto);
        
        // Act
        var response = ConnectorResponses.PacketTransferJson(dto, ConnectorName, ProcessPacketState.FatalError);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(expected);
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.FatalError);
        response.Http.Should().BeNull();
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [Theory]
    [InlineData(ProcessPacketState.Success)]
    [InlineData(ProcessPacketState.Retry)]
    [InlineData(ProcessPacketState.Error)]
    [InlineData(ProcessPacketState.FatalError)]
    [InlineData(ProcessPacketState.InProgress)]
    [InlineData(ProcessPacketState.RetryUnchanged)]
    public void PacketTransferState_WhenCalled_ReturnsExpectedState(ProcessPacketState state)
    {
        // Act
        var response = ConnectorResponses.PacketTransferState(ConnectorName, state);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.IsEmpty.Should().BeTrue();
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(state);
        response.Http.Should().BeNull();
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [Fact]
    public void PacketTransferSuccess_WhenCalled_ReturnsSuccessState()
    {
        // Act
        var response = ConnectorResponses.PacketTransferSuccess(ConnectorName);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.IsEmpty.Should().BeTrue();
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Success);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [Fact]
    public void PacketTransferFatalError_WithoutDetail_ReturnsFatalErrorState()
    {
        // Act
        var response = ConnectorResponses.PacketTransferFatalError(ConnectorName);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.IsEmpty.Should().BeTrue();
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.FatalError);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [Fact]
    public void PacketTransferFatalError_WithDetail_ReturnsFatalErrorState()
    {
        // Arrange
        const string detail = "hard failure";
        
        // Act
        var response = ConnectorResponses.PacketTransferFatalError(ConnectorName, detail);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(detail));
        response.ContentType.Should().Be(MediaTypeNames.Text.Plain);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.FatalError);
        response.ConnectorName.Should().Be(ConnectorName);
    }
    
    [Fact]
    public void HttpPacketTransferContent_WhenCalled_ReturnsContentResponse()
    {
        // Arrange
        const string body = "good";
        
        // Act
        var response = ConnectorResponses.HttpPacketTransferContent(body, MediaTypeNames.Text.Plain, ConnectorName);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(body));
        response.ContentType.Should().Be(MediaTypeNames.Text.Plain);
        response.Http.Should().NotBeNull();
        response.PacketTransfer.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(200);
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Success);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [Fact]
    public void HttpPacketTransferContent_WithStatusAndState_ReturnsCustomStatusAndStateContentResponse()
    {
        // Arrange
        const string body = "in progress";
        
        // Act
        var response = ConnectorResponses.HttpPacketTransferContent(body, MediaTypeNames.Text.Plain, ConnectorName, 202, ProcessPacketState.InProgress);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(body));
        response.ContentType.Should().Be(MediaTypeNames.Text.Plain);
        response.Http.Should().NotBeNull();
        response.PacketTransfer.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(202);
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.InProgress);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [Fact]
    public void HttpPacketTransferBinaryContent_WhenCalled_ReturnsBinaryResponse()
    {
        // Arrange
        var bytes = new byte[] { 0xAB, 0xCD };
        
        // Act
        var response = ConnectorResponses.HttpPacketTransferBinaryContent(bytes, MediaTypeNames.Application.Octet, ConnectorName);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(bytes);
        response.ContentType.Should().Be(MediaTypeNames.Application.Octet);
        response.Http.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(200);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Success);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [Fact]
    public void HttpPacketTransferBinaryContent_WithStatusAndState_ReturnsCustomStatusAndStateBinaryResponse()
    {
        // Arrange
        var bytes = new byte[] { 0xAB, 0xCD };
        
        // Act
        var response = ConnectorResponses.HttpPacketTransferBinaryContent(bytes, MediaTypeNames.Application.Octet, ConnectorName, 206, ProcessPacketState.RetryUnchanged);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(bytes);
        response.ContentType.Should().Be(MediaTypeNames.Application.Octet);
        response.Http.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(206);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.RetryUnchanged);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [Fact]
    public void HttpPacketTransferText_WhenCalled_ReturnsTextResponse()
    {
        // Arrange
        const string txt = "retry later";
        
        // Act
        var response = ConnectorResponses.HttpPacketTransferText(txt, ConnectorName);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(txt));
        response.ContentType.Should().Be(MediaTypeNames.Text.Plain);
        response.Http.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(200);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Success);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [Fact]
    public void HttpPacketTransferText_WithStatusAndState_ReturnsCustomStatusAndStateTextResponse()
    {
        // Arrange
        const string txt = "retry later";
        
        // Act
        var response = ConnectorResponses.HttpPacketTransferText(txt, ConnectorName, 409, ProcessPacketState.Retry);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(txt));
        response.ContentType.Should().Be(MediaTypeNames.Text.Plain);
        response.Http.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(409);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Retry);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [Fact]
    public void HttpPacketTransferJson_WhenCalled_ReturnsJsonResponse()
    {
        // Arrange
        var dto = new { value = 42 };
        var expected = JsonSerializer.SerializeToUtf8Bytes(dto);
        
        // Act
        var response = ConnectorResponses.HttpPacketTransferJson(dto, ConnectorName);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(expected);
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.Http.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(200);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Success);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [Fact]
    public void HttpPacketTransferJson_WithStatusAndState_ReturnsCustomStatusAndStateJsonResponse()
    {
        // Arrange
        var dto = new { value = 42 };
        var expected = JsonSerializer.SerializeToUtf8Bytes(dto);
        
        // Act
        var response = ConnectorResponses.HttpPacketTransferJson(dto, ConnectorName, 207, ProcessPacketState.Error);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(expected);
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.Http.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(207);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Error);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [Theory]
    [InlineData(200, ProcessPacketState.Success)]
    [InlineData(202, ProcessPacketState.Retry)]
    [InlineData(406, ProcessPacketState.Error)]
    [InlineData(503, ProcessPacketState.FatalError)]
    [InlineData(100, ProcessPacketState.InProgress)]
    [InlineData(204, ProcessPacketState.RetryUnchanged)]
    public void HttpPacketTransferState_WhenCalled_ReturnsStatusAndStateResponse(int status, ProcessPacketState state)
    {
        // Act
        var response = ConnectorResponses.HttpPacketTransferState(ConnectorName, status, state);
        
        // Assert
        response.Should().NotBeNull();
        response.Content.IsEmpty.Should().BeTrue();
        response.ContentType.Should().BeEmpty();
        response.Http.Should().NotBeNull();
        response.PacketTransfer.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(status);
        response.PacketTransfer.ProcessPacketState.Should().Be(state);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [Fact]
    public void HttpPacketTransferProblem_WhenCalled_ReturnsProblemResponse()
    {
        // Arrange
        const string title = "Internal error";
        
        // Act
        var response = ConnectorResponses.HttpPacketTransferProblem(title, ConnectorName);
        
        // Assert
        response.Should().NotBeNull();
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.Http.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(500);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.FatalError);
        response.ConnectorName.Should().Be(ConnectorName);

        var doc = JsonDocument.Parse(response.Content);
        doc.RootElement.GetProperty("title").GetString().Should().Be(title);
    }

    [Fact]
    public void HttpPacketTransferProblem_WithOptionalValues_ReturnsProblemResponse()
    {
        // Arrange
        const string title = "Upstream failed";
        const string detail = "timeout";
        
        // Act
        var response = ConnectorResponses.HttpPacketTransferProblem(title, ConnectorName, statusCode: 504, state: ProcessPacketState.Error, detail: detail);
        
        // Assert
        response.Should().NotBeNull();
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.Http.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(504);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Error);
        response.ConnectorName.Should().Be(ConnectorName);

        var doc = JsonDocument.Parse(response.Content);
        doc.RootElement.GetProperty("title").GetString().Should().Be(title);
        doc.RootElement.GetProperty("detail").GetString().Should().Be(detail);
    }
}
