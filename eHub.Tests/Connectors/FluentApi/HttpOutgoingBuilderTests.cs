using eHub.PlugIn;
using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;

namespace eHub.Tests.Connectors.FluentApi;

[TestClass]
public class HttpOutgoingBuilderTests
{
    private readonly HttpOutgoingBuilder _builder = new();

    [TestMethod]
    public void AddApi_WhenCalled_AddsApiConfiguration()
    {
        var configured = false;
        _builder.AddApi("api1", api => configured = true);

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.Api.Should().ContainKey("api1");
        configured.Should().BeTrue();
    }

    [TestMethod]
    public void AddApi_WithMultipleApis_AddsAllApiConfiguration()
    {
        _builder.AddApi("api1", api => { });
        _builder.AddApi("api2", api => { });
        _builder.AddApi("api3", api => { });

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.Api.Should().ContainKey("api1");
        config.Api.Should().ContainKey("api2");
        config.Api.Should().ContainKey("api3");
    }

    [TestMethod]
    public void AddApi_WhenCalledMultipleTimes_ThrowsException()
    {
        _builder.AddApi("api1", api => { });

        var act = () => _builder.AddApi("api1", api => { });

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void AddEndpoint_WithBuilder_AddsEndpoint()
    {
        var configured = false;
        _builder.AddEndpoint("ep1", ep => configured = true);

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.Endpoints.Should().ContainKey("ep1");
        configured.Should().BeTrue();
    }

    [TestMethod]
    public void AddEndpoint_WithConfig_AddsEndpoint()
    {
        var dummyConfig = new HttpConnectorEndpointConfig { Path = "/dummyOut" };
        _builder.AddEndpoint("ep2", dummyConfig);

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.Endpoints.Should().ContainKey("ep2");
        config.Endpoints["ep2"].Should().BeEquivalentTo(dummyConfig);
    }

    [TestMethod]
    public void ExcludeEndpointFromPacketTransfer_WithSingleTopic_ExcludesEndpoint()
    {
        _builder.ExcludeTopicFromPacketTransfer("topic1");

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.ExcludedEndpointsFromPacketTransfer.Should().Contain("topic1");
    }

    [TestMethod]
    public void ExcludeEndpointFromPacketTransfer_WithMultipleTopics_ExcludesEndpoints()
    {
        _builder.ExcludeTopicFromPacketTransfer("topic1");
        _builder.ExcludeTopicFromPacketTransfer("topic2")
            .ExcludeTopicFromPacketTransfer("topic3");

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.ExcludedEndpointsFromPacketTransfer.Should().BeEquivalentTo(["topic1", "topic2", "topic3"]);
    }

    [TestMethod]
    public void SetResendInProgressPacketsOnInterrupt_WithTrue_SetsProperty()
    {
        _builder.EnableResendOnInterrupt();

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.ResendInProgressPacketsOnInterrupt.Should().BeTrue();
    }

    [TestMethod]
    public void SetResendPacketsOnCommunicationError_WithTrue_SetsProperty()
    {
        _builder.EnableResendOnCommunicationError();

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.ResendPacketsOnCommunicationError.Should().BeTrue();
    }

    [TestMethod]
    public void SetStoreMode_WithValidStoreMode_SetsStoreMode()
    {
        _builder.SetStoreMode(StoreMode.Persistent);

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.StoreMode.Should().Be(StoreMode.Persistent);
    }

    [TestMethod]
    public void Build_WhenAllMethodsChained_ConfiguresAllProperties()
    {
        _builder
            .AddApi("api1", api => { })
            .AddEndpoint("ep1", ep => { })
            .ExcludeTopicFromPacketTransfer("topic1")
            .EnableResendOnInterrupt()
            .EnableResendOnCommunicationError()
            .SetStoreMode(StoreMode.Persistent);

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.Api.Should().ContainKey("api1");
        config.Endpoints.Should().ContainKey("ep1");
        config.ExcludedEndpointsFromPacketTransfer.Should().Contain("topic1");
        config.ResendInProgressPacketsOnInterrupt.Should().BeTrue();
        config.ResendPacketsOnCommunicationError.Should().BeTrue();
        config.StoreMode.Should().Be(StoreMode.Persistent);
    }
}
