using eHub.Contracts;
using eHub.PlugIn.UI;
using eHub.UI.Services;
using eMessenger;
using eMessenger.Tests;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace eHub.UI.Tests.Services;

public class ConnectorRegistryTests
{
    private readonly IMessenger _messenger;
    private readonly ConnectorRegistry _connectorRegistry;
    
    public ConnectorRegistryTests()
    {
        _messenger = TestingMessenger.CreateScoped();
        var logger = Substitute.For<ILogger<ConnectorRegistry>>();
        _connectorRegistry = new ConnectorRegistry(_messenger, logger);
    }
    
    [Fact]
    public async Task WorkAsync_WhenNewConnectorsFound_RaisesActiveConnectorsChanged()
    {
        // Arrange
        var connectorId = new ConnectorIdentifier("Test", "TestConnector");
        var keepAliveDto = new ConnectorKeepAliveDto(connectorId);
        var uiData = new ConnectorUiData("TestConnector", "TestConnector", new UIViewConfig());

        await _messenger.AnswerAsync<ConnectorKeepAliveDto>(ConnectorContract.PollAliveConnectorsTopic(), () => keepAliveDto);
        await _messenger.AnswerAsync<ConnectorUiData>(ConnectorContract.UIGetTopic(connectorId), () => uiData);

        var eventRaised = false;
        _connectorRegistry.ActiveConnectorsChanged += () => eventRaised = true;
        
        // Act
        await _connectorRegistry.WorkAsync();
        
        // Assert
        eventRaised.Should().BeTrue();
        _connectorRegistry.ActiveConnectors.Should().ContainKey(connectorId);
    }

    [Fact]
    public async Task WorkAsync_WhenNoChange_DoesNothing()
    {
        // Arrange
        var connectorId = new ConnectorIdentifier("Test", "TestConnector");
        var keepAliveDto = new ConnectorKeepAliveDto(connectorId);
        var uiData = new ConnectorUiData("TestConnector", "TestConnector", new UIViewConfig());

        await _messenger.AnswerAsync<ConnectorKeepAliveDto>(ConnectorContract.PollAliveConnectorsTopic(), () => keepAliveDto);
        await _messenger.AnswerAsync<ConnectorUiData>(ConnectorContract.UIGetTopic(connectorId), () => uiData);

        var eventRaised = false;
        _connectorRegistry.ActiveConnectorsChanged += () => eventRaised = true;
        
        // Act
        await _connectorRegistry.WorkAsync();
        
        // expected true but next WorkAsync should be false
        eventRaised = false;
        await _connectorRegistry.WorkAsync();
        
        // Assert
        eventRaised.Should().BeFalse();
    }

    [Fact]
    public async Task WorkAsync_WhenConnectorDisconnects_RemovesFromActiveConnectors()
    {
        // Arrange
        var connectorId = new ConnectorIdentifier("Test", "TestConnector");
        var keepAliveDto = new ConnectorKeepAliveDto(connectorId);

        var uiData = new ConnectorUiData("TestConnector", "TestConnector", new UIViewConfig());

        await _messenger.AnswerAsync(ConnectorContract.PollAliveConnectorsTopic(), () => keepAliveDto);
        await _messenger.AnswerAsync(ConnectorContract.UIGetTopic(connectorId), () => uiData);
        
        // Act
        await _connectorRegistry.WorkAsync();
        _connectorRegistry.ActiveConnectors.Should().ContainKey(connectorId);

        await _messenger.AnswerAsync<List<ConnectorKeepAliveDto>>(ConnectorContract.PollAliveConnectorsTopic(), () => []); 
        var eventRaised = false;
        
        _connectorRegistry.ActiveConnectorsChanged += () => eventRaised = true;
        await _connectorRegistry.WorkAsync();

        // Assert
        eventRaised.Should().BeTrue();
        _connectorRegistry.ActiveConnectors.Should().NotContainKey(connectorId);
    }
}
