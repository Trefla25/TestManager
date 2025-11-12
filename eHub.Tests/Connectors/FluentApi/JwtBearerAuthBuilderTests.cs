using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;

namespace eHub.Tests.Connectors.FluentApi;

public class JwtBearerAuthBuilderTests
{
    private readonly JwtBearerAuthBuilder _builder = new();

    [Fact]
    public void SetAudience_WithValidAudience_SetsAudience()
    {
        // Arrange
        _builder.SetAudience("audienceValue");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Audience.Should().Be("audienceValue");
    }

    [Fact]
    public void SetAuthority_WithValidAuthority_SetsAuthority()
    {
        // Arrange
        _builder.SetAuthority("authorityValue");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Authority.Should().Be("authorityValue");
    }

    [Fact]
    public void Build_WhenAllMethodsChained_ConfiguresAllProperties()
    {
        // Arrange
        _builder
            .SetAudience("audienceValue")
            .SetAuthority("authorityValue");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Audience.Should().Be("audienceValue");
        config.Authority.Should().Be("authorityValue");
    }
}
