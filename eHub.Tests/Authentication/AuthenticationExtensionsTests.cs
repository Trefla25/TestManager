using eHub.Authentication.Service;
using eHub.Authentication;
using eHub.Config;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace eHub.Tests.Authentication;

[TestClass]
public class AuthenticationExtensionsTests
{
    [TestMethod]
    public void AddEHubAuthentication_WhenDisabled_DoesNothing()
    {
        var authConfig = new AuthenticationConfig { Enabled = false };

        var services = new ServiceCollection();

        services.AddEHubAuthentication(authConfig);

        services.Count.Should().Be(0);
    }

    [TestMethod]
    public async Task AddEHubAuthentication_WithBasic_RegistersUserServiceAndBasicScheme()
    {
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

        services.AddEHubAuthentication(authConfig);
        var provider = services.BuildServiceProvider();

        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var schemes = await schemeProvider.GetAllSchemesAsync();

        schemes.Should().Contain(s => s.Name == BasicAuthenticationHandler.SchemeName);
        provider.GetService<IUserService>().Should().NotBeNull();

        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var defaultPolicy = await policyProvider.GetDefaultPolicyAsync();
        defaultPolicy.AuthenticationSchemes.Should().Contain(BasicAuthenticationHandler.SchemeName);
    }

    [TestMethod]
    public async Task AddEHubAuthentication_WithJWT_RegistersJwtBearerScheme()
    {
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

        services.AddEHubAuthentication(authConfig);
        var provider = services.BuildServiceProvider();

        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var schemes = await schemeProvider.GetAllSchemesAsync();

        schemes.Should().Contain(s => s.Name == JwtBearerDefaults.AuthenticationScheme);

        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var defaultPolicy = await policyProvider.GetDefaultPolicyAsync();
        defaultPolicy.AuthenticationSchemes.Should().Contain(JwtBearerDefaults.AuthenticationScheme);
    }

    [TestMethod]
    public async Task AddEHubAuthentication_WithSignatureParameter_RegistersSignatureScheme()
    {
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

        services.AddEHubAuthentication(authConfig);
        var provider = services.BuildServiceProvider();

        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var schemes = await schemeProvider.GetAllSchemesAsync();

        schemes.Should().Contain(s => s.Name == SignatureAuthenticationHandler.SchemeName);
    }

    [TestMethod]
    public async Task AddEHubAuthentication_WithBasicAndJWTAndSignature_RegistersAllExpectedSchemes()
    {
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

        services.AddEHubAuthentication(authConfig);
        var provider = services.BuildServiceProvider();
        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var schemes = await schemeProvider.GetAllSchemesAsync();

        schemes.Should().Contain(s => s.Name == BasicAuthenticationHandler.SchemeName);
        schemes.Should().Contain(s => s.Name == JwtBearerDefaults.AuthenticationScheme);
        schemes.Should().Contain(s => s.Name == SignatureAuthenticationHandler.SchemeName);

        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var defaultPolicy = await policyProvider.GetDefaultPolicyAsync();
        defaultPolicy.AuthenticationSchemes.Should().Contain([
            BasicAuthenticationHandler.SchemeName,
            JwtBearerDefaults.AuthenticationScheme
        ]);
    }

    [TestMethod]
    public async Task AddEHubAuthentication_NoConfigAndNoExtraSchemes_DefaultPolicyIsEmpty()
    {
        var authConfig = new AuthenticationConfig
        {
            Enabled = true,
            Basic = null,
            JWT = null
        };

        var services = new ServiceCollection();

        services.AddEHubAuthentication(authConfig);
        var provider = services.BuildServiceProvider();
        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var schemes = await schemeProvider.GetAllSchemesAsync();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var defaultPolicy = await policyProvider.GetDefaultPolicyAsync();

        schemes.Should().BeEmpty();
        defaultPolicy.AuthenticationSchemes.Should().BeEmpty();
    }

    [TestMethod]
    public void AddEHubAuthentication_MultipleCalls_ShouldThrowException()
    {
        var authConfig = new AuthenticationConfig
        {
            Enabled = true,
            Basic = new BasicAuthenticationConfig
            {
                DummyUsers = new[] { new DummyUser { Username = "Alice", Password = "password123" } }
            },
            JWT = new JWTAuthenticationConfig
            {
                Authority = "https://example.com",
                Audience = "test"
            }
        };
        var services = new ServiceCollection();

        services.AddEHubAuthentication(authConfig);
        services.AddEHubAuthentication(authConfig);
        Action act = () =>
        {
            var provider = services.BuildServiceProvider();
            provider.GetRequiredService<IAuthenticationSchemeProvider>();
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*BasicAuthentication*");
    }

    [TestMethod]
    public void AddEHubAuthentication_MultipleCall_ThrowsException()
    {
        var authConfigExample = new AuthenticationConfig
        {
            Enabled = true,
            Basic = new BasicAuthenticationConfig
            {
                DummyUsers = [new DummyUser { Username = "Alice", Password = "password123" }]
            },
            JWT = new JWTAuthenticationConfig
            {
                Authority = "https://example.com",
                Audience = "example"
            }
        };

        var authConfigTest = new AuthenticationConfig
        {
            Enabled = true,
            Basic = new BasicAuthenticationConfig
            {
                DummyUsers = [new DummyUser { Username = "Wonderland", Password = "321password" }]
            },
            JWT = new JWTAuthenticationConfig
            {
                Authority = "https://test.com",
                Audience = "test"
            }
        };

        var services = new ServiceCollection();
        services.AddEHubAuthentication(authConfigExample);
        services.AddEHubAuthentication(authConfigTest);

        Action act = () =>
        {
            var provider = services.BuildServiceProvider();
            provider.GetRequiredService<IAuthenticationSchemeProvider>();
        };

        act.Should().Throw<InvalidOperationException>().WithMessage("*BasicAuthentication*");
    }
}
