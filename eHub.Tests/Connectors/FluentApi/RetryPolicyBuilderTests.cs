using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;

namespace eHub.Tests.Connectors.FluentApi;

public class RetryPolicyBuilderTests
{
    private readonly RetryPolicyBuilder _builder = new();

    [Fact]
    public void AddRetryResponseStatus_WithSingleStatus_AddsResponseStatus()
    {
        // Arrange
        _builder.EnableRetryOnStatusCodes("500", "501");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.ResponseStatuses.Should().HaveCount(2);
        config.ResponseStatuses.Should().Contain("500", "501");
    }

    [Fact]
    public void AddRetryResponseStatus_WithMultipleStatuses_AddsResponseStatuses()
    {
        // Arrange
        _builder.EnableRetryOnStatusCodes("500", "501");
        _builder.EnableRetryOnStatusCodes("41X");
        _builder.EnableRetryOnStatusCodes("490")
            .EnableRetryOnStatusCodes("402")
            .EnableRetryOnStatusCodes("XX9");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.ResponseStatuses.Should().HaveCount(6);
        config.ResponseStatuses.Should().BeEquivalentTo(["500" , "501", "41X", "490", "402", "XX9"]);
    }

    [Fact]
    public void RetryOnException_WithTrue_SetsRetryOnException()
    {
        // Arrange
        _builder.EnableRetryOnException();
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.RetryOnException.Should().BeTrue();
    }

    [Fact]
    public void SetRetryCount_WithValidCount_SetsRetryCount()
    {
        // Arrange
        _builder.SetRetryCount(5);
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.RetryCount.Should().Be(5);
    }

    [Fact]
    public void SetRetryDelay_WithValidDelay_SetsRetryDelay()
    {
        // Arrange
        _builder.SetRetryDelay(TimeSpan.FromSeconds(5));
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.RetryDelay.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Build_WhenAllMethodsChained_ConfiguresAllProperties()
    {
        // Arrange
        _builder
            .EnableRetryOnStatusCodes("500")
            .EnableRetryOnStatusCodes("404")
            .EnableRetryOnException()
            .SetRetryCount(3)
            .SetRetryDelay(TimeSpan.FromSeconds(2));
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.ResponseStatuses.Should().BeEquivalentTo([ "500", "404" ]);
        config.RetryOnException.Should().BeTrue();
        config.RetryCount.Should().Be(3);
        config.RetryDelay.Should().Be(TimeSpan.FromSeconds(2));
    }
}
