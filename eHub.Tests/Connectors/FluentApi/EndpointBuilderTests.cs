using eHub.PlugIn;
using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;

namespace eHub.Tests.Connectors.FluentApi;

public class EndpointBuilderTests
{
    private readonly EndpointBuilder _builder = new();

    [Fact]
    public void AddAcceptedContentType_WithValidContentType_AddsContentType()
    {
        // Arrange
        _builder.AddAcceptedContentType("application/json");
        
        // Act
        var endpointConfig = _builder.Build();
        
        // Assert
        endpointConfig.Should().NotBeNull();
        endpointConfig.ContentTypes.Should().Contain("application/json");
    }

    [Fact]
    public void RequireAuthorization_WithTrue_SetsAuthorize()
    {
        // Arrange
        _builder.RequireAuthorization(true);
        
        // Act
        var endpointConfig = _builder.Build();
        
        // Assert
        endpointConfig.Should().NotBeNull();
        endpointConfig.Authorize.Should().BeTrue();
    }

    [Fact]
    public void SetHttpMethod_WithValidMethod_SetsHttpMethod()
    {
        // Arrange
        _builder.SetHttpMethod("GET");
        
        // Act
        var endpointConfig = _builder.Build();
        
        // Assert
        endpointConfig.Should().NotBeNull();
        endpointConfig.HttpMethod.Should().Be("GET");
    }

    [Fact]
    public void SetPath_WithValidPath_SetsPath()
    {
        // Arrange
        _builder.SetPath("/api/data");
        
        // Act
        var endpointConfig = _builder.Build();
        
        // Assert
        endpointConfig.Should().NotBeNull();
        endpointConfig.Path.Should().Be("/api/data");
    }

    [Fact]
    public void SetRequestSizeLimit_WithValidLimit_SetsRequestSizeLimit()
    {
        // Arrange
        _builder.SetRequestSizeLimit(1024);
        
        // Act
        var endpointConfig = _builder.Build();
        
        // Assert
        endpointConfig.Should().NotBeNull();
        endpointConfig.RequestSizeLimit.Should().Be(1024);
    }

    [Fact]
    public void SetResendPacketsOnCommunicationError_WithTrue_SetsFlag()
    {
        // Arrange
        _builder.EnableResendOnCommunicationError();
        
        // Act
        var endpointConfig = _builder.Build();
        
        // Assert
        endpointConfig.Should().NotBeNull();

        endpointConfig.ResendPacketsOnCommunicationError.Should().BeTrue();
    }

    [Fact]
    public void SetStoreMode_WithValidStoreMode_SetsStoreMode()
    {
        // Arrange
        _builder.SetStoreMode(StoreMode.Persistent);
        
        // Act
        var endpointConfig = _builder.Build();
        
        // Assert
        endpointConfig.Should().NotBeNull();
        endpointConfig.StoreMode.Should().Be(StoreMode.Persistent);
    }

    [Fact]
    public void SetTopic_WithValidTopic_SetsTopic()
    {
        // Arrange
        _builder.SetTopic("topic1");
        
        // Act
        var endpointConfig = _builder.Build();
        
        // Assert
        endpointConfig.Should().NotBeNull();
        endpointConfig.Topic.Should().Be("topic1");
    }

    [Fact]
    public void UseApi_WithValidApiName_SetsApiName()
    {
        // Arrange
        _builder.UseApi("MyApi");
        
        // Act
        var endpointConfig = _builder.Build();
        
        // Assert
        endpointConfig.Should().NotBeNull();
        endpointConfig.Api.Should().Be("MyApi");
    }

    [Fact]
    public void Build_WhenAllMethodsChained_ConfiguresAllProperties()
    {
        // Arrange
        _builder
            .AddAcceptedContentType("application/json")
            .RequireAuthorization(true)
            .SetHttpMethod("POST")
            .SetPath("/submit")
            .SetRequestSizeLimit(2048)
            .EnableResendOnCommunicationError()
            .SetStoreMode(StoreMode.Persistent)
            .SetTopic("topic2")
            .UseApi("MyApi");
        
        // Act
        var endpointConfig = _builder.Build();
        
        // Assert
        endpointConfig.Should().NotBeNull();
        endpointConfig.ContentTypes.Should().Contain("application/json");
        endpointConfig.Authorize.Should().BeTrue();
        endpointConfig.HttpMethod.Should().Be("POST");
        endpointConfig.Path.Should().Be("/submit");
        endpointConfig.RequestSizeLimit.Should().Be(2048);
        endpointConfig.ResendPacketsOnCommunicationError.Should().BeTrue();
        endpointConfig.StoreMode.Should().Be(StoreMode.Persistent);
        endpointConfig.Topic.Should().Be("topic2");
        endpointConfig.Api.Should().Be("MyApi");
    }
}
