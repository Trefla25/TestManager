using eHub.Config;
using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;

namespace eHub.Tests.Connectors.FluentApi;

public class OAuth2ConfigBuilderTests
{
    private readonly OAuth2ConfigBuilder _builder = new();

    [Fact]
    public void SetAccessTokenUrl_WithValidUrl_SetsAccessTokenUrl()
    {
        // Arrange
        _builder.SetAccessTokenUrl("https://oauth2.example.com");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.AccessTokenUrl.Should().Be("https://oauth2.example.com");
    }

    [Fact]
    public void SetClientId_WithValidClientId_SetsClientId()
    {
        // Arrange
        _builder.SetClientId("client123");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.ClientId.Should().Be("client123");
    }

    [Fact]
    public void SetClientSecret_WithValidSecret_SetsClientSecret()
    {
        // Arrange
        _builder.SetClientSecret("secret");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.ClientSecret.Should().Be("secret");

    }

    [Fact]
    public void SetPassword_WithValidPassword_SetsPassword()
    {
        // Arrange
        _builder.SetPassword("myPassword");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Password.Should().Be("myPassword");
    }

    [Fact]
    public void SetScope_WithValidScope_SetsScope()
    {
        // Arrange
        _builder.SetScope("read");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Scope.Should().Be("read");
    }

    [Fact]
    public void SetUsername_WithValidUsername_SetsUsername()
    {
        // Arrange
        _builder.SetUsername("user");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Username.Should().Be("user");
    }

    [Fact]
    public void SetCode_WithValidCode_SetsCode()
    {
        // Arrange
        _builder.SetCode("code123");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Code.Should().Be("code123");
    }

    [Fact]
    public void SetRedirectUri_WithValidUri_SetsRedirectUri()
    {
        // Arrange
        _builder.SetRedirectUri("https://redirect.example.com");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.RedirectUri.Should().Be("https://redirect.example.com");
    }

    [Fact]
    public void UseAuthorizationCode_WhenCalled_SetsGrantTypeToAuthorizationCode()
    {
        // Arrange
        _builder.UseAuthorizationCode();
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.GrantType.Should().Be(GrantTypes.AuthorizationCode);
    }

    [Fact]
    public void UseClientCredentials_WhenCalled_SetsGrantTypeToClientCredentials()
    {
        // Arrange
        _builder.UseClientCredentials();
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.GrantType.Should().Be(GrantTypes.ClientCredentials);
    }

    [Fact]
    public void UsePassword_WhenCalled_SetsGrantTypeToPassword()
    {
        // Arrange
        _builder.UsePassword();
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.GrantType.Should().Be(GrantTypes.Password);
    }

    [Fact]
    public void Build_WithMultipleGrantTypes_KeepsLastType()
    {
        // Arrange
        _builder
            .UsePassword()
            .UseClientCredentials()
            .UseAuthorizationCode();
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.GrantType.Should().Be(GrantTypes.AuthorizationCode);
    }

    [Fact]
    public void Build_WhenAllMethodsChained_ConfiguresAllProperties()
    {
        // Arrange
        _builder
            .SetAccessTokenUrl("https://oauth2.example.com")
            .SetClientId("client123")
            .SetClientSecret("secret")
            .SetPassword("myPassword")
            .SetScope("read")
            .SetUsername("user")
            .SetCode("code123")
            .SetRedirectUri("https://redirect.example.com")
            .UseClientCredentials();
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.AccessTokenUrl.Should().Be("https://oauth2.example.com");
        config.ClientId.Should().Be("client123");
        config.ClientSecret.Should().Be("secret");
        config.Password.Should().Be("myPassword");
        config.Scope.Should().Be("read");
        config.Username.Should().Be("user");
        config.Code.Should().Be("code123");
        config.RedirectUri.Should().Be("https://redirect.example.com");
        config.GrantType.Should().Be(GrantTypes.ClientCredentials);
    }
}
