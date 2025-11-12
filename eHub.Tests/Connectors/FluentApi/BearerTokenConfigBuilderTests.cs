using eHub.Config;
using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;

namespace eHub.Tests.Connectors.FluentApi;

public class BearerTokenConfigBuilderTests
{
    private readonly BearerTokenConfigBuilder _builder = new();

    [Fact]
    public void AddParameter_WithSingleParameter_AddsParameter()
    {
        // Arrange
        _builder.AddParameter("client_id", "abc123");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.Parameters.Should().ContainKey("client_id");
        config.Parameters["client_id"].Should().Be("abc123");
    }

    [Fact]
    public void AddParameter_WithMultipleParameters_AddsParameters()
    {
        // Arrange
        _builder.AddParameter("client_id", "abc123");
        _builder.AddParameter("client_secret", "456cef");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.Parameters.Should().ContainKey("client_id");
        config.Parameters.Should().ContainKey("client_secret");
        config.Parameters["client_id"].Should().Be("abc123");
        config.Parameters["client_secret"].Should().Be("456cef");
    }

    [Fact]
    public void AddParameter_WhenCalledMultipleTimes_ThrowsException()
    {
        // Arrange
        _builder.AddParameter("client_id", "abc123");
        
        // Act
        Action act = () => _builder.AddParameter("client_id", "456cef");
        
        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetAccessTokenUrl_WithValidUrl_SetsAccessTokenUrl()
    {
        // Arrange
        _builder.SetAccessTokenUrl("https://token.example.com");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.AccessTokenUrl.Should().Be("https://token.example.com");
    }

    [Fact]
    public void SetTokenExpirationTime_WithValidTime_SetsTokenExpirationTime()
    {
        // Arrange
        _builder.SetTokenExpirationTime(TimeSpan.FromMinutes(60));
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.TokenExpirationTime.Should().Be(TimeSpan.FromMinutes(60));
    }

    [Fact]
    public void UseQuery_WhenCalled_SetsContentTypeToQuery()
    {
        // Arrange
        _builder.UseQuery();
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.ContentType.Should().Be(ParameterContentType.Query);
    }

    [Fact]
    public void UseFormUrlEncoded_WhenCalled_SetsContentTypeToFormUrlEncoded()
    {
        // Arrange
        _builder.UseFormUrlEncoded();
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.ContentType.Should().Be(ParameterContentType.FormUrlEncoded);
    }

    [Fact]
    public void UseHeaders_WhenCalled_SetsContentTypeToHeaders()
    {
        // Arrange
        _builder.UseHeaders();
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.ContentType.Should().Be(ParameterContentType.Headers);
    }

    [Fact]
    public void Build_WithMultipleParameterContentTypes_KeepsLastType()
    {
        // Arrange
        _builder
            .UseFormUrlEncoded()
            .UseQuery()
            .UseHeaders();
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.ContentType.Should().Be(ParameterContentType.Headers);
    }

    [Fact]
    public void Build_WhenAllMethodsChained_ConfiguresAllProperties()
    {
        // Arrange
        _builder
            .AddParameter("client_id", "abc123")
            .AddParameter("client_secret", "456cef")
            .SetAccessTokenUrl("https://token.example.com")
            .SetTokenExpirationTime(TimeSpan.FromMinutes(30))
            .UseFormUrlEncoded();
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Parameters.Should().ContainKey("client_id");
        config.Parameters.Should().ContainKey("client_secret");
        config.Parameters["client_id"].Should().Be("abc123");
        config.Parameters["client_secret"].Should().Be("456cef");
        config.AccessTokenUrl.Should().Be("https://token.example.com");
        config.TokenExpirationTime.Should().Be(TimeSpan.FromMinutes(30));
        config.ContentType.Should().Be(ParameterContentType.FormUrlEncoded);
    }
}
