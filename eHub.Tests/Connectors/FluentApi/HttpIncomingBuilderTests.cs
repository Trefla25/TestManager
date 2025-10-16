using eHub.PlugIn;
using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;

namespace eHub.Tests.Connectors.FluentApi;

[TestClass]
public class HttpIncomingBuilderTests
{
    private readonly HttpIncomingBuilder _builder = new();

    [TestMethod]
    public void AddEndpoint_WithBuilder_AddsEndpoint()
    {
        var configured = false;
        _builder.AddEndpoint("endpoint1", ep => configured = true);

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.Endpoints.Should().ContainKey("endpoint1");
        configured.Should().BeTrue();
    }

    [TestMethod]
    public void AddEndpoint_WithConfig_AddsEndpoint()
    {
        var dummyConfig = new HttpConnectorEndpointConfig { Path = "/dummy" };
        _builder.AddEndpoint("endpoint2", dummyConfig);

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.Endpoints.Should().ContainKey("endpoint2");
        config.Endpoints["endpoint2"].Should().BeEquivalentTo(dummyConfig);
    }

    [TestMethod]
    public void ConfigureAuthentication_WhenCalled_ConfiguresAuthentication()
    {
        var configured = false;
        _builder.ConfigureAuthentication(auth => configured = true);

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.Auth.Should().NotBeNull();
        config.Auth.Enabled.Should().BeTrue();
        configured.Should().BeTrue();
    }

    [TestMethod]
    public void ConfigureKestrel_WhenCalled_ConfiguredKestrel()
    {
        var configured = false;
        _builder.ConfigureKestrel(k => configured = true);

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.KestrelBuilder.Should().NotBeNull();
        configured.Should().BeTrue();
    }

    [TestMethod]
    public void ExcludeEndpointFromPacketTransfer_WithSinglePath_ExcludesEndpoint()
    {
        _builder.ExcludePathFromPacketTransfer("/exclude");

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.ExcludedEndpointsFromPacketTransfer.Should().Contain("/exclude");
    }

    [TestMethod]
    public void ExcludeEndpointFromPacketTransfer_WithMultiplePath_ExcludesEndpoints()
    {
        _builder.ExcludePathFromPacketTransfer("/exclude1");
        _builder.ExcludePathFromPacketTransfer("/exclude2")
            .ExcludePathFromPacketTransfer("/exclude3");

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.ExcludedEndpointsFromPacketTransfer.Should().BeEquivalentTo(["/exclude1", "/exclude2", "/exclude3"]);
    }

    [TestMethod]
    public void SetInsertUnauthorizedPackets_WithTrue_SetsProperty()
    {
        _builder.StoreUnauthorizedPackets();

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.InsertUnauthorizedPackets.Should().BeTrue();
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
            .AddEndpoint("ep1", ep => { })
            .ConfigureAuthentication(auth => { })
            .ConfigureKestrel(k => { })
            .ExcludePathFromPacketTransfer("/exclude")
            .StoreUnauthorizedPackets()
            .EnableResendOnInterrupt()
            .SetStoreMode(StoreMode.Persistent);

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.Endpoints.Should().ContainKey("ep1");
        config.Auth.Should().NotBeNull();
        config.Auth.Enabled.Should().BeTrue();
        config.ExcludedEndpointsFromPacketTransfer.Should().Contain("/exclude");
        config.InsertUnauthorizedPackets.Should().BeTrue();
        config.ResendInProgressPacketsOnInterrupt.Should().BeTrue();
        config.StoreMode.Should().Be(StoreMode.Persistent);
    }
}
