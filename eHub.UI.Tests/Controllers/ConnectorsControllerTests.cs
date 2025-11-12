using System.Text.Json;
using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eHub.PlugIn;
using eHub.UI.Controllers;
using eMessenger;
using eMessenger.Tests;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace eHub.UI.Tests.Controllers;

public class ConnectorsControllerTests
{
    private readonly IMessenger _messenger;
    private readonly ConnectorsController _controller;

    public ConnectorsControllerTests()
    {
        _messenger = TestingMessenger.CreateScoped();
        _controller = new ConnectorsController(_messenger);
    }

    [Fact]
    public async Task DownloadPacketsAsJson_WhenPacketsExist_ReturnsJsonFile()
    {
        // Arrange
        var connectorIdentifier = new ConnectorIdentifier("TestConnector", "TestConnector");
        var filter = new PacketRequestDto();
        var filterJson = JsonSerializer.Serialize(filter);
        const string title = "testExport";

        var expectedResponse = new ConnectorPacketsExportDto([
            new PacketDto { Id = 1, ConnectorName = "TestConnector1", Channel = "A", DateCreated = DateTime.Now, Data = "Data1", ParentId = null, Status = PacketStatus.Enqueued },
            new PacketDto { Id = 2, ConnectorName = "TestConnector2", Channel = "B", DateCreated = DateTime.Now.AddMinutes(1), Data = "Data2", ParentId = 1, Status = PacketStatus.Error }
        ]);

        await _messenger.AnswerAsync<PacketRequestDto, ConnectorPacketsExportDto>(
            ConnectorContract.PacketExportTopic(connectorIdentifier),
            _ => expectedResponse
        );
        
        // Act
        var result = await _controller.DownloadPacketsAsJson(connectorIdentifier, filterJson, title);
        
        // Assert
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

    [Fact]
    public async Task DownloadPacketsAsJson_WhenNoPacketsExist_ReturnsNoContent()
    {
        // Arrange
        var connectorIdentifier = new ConnectorIdentifier("TestConnector", "TestConnector");
        var filterJson = JsonSerializer.Serialize(new PacketRequestDto());
        const string title = "testExport";

        await _messenger.AnswerAsync<PacketRequestDto, ConnectorPacketsExportDto>(
            ConnectorContract.PacketExportTopic(connectorIdentifier),
            _ => null!
        );
        
        // Act
        var result = await _controller.DownloadPacketsAsJson(connectorIdentifier, filterJson, title);
        
        // Assert
        result.Should().BeOfType<NoContentResult>();
    }
}
