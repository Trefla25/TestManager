using eHub.PlugIn;
using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;

namespace eHub.Tests.Connectors.FluentApi;

public class HttpIncomingBuilderTests
{
    private readonly HttpIncomingBuilder _builder = new();

    [Fact]
    public void AddEndpoint_WithBuilder_AddsEndpoint()
    {
        // Arrange
        var configured = false;
        _builder.AddEndpoint("endpoint1", _ => configured = true);
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.Endpoints.Should().ContainKey("endpoint1");
        configured.Should().BeTrue();
    }

    [Fact]
    public void AddEndpoint_WithConfig_AddsEndpoint()
    {
        // Arrange
        var dummyConfig = new HttpConnectorEndpointConfig { Path = "/dummy" };
        _builder.AddEndpoint("endpoint2", dummyConfig);
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.Endpoints.Should().ContainKey("endpoint2");
        config.Endpoints["endpoint2"].Should().BeEquivalentTo(dummyConfig);
    }

    [Fact]
    public void ConfigureAuthentication_WhenCalled_ConfiguresAuthentication()
    {
        // Arrange
        var configured = false;
        _builder.ConfigureAuthentication(_ => configured = true);
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.Auth.Should().NotBeNull();
        config.Auth.Enabled.Should().BeTrue();
        configured.Should().BeTrue();
    }

    [Fact]
    public void ConfigureKestrel_WhenCalled_ConfiguredKestrel()
    {
        // Arrange
        var configured = false;
        _builder.ConfigureKestrel(_ => configured = true);
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.KestrelBuilder.Should().NotBeNull();
        configured.Should().BeTrue();
    }

    [Fact]
    public void ExcludeEndpointFromPacketTransfer_WithSinglePath_ExcludesEndpoint()
    {
        // Arrange
        _builder.ExcludePathFromPacketTransfer("/exclude");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.ExcludedEndpointsFromPacketTransfer.Should().Contain("/exclude");
    }

    [Fact]
    public void ExcludeEndpointFromPacketTransfer_WithMultiplePath_ExcludesEndpoints()
    {
        // Arrange
        _builder.ExcludePathFromPacketTransfer("/exclude1");
        _builder.ExcludePathFromPacketTransfer("/exclude2")
            .ExcludePathFromPacketTransfer("/exclude3");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.ExcludedEndpointsFromPacketTransfer.Should().BeEquivalentTo(["/exclude1", "/exclude2", "/exclude3"]);
    }

    [Fact]
    public void SetInsertUnauthorizedPackets_WithTrue_SetsProperty()
    {
        // Arrange
        _builder.StoreUnauthorizedPackets();
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.InsertUnauthorizedPackets.Should().BeTrue();
    }

    [Fact]
    public void SetResendInProgressPacketsOnInterrupt_WithTrue_SetsProperty()
    {
        // Arrange
        _builder.EnableResendOnInterrupt();
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.ResendInProgressPacketsOnInterrupt.Should().BeTrue();
    }

    [Fact]
    public void SetStoreMode_WithValidStoreMode_SetsStoreMode()
    {
        // Arrange
        _builder.SetStoreMode(StoreMode.Persistent);
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.StoreMode.Should().Be(StoreMode.Persistent);
    }

    [Fact]
    public void Build_WhenAllMethodsChained_ConfiguresAllProperties()
    {
        // Arrange
        _builder
            .AddEndpoint("ep1", _ => { })
            .ConfigureAuthentication(_ => { })
            .ConfigureKestrel(_ => { })
            .ExcludePathFromPacketTransfer("/exclude")
            .StoreUnauthorizedPackets()
            .EnableResendOnInterrupt()
            .SetStoreMode(StoreMode.Persistent);
        
        // Act
        var config = _builder.Build();
        
        // Assert
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
