using eHub.PlugIn;
using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;

namespace eHub.Tests.Connectors.FluentApi;

public class HttpOutgoingBuilderTests
{
    private readonly HttpOutgoingBuilder _builder = new();

    [Fact]
    public void AddApi_WhenCalled_AddsApiConfiguration()
    {
        // Arrange
        var configured = false;
        _builder.AddApi("api1", _ => configured = true);
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.Api.Should().ContainKey("api1");
        configured.Should().BeTrue();
    }

    [Fact]
    public void AddApi_WithMultipleApis_AddsAllApiConfiguration()
    {
        // Arrange
        _builder.AddApi("api1", _ => { });
        _builder.AddApi("api2", _ => { });
        _builder.AddApi("api3", _ => { });
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.Api.Should().ContainKey("api1");
        config.Api.Should().ContainKey("api2");
        config.Api.Should().ContainKey("api3");
    }

    [Fact]
    public void AddApi_WhenCalledMultipleTimes_ThrowsException()
    {
        // Arrange
        _builder.AddApi("api1", _ => { });
        
        // Act
        var act = () => _builder.AddApi("api1", _ => { });
        
        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddEndpoint_WithBuilder_AddsEndpoint()
    {
        // Arrange
        var configured = false;
        _builder.AddEndpoint("ep1", _ => configured = true);
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.Endpoints.Should().ContainKey("ep1");
        configured.Should().BeTrue();
    }

    [Fact]
    public void AddEndpoint_WithConfig_AddsEndpoint()
    {
        // Arrange
        var dummyConfig = new HttpConnectorEndpointConfig { Path = "/dummyOut" };
        _builder.AddEndpoint("ep2", dummyConfig);
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.Endpoints.Should().ContainKey("ep2");
        config.Endpoints["ep2"].Should().BeEquivalentTo(dummyConfig);
    }

    [Fact]
    public void ExcludeEndpointFromPacketTransfer_WithSingleTopic_ExcludesEndpoint()
    {
        // Arrange
        _builder.ExcludeTopicFromPacketTransfer("topic1");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.ExcludedEndpointsFromPacketTransfer.Should().Contain("topic1");
    }

    [Fact]
    public void ExcludeEndpointFromPacketTransfer_WithMultipleTopics_ExcludesEndpoints()
    {
        // Arrange
        _builder.ExcludeTopicFromPacketTransfer("topic1");
        _builder.ExcludeTopicFromPacketTransfer("topic2")
            .ExcludeTopicFromPacketTransfer("topic3");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.ExcludedEndpointsFromPacketTransfer.Should().BeEquivalentTo(["topic1", "topic2", "topic3"]);
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
    public void SetResendPacketsOnCommunicationError_WithTrue_SetsProperty()
    {
        // Arrange
        _builder.EnableResendOnCommunicationError();
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.ResendPacketsOnCommunicationError.Should().BeTrue();
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
            .AddApi("api1", _ => { })
            .AddEndpoint("ep1", _ => { })
            .ExcludeTopicFromPacketTransfer("topic1")
            .EnableResendOnInterrupt()
            .EnableResendOnCommunicationError()
            .SetStoreMode(StoreMode.Persistent);
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.Api.Should().ContainKey("api1");
        config.Endpoints.Should().ContainKey("ep1");
        config.ExcludedEndpointsFromPacketTransfer.Should().Contain("topic1");
        config.ResendInProgressPacketsOnInterrupt.Should().BeTrue();
        config.ResendPacketsOnCommunicationError.Should().BeTrue();
        config.StoreMode.Should().Be(StoreMode.Persistent);
    }
}
