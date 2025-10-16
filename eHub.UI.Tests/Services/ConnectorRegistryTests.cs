using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using eHub.Contracts;
using eHub.PlugIn.UI;
using eHub.UI.Services;
using eMessenger;
using eMessenger.Tests;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace eHub.UI.Tests.Services;

[TestClass]
public class ConnectorRegistryTests
{
    private IMessenger _messenger = default!;
    private ILogger<ConnectorRegistry> _logger = default!;
    private ConnectorRegistry _connectorRegistry = default!;

    [TestInitialize]
    public void TestInitialize()
    {
        _messenger = TestingMessenger.CreateScoped();
        _logger = Substitute.For<ILogger<ConnectorRegistry>>();
        _connectorRegistry = new ConnectorRegistry(_messenger, _logger);
    }

    [TestMethod]
    public async Task WorkAsync_WhenNewConnectorsFound_RaisesActiveConnectorsChanged()
    {
        var connectorId = new ConnectorIdentifier("Test", "TestConnector");
        var keepAliveDto = new ConnectorKeepAliveDto(connectorId);
        var uiData = new ConnectorUiData("TestConnector", "TestConnector", new UIViewConfig());

        await _messenger.AnswerAsync<ConnectorKeepAliveDto>(ConnectorContract.PollAliveConnectorsTopic(), () => keepAliveDto);
        await _messenger.AnswerAsync<ConnectorUiData>(ConnectorContract.UIGetTopic(connectorId), () => uiData);

        var eventRaised = false;
        _connectorRegistry.ActiveConnectorsChanged += () => eventRaised = true;

        await _connectorRegistry.WorkAsync();

        eventRaised.Should().BeTrue();
        _connectorRegistry.ActiveConnectors.Should().ContainKey(connectorId);

    }

    [TestMethod]
    public async Task WorkAsync_WhenNoChange_DoesNothing()
    {
        var connectorId = new ConnectorIdentifier("Test", "TestConnector");
        var keepAliveDto = new ConnectorKeepAliveDto(connectorId);
        var uiData = new ConnectorUiData("TestConnector", "TestConnector", new UIViewConfig());

        await _messenger.AnswerAsync<ConnectorKeepAliveDto>(ConnectorContract.PollAliveConnectorsTopic(), () => keepAliveDto);
        await _messenger.AnswerAsync<ConnectorUiData>(ConnectorContract.UIGetTopic(connectorId), () => uiData);

        bool eventRaised = false;
        _connectorRegistry.ActiveConnectorsChanged += () => eventRaised = true;

        await _connectorRegistry.WorkAsync();
        // expected true but next WorkAsync should be false
        eventRaised = false;
        await _connectorRegistry.WorkAsync();

        eventRaised.Should().BeFalse();

    }

    [TestMethod]
    public async Task WorkAsync_WhenConnectorDisconnects_RemovesFromActiveConnectors()
    {
        var connectorId = new ConnectorIdentifier("Test", "TestConnector");
        var keepAliveDto = new ConnectorKeepAliveDto(connectorId);

        var uiData = new ConnectorUiData("TestConnector", "TestConnector", new UIViewConfig());

        await _messenger.AnswerAsync<ConnectorKeepAliveDto>(ConnectorContract.PollAliveConnectorsTopic(), () => keepAliveDto);
        await _messenger.AnswerAsync<ConnectorUiData>(ConnectorContract.UIGetTopic(connectorId), () => uiData);

        await _connectorRegistry.WorkAsync();
        _connectorRegistry.ActiveConnectors.Should().ContainKey(connectorId);

        await _messenger.AnswerAsync<List<ConnectorKeepAliveDto>>(ConnectorContract.PollAliveConnectorsTopic(), () => []); 
        bool eventRaised = false;
        _connectorRegistry.ActiveConnectorsChanged += () => eventRaised = true;
        await _connectorRegistry.WorkAsync();

        eventRaised.Should().BeTrue();
        _connectorRegistry.ActiveConnectors.Should().NotContainKey(connectorId);
    }

}


