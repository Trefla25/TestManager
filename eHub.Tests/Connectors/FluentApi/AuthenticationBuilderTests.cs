using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;

namespace eHub.Tests.Connectors.FluentApi;

public class AuthenticationBuilderTests
{
    private readonly AuthenticationBuilder _builder = new();

    [Fact]
    public void UseBasic_WhenCalled_AddsBasicAuthentication()
    {
        // Arrange
        var configured = false;
        _builder.AddBasic(_ => configured = true);
        
        // Act
        var authConfig = _builder.Build();
        
        // Assert
        authConfig.Basic.Should().NotBeNull();
        configured.Should().BeTrue();
    }

    [Fact]
    public void UseJwtBearer_WithValidSettings_AddsJwtBearerAuthentication()
    {
        // Arrange
        var configured = false;
        _builder.AddJwtBearer(_ => configured = true);
        
        // Act
        var authConfig = _builder.Build();
        
        // Assert
        authConfig.JWT.Should().NotBeNull();
        configured.Should().BeTrue();
    }

    [Fact]
    public void Build_WhenAllMethodsChained_ConfiguresAllProperties()
    {
        // Arrange
        _builder
            .AddBasic(_ => { })
            .AddJwtBearer(_ => { });
        
        // Act
        var authConfig = _builder.Build();
        
        // Assert
        authConfig.Basic.Should().NotBeNull();
        authConfig.JWT.Should().NotBeNull();
    }
}
