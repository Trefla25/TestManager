using eHub.Authentication.Service;
using eHub.Authentication;
using eHub.Config;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace eHub.Tests.Authentication;

public class AuthenticationExtensionsTests
{
    [Fact]
    public void AddEHubAuthentication_WhenDisabled_DoesNothing()
    {
        // Arrange
        var authConfig = new AuthenticationConfig { Enabled = false };
        var services = new ServiceCollection();

        // Act
        services.AddEHubAuthentication(authConfig);
        
        // Assert
        services.Count.Should().Be(0);
    }

    [Fact]
    public async Task AddEHubAuthentication_WithBasic_RegistersUserServiceAndBasicScheme()
    {
        // Arrange
        var authConfig = new AuthenticationConfig
        {
            Enabled = true,
            Basic = new BasicAuthenticationConfig
            {
                DummyUsers =
                [
                    new DummyUser { Username = "Alice", Password = "password123" }
                ]
            }
        };
        
        var services = new ServiceCollection();

        // Act
        services.AddEHubAuthentication(authConfig);
        
        // Assert
        var provider = services.BuildServiceProvider();

        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var schemes = await schemeProvider.GetAllSchemesAsync();
        
        schemes.Should().Contain(s => s.Name == BasicAuthenticationHandler.SchemeName);
        provider.GetService<IUserService>().Should().NotBeNull();

        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var defaultPolicy = await policyProvider.GetDefaultPolicyAsync();
        defaultPolicy.AuthenticationSchemes.Should().Contain(BasicAuthenticationHandler.SchemeName);
    }

    [Fact]
    public async Task AddEHubAuthentication_WithJWT_RegistersJwtBearerScheme()
    {
        // Arrange
        var authConfig = new AuthenticationConfig
        {
            Enabled = true,
            JWT = new JWTAuthenticationConfig
            {
                Authority = "https://example.com",
                Audience = "my-audience"
            }
        };
        
        var services = new ServiceCollection();

        // Act
        services.AddEHubAuthentication(authConfig);

        // Assert
        var provider = services.BuildServiceProvider();

        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var schemes = await schemeProvider.GetAllSchemesAsync();

        schemes.Should().Contain(s => s.Name == JwtBearerDefaults.AuthenticationScheme);

        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var defaultPolicy = await policyProvider.GetDefaultPolicyAsync();
        defaultPolicy.AuthenticationSchemes.Should().Contain(JwtBearerDefaults.AuthenticationScheme);
    }

    [Fact]
    public async Task AddEHubAuthentication_WithSignatureParameter_RegistersSignatureScheme()
    {
        // Arrange
        var authConfig = new AuthenticationConfig
        {
            Enabled = true,
            Basic = null,
            JWT = null
        };
        
        var services = new ServiceCollection();

        var signatureAuthScheme = new AuthenticationSchemeConfig
        {
            SchemeName = SignatureAuthenticationHandler.SchemeName,
        };

        signatureAuthScheme.SetHandler<SignatureAuthenticationHandler, AuthenticationSchemeOptions>();
        authConfig.Schemes.Add(signatureAuthScheme);
        
        // Act
        services.AddEHubAuthentication(authConfig);
        
        // Assert
        var provider = services.BuildServiceProvider();

        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var schemes = await schemeProvider.GetAllSchemesAsync();

        schemes.Should().Contain(s => s.Name == SignatureAuthenticationHandler.SchemeName);
    }

    [Fact]
    public async Task AddEHubAuthentication_WithBasicAndJWTAndSignature_RegistersAllExpectedSchemes()
    {
        // Arrange
        var authConfig = new AuthenticationConfig
        {
            Enabled = true,
            Basic = new BasicAuthenticationConfig
            {
                DummyUsers =
                [
                    new DummyUser { Username = "Alice", Password = "password123" }
                ]
            },
            JWT = new JWTAuthenticationConfig
            {
                Authority = "https://example.com",
                Audience = "my-audience"
            }
        };

        var services = new ServiceCollection();

        var signatureAuthScheme = new AuthenticationSchemeConfig
        {
            SchemeName = SignatureAuthenticationHandler.SchemeName,
        };

        signatureAuthScheme.SetHandler<SignatureAuthenticationHandler, AuthenticationSchemeOptions>();
        authConfig.Schemes.Add(signatureAuthScheme);
        
        // Act
        services.AddEHubAuthentication(authConfig);
        
        // Assert
        var provider = services.BuildServiceProvider();
        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var schemes = await schemeProvider.GetAllSchemesAsync();
        var schemeArray = schemes.ToArray();

        schemeArray.Should().Contain(s => s.Name == BasicAuthenticationHandler.SchemeName);
        schemeArray.Should().Contain(s => s.Name == JwtBearerDefaults.AuthenticationScheme);
        schemeArray.Should().Contain(s => s.Name == SignatureAuthenticationHandler.SchemeName);

        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var defaultPolicy = await policyProvider.GetDefaultPolicyAsync();
        defaultPolicy.AuthenticationSchemes.Should().Contain([
            BasicAuthenticationHandler.SchemeName,
            JwtBearerDefaults.AuthenticationScheme
        ]);
    }

    [Fact]
    public async Task AddEHubAuthentication_NoConfigAndNoExtraSchemes_DefaultPolicyIsEmpty()
    {
        // Arrange
        var authConfig = new AuthenticationConfig
        {
            Enabled = true,
            Basic = null,
            JWT = null
        };

        var services = new ServiceCollection();
        
        // Act
        services.AddEHubAuthentication(authConfig);
        
        // Assert
        var provider = services.BuildServiceProvider();
        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var schemes = await schemeProvider.GetAllSchemesAsync();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var defaultPolicy = await policyProvider.GetDefaultPolicyAsync();
        
        schemes.Should().BeEmpty();
        defaultPolicy.AuthenticationSchemes.Should().BeEmpty();
    }

    [Fact]
    public void AddEHubAuthentication_MultipleCalls_ShouldThrowException()
    {
        // Arrange
        var authConfig = new AuthenticationConfig
        {
            Enabled = true,
            Basic = new BasicAuthenticationConfig
            {
                DummyUsers = [new DummyUser { Username = "Alice", Password = "password123" }]
            },
            JWT = new JWTAuthenticationConfig
            {
                Authority = "https://example.com",
                Audience = "test"
            }
        };
        var services = new ServiceCollection();

        // Act
        services.AddEHubAuthentication(authConfig);
        services.AddEHubAuthentication(authConfig);
        var act = () =>
        {
            var provider = services.BuildServiceProvider();
            provider.GetRequiredService<IAuthenticationSchemeProvider>();
        };
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*BasicAuthentication*");
    }
}

