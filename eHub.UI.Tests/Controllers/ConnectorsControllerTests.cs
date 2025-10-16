using System.Text.Json;
using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eHub.PlugIn;
using eHub.UI.Controllers;
using eMessenger;
using eMessenger.Tests;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace eHub.UI.Tests.Controllers;

[TestClass]
public class ConnectorsControllerTests
{
    private IMessenger _messenger = default!;
    private ConnectorsController _controller = default!;

    [TestInitialize]
    public void Setup()
    {
        _messenger = TestingMessenger.CreateScoped();
        _controller = new ConnectorsController(_messenger);
    }

    [TestMethod]
    public async Task DownloadPacketsAsJson_WhenPacketsExist_ReturnsJsonFile()
    {
        var connectorIdentifier = new ConnectorIdentifier("TestConnector", "TestConnector");
        var filter = new PacketRequestDto();
        var filterJson = JsonSerializer.Serialize(filter);
        var title = "testExport";

        var expectedResponse = new ConnectorPacketsExportDto([
            new PacketDto { Id = 1, ConnectorName = "TestConnector1", Channel = "A", DateCreated = DateTime.Now, Data = "Data1", ParentId = null, Status = PacketStatus.Enqueued },
            new PacketDto { Id = 2, ConnectorName = "TestConnector2", Channel = "B", DateCreated = DateTime.Now.AddMinutes(1), Data = "Data2", ParentId = 1, Status = PacketStatus.Error }
        ]);

        await _messenger.AnswerAsync<PacketRequestDto, ConnectorPacketsExportDto>(
            ConnectorContract.PacketExportTopic(connectorIdentifier),
            _ => expectedResponse
        );

        var result = await _controller.DownloadPacketsAsJson(connectorIdentifier, filterJson, title);

        result.Should().BeOfType<FileContentResult>();
        var fileResult = (FileContentResult)result;
        fileResult.ContentType.Should().Be("application/json");
        fileResult.FileDownloadName.Should().Be("testExport.json");

        var deserializedContent = JsonSerializer.Deserialize<ConnectorPacketsExportDto>(fileResult.FileContents);
        deserializedContent.Should().NotBeNull();
        deserializedContent!.Packets.Should().HaveCount(2);
        for (var i = 0; i < expectedResponse.Packets.Length; i++)
        {
            deserializedContent.Packets[i].Should().Be(expectedResponse.Packets[i]);
        }
    }

    [TestMethod]
    public async Task DownloadPacketsAsJson_WhenNoPacketsExist_ReturnsNoContent()
    {
        var connectorIdentifier = new ConnectorIdentifier("TestConnector", "TestConnector");
        var filterJson = JsonSerializer.Serialize(new PacketRequestDto());
        var title = "testExport";

        await _messenger.AnswerAsync<PacketRequestDto, ConnectorPacketsExportDto>(
            ConnectorContract.PacketExportTopic(connectorIdentifier),
            _ => null!
        );

        var result = await _controller.DownloadPacketsAsJson(connectorIdentifier, filterJson, title);

        result.Should().BeOfType<NoContentResult>();
    }
}

