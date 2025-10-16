using eHub.PlugIn;
using eHub.PlugIn.Communication;
using FluentAssertions;
using System.Net.Mime;
using System.Text;
using System.Text.Json;

namespace eHub.Tests.Connectors;

[TestClass]
public class ConnectorResponsesTests
{
    private const string ConnectorName = "MyConnector";

    [TestMethod]
    public void Acknowledge_WhenCalled_ReturnsEmptyResponse()
    {
        var response = ConnectorResponses.Acknowledge(ConnectorName);

        response.Should().NotBeNull();
        response.Content.IsEmpty.Should().BeTrue();
        response.ContentType.Should().BeEmpty();
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [TestMethod]
    public void Content_WhenCalled_ReturnsContentResponse()
    {
        var json = "{\"message\":\"test\"}";
        var response = ConnectorResponses.Content(json, MediaTypeNames.Application.Json, ConnectorName);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(json));
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [TestMethod]
    public void BinaryContent_WhenCalled_ReturnsContentRespose()
    {
        var bytes = new byte[] { 0x01, 0xFF, 0x42 };
        var response = ConnectorResponses.BinaryContent(bytes, MediaTypeNames.Application.Octet, ConnectorName);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(bytes);
        response.ContentType.Should().Be(MediaTypeNames.Application.Octet);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [TestMethod]
    public void Text_WhenCalled_ReturnsPlainTextResponse()
    {
        var text = "hello!";
        var response = ConnectorResponses.Text(text, ConnectorName);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(text));
        response.ContentType.Should().Be(MediaTypeNames.Text.Plain);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [TestMethod]
    public void Json_WhenCalled_ReturnsJsonResponse()
    {
        var obj = new { Foo = "bar", Count = 123 };
        var expectedJson = JsonSerializer.SerializeToUtf8Bytes(obj);
        var response = ConnectorResponses.Json(obj, ConnectorName);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(expectedJson);
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [TestMethod]
    public void HttpContent_WithoutStatus_Returns200StatusContentResponse()
    {
        var body = "{\"ok\":true}";
        var statusCode = 200;
        var response = ConnectorResponses.HttpContent(body, MediaTypeNames.Application.Json, ConnectorName);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(body));
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.ConnectorName.Should().Be(ConnectorName);
        response.Http!.StatusCode.Should().Be(statusCode);
    }

    [TestMethod]
    public void HttpContent_WithStatus_ReturnsCustomStatusContentResponse()
    {
        var body = "created!";
        var customCode = 201;
        var response = ConnectorResponses.HttpContent(body, MediaTypeNames.Text.Plain, ConnectorName, customCode);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(body));
        response.ContentType.Should().Be(MediaTypeNames.Text.Plain);
        response.ConnectorName.Should().Be(ConnectorName);
        response.Http!.StatusCode.Should().Be(customCode);
    }

    [TestMethod]
    public void HttpBinaryContent_WithoutStatus_Returns200StatusBinaryResponse()
    {
        var bytes = new byte[] { 0x10, 0x20, 0x30 };
        var response = ConnectorResponses.HttpBinaryContent(bytes, MediaTypeNames.Application.Octet, ConnectorName);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(bytes);
        response.ContentType.Should().Be(MediaTypeNames.Application.Octet);
        response.ConnectorName.Should().Be(ConnectorName);
        response.Http!.StatusCode.Should().Be(200);
    }

    [TestMethod]
    public void HttpBinaryContent_WithStatus_ReturnsCustomStatusBinaryResponse()
    {
        var bytes = new byte[] { 0x10, 0x20, 0x30 };
        var response = ConnectorResponses.HttpBinaryContent(bytes, MediaTypeNames.Application.Octet, ConnectorName, 206);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(bytes);
        response.ContentType.Should().Be(MediaTypeNames.Application.Octet);
        response.ConnectorName.Should().Be(ConnectorName);
        response.Http!.StatusCode.Should().Be(206);
    }

    [TestMethod]
    public void HttpText_WithoutStatus_Returns200StatusPlainTextResponse()
    {
        var msg = "all green";
        var response = ConnectorResponses.HttpText(msg, ConnectorName);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(msg));
        response.ContentType.Should().Be(MediaTypeNames.Text.Plain);
        response.ConnectorName.Should().Be(ConnectorName);
        response.Http!.StatusCode.Should().Be(200);
    }

    [TestMethod]
    public void HttpText_WithStatus_ReturnsCustomStatusPlainTextResponse()
    {
        var msg = "all green";
        var response = ConnectorResponses.HttpText(msg, ConnectorName, 202);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(msg));
        response.ContentType.Should().Be(MediaTypeNames.Text.Plain);
        response.ConnectorName.Should().Be(ConnectorName);
        response.Http!.StatusCode.Should().Be(202);
    }

    [TestMethod]
    public void HttpJson_WithoutStatus_Returns200StatusJsonResponse()
    {
        var payload = new { x = 1, y = 2 };
        var expected = JsonSerializer.SerializeToUtf8Bytes(payload);
        var response = ConnectorResponses.HttpJson(payload, ConnectorName);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(expected);
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.ConnectorName.Should().Be(ConnectorName);
        response.Http!.StatusCode.Should().Be(200);
    }

    [TestMethod]
    public void HttpJson_WithStatus_ReturnsCustomStatusJsonResponse()
    {
        var payload = new { x = 1, y = 2 };
        var expected = JsonSerializer.SerializeToUtf8Bytes(payload);
        var response = ConnectorResponses.HttpJson(payload, ConnectorName, 220);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(expected);
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.ConnectorName.Should().Be(ConnectorName);
        response.Http!.StatusCode.Should().Be(220);
    }

    [DataTestMethod]
    [DataRow(102)]
    [DataRow(204)]
    [DataRow(302)]
    [DataRow(418)]
    [DataRow(509)]
    public void HttpStatus_WhenCalled_ReturnsStatusOnlyResponse(int status)
    {
        var response = ConnectorResponses.HttpStatus(ConnectorName, status);

        response.Should().NotBeNull();
        response.Content.IsEmpty.Should().BeTrue();
        response.ContentType.Should().BeEmpty();
        response.ConnectorName.Should().Be(ConnectorName);
        response.Http!.StatusCode.Should().Be(status);
    }

    [DataTestMethod]
    [DataRow(nameof(ConnectorResponses.HttpOk), 200)]
    [DataRow(nameof(ConnectorResponses.HttpCreated), 201)]
    [DataRow(nameof(ConnectorResponses.HttpAccepted), 202)]
    public void KnownHttpSuccessStatusMethods_WhenCalled_ReturnExpectedStatus(string methodName, int expectedStatus)
    {
        var method = typeof(ConnectorResponses).GetMethod(methodName)!;
        var response = (ConnectorResponse)method.Invoke(null, [ConnectorName])!;

        response.Should().NotBeNull();
        response.Http!.StatusCode.Should().Be(expectedStatus);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [TestMethod]
    public void HttpBadRequest_WithoutDetail_Returns400ProblemResponse()
    {
        var response = ConnectorResponses.HttpBadRequest(ConnectorName);

        response.Should().NotBeNull();
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.Http!.StatusCode.Should().Be(400);
        response.ConnectorName.Should().Be(ConnectorName);

        var json = JsonDocument.Parse(response.Content);
        json.RootElement.GetProperty("title").GetString().Should().Be("Bad Request");
    }

    [TestMethod]
    public void HttpBadRequest_WithDetail_Returns400ProblemResponse()
    {
        var detail = "Missing field";
        var response = ConnectorResponses.HttpBadRequest(ConnectorName, detail);

        response.Should().NotBeNull();
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.Http!.StatusCode.Should().Be(400);
        response.ConnectorName.Should().Be(ConnectorName);

        var json = JsonDocument.Parse(response.Content);
        json.RootElement.GetProperty("title").GetString().Should().Be("Bad Request");
        json.RootElement.GetProperty("detail").GetString().Should().Be(detail);
    }

    [TestMethod]
    public void HttpBadGateway_WithoutDetail_Returns502ProblemResponse()
    {
        var response = ConnectorResponses.HttpBadGateway(ConnectorName);

        response.Should().NotBeNull();
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.Http!.StatusCode.Should().Be(502);
        response.ConnectorName.Should().Be(ConnectorName);

        var json = JsonDocument.Parse(response.Content);
        json.RootElement.GetProperty("title").GetString().Should().Be("Bad Gateway");
    }

    [TestMethod]
    public void HttpBadGateway_WithDetail_Returns502ProblemResponse()
    {
        var detail = "Communication error";
        var response = ConnectorResponses.HttpBadGateway(ConnectorName, detail);

        response.Should().NotBeNull();
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.Http!.StatusCode.Should().Be(502);
        response.ConnectorName.Should().Be(ConnectorName);

        var json = JsonDocument.Parse(response.Content);
        json.RootElement.GetProperty("title").GetString().Should().Be("Bad Gateway");
        json.RootElement.GetProperty("detail").GetString().Should().Be(detail);
    }

    [TestMethod]
    public void HttpRedirect_WithoutStatus_Returns302StatusWithLocationHeaderResponse()
    {
        var location = "https://example.com/next";
        var response = ConnectorResponses.HttpRedirect(location, ConnectorName);

        response.Should().NotBeNull();
        response.Http!.StatusCode.Should().Be(302);
        response.Http.Headers!["Location"].ToString().Should().Be(location);
        response.Content.IsEmpty.Should().BeTrue();
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [TestMethod]
    public void HttpRedirect_WithStatus_ReturnsCustomStatusWithLocationHeaderResponse()
    {
        var location = "https://example.com/next";
        var response = ConnectorResponses.HttpRedirect(location, ConnectorName, 301);

        response.Should().NotBeNull();
        response.Http!.StatusCode.Should().Be(301);
        response.Http.Headers!["Location"].ToString().Should().Be(location);
        response.Content.IsEmpty.Should().BeTrue();
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [TestMethod]
    public void HttpProblem_WithoutStatus_Returns500ProblemPayload()
    {
        var title = "Internal Oops";
        var response = ConnectorResponses.HttpProblem(title, ConnectorName);

        response.Should().NotBeNull();
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.Http!.StatusCode.Should().Be(500);
        response.ConnectorName.Should().Be(ConnectorName);

        var doc = JsonDocument.Parse(response.Content);
        doc.RootElement.GetProperty("title").GetString().Should().Be(title);
    }

    [TestMethod]
    public void HttpProblem_WithStatus_ReturnsCustomStatusProblemPayload()
    {
        var title = "Internal Oops";
        var response = ConnectorResponses.HttpProblem(title, ConnectorName, 505);

        response.Should().NotBeNull();
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.Http!.StatusCode.Should().Be(505);
        response.ConnectorName.Should().Be(ConnectorName);

        var doc = JsonDocument.Parse(response.Content);
        doc.RootElement.GetProperty("title").GetString().Should().Be(title);
    }

    [TestMethod]
    public void HttpProblem_WithDetail_ReturnsProblemPayloadWithDetail()
    {
        var title = "Internal Oops";
        var detail = "Stack overflow";
        var response = ConnectorResponses.HttpProblem(title, ConnectorName, detail: detail);

        response.Should().NotBeNull();
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.Http.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(500);
        response.ConnectorName.Should().Be(ConnectorName);

        var doc = JsonDocument.Parse(response.Content);
        doc.RootElement.GetProperty("title").GetString().Should().Be(title);
        doc.RootElement.GetProperty("detail").GetString().Should().Be(detail);
    }

    [TestMethod]
    public void PacketTransferContent_WithoutState_ReturnsSuccessStateContentResponse()
    {
        var body = "<ok />";
        var response = ConnectorResponses.PacketTransferContent(body, MediaTypeNames.Text.Xml, ConnectorName);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(body));
        response.ContentType.Should().Be(MediaTypeNames.Text.Xml);
        response.ConnectorName.Should().Be(ConnectorName);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Success);
        response.Http.Should().BeNull();
    }

    [TestMethod]
    public void PacketTransferContent_WithState_ReturnsCustomStateContentResponse()
    {
        var body = "<ok />";
        var response = ConnectorResponses.PacketTransferContent(body, MediaTypeNames.Text.Xml, ConnectorName, ProcessPacketState.Retry);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(body));
        response.ContentType.Should().Be(MediaTypeNames.Text.Xml);
        response.ConnectorName.Should().Be(ConnectorName);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Retry);
        response.Http.Should().BeNull();
    }

    [TestMethod]
    public void PacketTransferBinaryContent_WithoutState_ReturnsSuccessStateBinaryResponse()
    {
        var bytes = new byte[] { 0xFE, 0xED, 0xFA, 0xCE };
        var response = ConnectorResponses.PacketTransferBinaryContent(bytes, MediaTypeNames.Application.Octet, ConnectorName);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(bytes);
        response.ContentType.Should().Be(MediaTypeNames.Application.Octet);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Success);
        response.Http.Should().BeNull();
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [TestMethod]
    public void PacketTransferBinaryContent_WithState_ReturnsCustomStateBinaryResponse()
    {
        var bytes = new byte[] { 0xFE, 0xED, 0xFA, 0xCE };
        var response = ConnectorResponses.PacketTransferBinaryContent(bytes, MediaTypeNames.Application.Octet, ConnectorName, ProcessPacketState.Error);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(bytes);
        response.ContentType.Should().Be(MediaTypeNames.Application.Octet);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Error);
        response.Http.Should().BeNull();
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [TestMethod]
    public void PacketTransferText_WithoutState_ReturnsSuccessStatePlainTextResponse()
    {
        var text = "Some text";
        var response = ConnectorResponses.PacketTransferText(text, ConnectorName);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(text));
        response.ContentType.Should().Be(MediaTypeNames.Text.Plain);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Success);
        response.Http.Should().BeNull();
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [TestMethod]
    public void PacketTransferText_WithState_ReturnsCustomStatePlainTextResponse()
    {
        var text = "Some text";
        var response = ConnectorResponses.PacketTransferText(text, ConnectorName, ProcessPacketState.InProgress);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(text));
        response.ContentType.Should().Be(MediaTypeNames.Text.Plain);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.InProgress);
        response.Http.Should().BeNull();
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [TestMethod]
    public void PacketTransferJson_WithoutState_ReturnsSuccessStateJsonResponse()
    {
        var dto = new { ok = true };
        var expected = JsonSerializer.SerializeToUtf8Bytes(dto);
        var response = ConnectorResponses.PacketTransferJson(dto, ConnectorName);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(expected);
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Success);
        response.Http.Should().BeNull();
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [TestMethod]
    public void PacketTransferJson_WithState_ReturnsCustomStateJsonResponse()
    {
        var dto = new { ok = true };
        var expected = JsonSerializer.SerializeToUtf8Bytes(dto);
        var response = ConnectorResponses.PacketTransferJson(dto, ConnectorName, ProcessPacketState.FatalError);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(expected);
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.FatalError);
        response.Http.Should().BeNull();
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [DataTestMethod]
    [DataRow(ProcessPacketState.Success)]
    [DataRow(ProcessPacketState.Retry)]
    [DataRow(ProcessPacketState.Error)]
    [DataRow(ProcessPacketState.FatalError)]
    [DataRow(ProcessPacketState.InProgress)]
    [DataRow(ProcessPacketState.RetryUnchanged)]
    public void PacketTransferState_WhenCalled_ReturnsExpectedState(ProcessPacketState state)
    {
        var response = ConnectorResponses.PacketTransferState(ConnectorName, state);

        response.Should().NotBeNull();
        response.Content.IsEmpty.Should().BeTrue();
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(state);
        response.Http.Should().BeNull();
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [TestMethod]
    public void PacketTransferSuccess_WhenCalled_ReturnsSuccessState()
    {
        var response = ConnectorResponses.PacketTransferSuccess(ConnectorName);

        response.Should().NotBeNull();
        response.Content.IsEmpty.Should().BeTrue();
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Success);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [TestMethod]
    public void PacketTransferFatalError_WithoutDetail_ReturnsFatalErrorState()
    {
        var response = ConnectorResponses.PacketTransferFatalError(ConnectorName);

        response.Should().NotBeNull();
        response.Content.IsEmpty.Should().BeTrue();
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.FatalError);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [TestMethod]
    public void PacketTransferFatalError_WithDetail_ReturnsFatalErrorState()
    {
        var detail = "hard failure";
        var response = ConnectorResponses.PacketTransferFatalError(ConnectorName, detail);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(detail));
        response.ContentType.Should().Be(MediaTypeNames.Text.Plain);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.FatalError);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    // ---------- HTTP + Packet‑transfer helpers ----------

    [TestMethod]
    public void HttpPacketTransferContent_WhenCalled_ReturnsContentResponse()
    {
        var body = "good";
        var response = ConnectorResponses.HttpPacketTransferContent(body, MediaTypeNames.Text.Plain, ConnectorName);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(body));
        response.ContentType.Should().Be(MediaTypeNames.Text.Plain);
        response.Http.Should().NotBeNull();
        response.PacketTransfer.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(200);
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Success);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [TestMethod]
    public void HttpPacketTransferContent_WithStatusAndState_ReturnsCustomStatusAndStateContentResponse()
    {
        var body = "in progress";
        var response = ConnectorResponses.HttpPacketTransferContent(body, MediaTypeNames.Text.Plain, ConnectorName, 202, ProcessPacketState.InProgress);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(body));
        response.ContentType.Should().Be(MediaTypeNames.Text.Plain);
        response.Http.Should().NotBeNull();
        response.PacketTransfer.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(202);
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.InProgress);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [TestMethod]
    public void HttpPacketTransferBinaryContent_WhenCalled_ReturnsBinaryResponse()
    {
        var bytes = new byte[] { 0xAB, 0xCD };
        var response = ConnectorResponses.HttpPacketTransferBinaryContent(bytes, MediaTypeNames.Application.Octet, ConnectorName);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(bytes);
        response.ContentType.Should().Be(MediaTypeNames.Application.Octet);
        response.Http.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(200);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Success);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [TestMethod]
    public void HttpPacketTransferBinaryContent_WithStatusAndState_ReturnsCustomStatusAndStateBinaryResponse()
    {
        var bytes = new byte[] { 0xAB, 0xCD };
        var response = ConnectorResponses.HttpPacketTransferBinaryContent(bytes, MediaTypeNames.Application.Octet, ConnectorName, 206, ProcessPacketState.RetryUnchanged);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(bytes);
        response.ContentType.Should().Be(MediaTypeNames.Application.Octet);
        response.Http.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(206);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.RetryUnchanged);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [TestMethod]
    public void HttpPacketTransferText_WhenCalled_ReturnsTextResponse()
    {
        var txt = "retry later";
        var response = ConnectorResponses.HttpPacketTransferText(txt, ConnectorName);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(txt));
        response.ContentType.Should().Be(MediaTypeNames.Text.Plain);
        response.Http.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(200);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Success);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [TestMethod]
    public void HttpPacketTransferText_WithStatusAndState_ReturnsCustomStatusAndStateTextResponse()
    {
        var txt = "retry later";
        var response = ConnectorResponses.HttpPacketTransferText(txt, ConnectorName, 409, ProcessPacketState.Retry);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes(txt));
        response.ContentType.Should().Be(MediaTypeNames.Text.Plain);
        response.Http.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(409);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Retry);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [TestMethod]
    public void HttpPacketTransferJson_WhenCalled_ReturnsJsonResponse()
    {
        var dto = new { value = 42 };
        var expected = JsonSerializer.SerializeToUtf8Bytes(dto);
        var response = ConnectorResponses.HttpPacketTransferJson(dto, ConnectorName);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(expected);
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.Http.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(200);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Success);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [TestMethod]
    public void HttpPacketTransferJson_WithStatusAndState_ReturnsCustomStatusAndStateJsonResponse()
    {
        var dto = new { value = 42 };
        var expected = JsonSerializer.SerializeToUtf8Bytes(dto);
        var response = ConnectorResponses.HttpPacketTransferJson(dto, ConnectorName, 207, ProcessPacketState.Error);

        response.Should().NotBeNull();
        response.Content.ToArray().Should().BeEquivalentTo(expected);
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
        response.Http.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(207);
        response.PacketTransfer.Should().NotBeNull();
        response.PacketTransfer.ProcessPacketState.Should().Be(ProcessPacketState.Error);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [DataTestMethod]
    [DataRow(200, ProcessPacketState.Success)]
    [DataRow(202, ProcessPacketState.Retry)]
    [DataRow(406, ProcessPacketState.Error)]
    [DataRow(503, ProcessPacketState.FatalError)]
    [DataRow(100, ProcessPacketState.InProgress)]
    [DataRow(204, ProcessPacketState.RetryUnchanged)]
    public void HttpPacketTransferState_WhenCalled_ReturnsStatusAndStateResponse(int status, ProcessPacketState state)
    {
        var response = ConnectorResponses.HttpPacketTransferState(ConnectorName, status, state);

        response.Should().NotBeNull();
        response.Content.IsEmpty.Should().BeTrue();
        response.ContentType.Should().BeEmpty();
        response.Http.Should().NotBeNull();
        response.PacketTransfer.Should().NotBeNull();
        response.Http.StatusCode.Should().Be(status);
        response.PacketTransfer.ProcessPacketState.Should().Be(state);
        response.ConnectorName.Should().Be(ConnectorName);
    }

    [TestMethod]
    public void HttpPacketTransferProblem_WhenCalled_ReturnsProblemResponse()
    {
        var title = "Internal error";
        var response = ConnectorResponses.HttpPacketTransferProblem(title, ConnectorName);

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

    [TestMethod]
    public void HttpPacketTransferProblem_WithOptionalValues_ReturnsProblemResponse()
    {
        var title = "Upstream failed";
        var detail = "timeout";
        var response = ConnectorResponses.HttpPacketTransferProblem(title, ConnectorName, statusCode: 504, state: ProcessPacketState.Error, detail: detail);

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
