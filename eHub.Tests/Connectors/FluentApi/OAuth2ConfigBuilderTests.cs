using eHub.Config;
using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;

namespace eHub.Tests.Connectors.FluentApi;

[TestClass]
public class OAuth2ConfigBuilderTests
{
    private readonly OAuth2ConfigBuilder _builder = new();

    [TestMethod]
    public void SetAccessTokenUrl_WithValidUrl_SetsAccessTokenUrl()
    {
        _builder.SetAccessTokenUrl("http://oauth2.example.com");

        var config = _builder.Build();

        config.AccessTokenUrl.Should().Be("http://oauth2.example.com");
    }

    [TestMethod]
    public void SetClientId_WithValidClientId_SetsClientId()
    {
        _builder.SetClientId("client123");

        var config = _builder.Build();

        config.ClientId.Should().Be("client123");
    }

    [TestMethod]
    public void SetClientSecret_WithValidSecret_SetsClientSecret()
    {
        _builder.SetClientSecret("secret");

        var config = _builder.Build();
        config.ClientSecret.Should().Be("secret");

    }

    [TestMethod]
    public void SetPassword_WithValidPassword_SetsPassword()
    {
        _builder.SetPassword("mypassword");

        var config = _builder.Build();

        config.Password.Should().Be("mypassword");
    }

    [TestMethod]
    public void SetScope_WithValidScope_SetsScope()
    {
        _builder.SetScope("read");

        var config = _builder.Build();

        config.Scope.Should().Be("read");
    }

    [TestMethod]
    public void SetUsername_WithValidUsername_SetsUsername()
    {
        _builder.SetUsername("user");

        var config = _builder.Build();

        config.Username.Should().Be("user");
    }

    [TestMethod]
    public void SetCode_WithValidCode_SetsCode()
    {
        _builder.SetCode("code123");

        var config = _builder.Build();

        config.Code.Should().Be("code123");
    }

    [TestMethod]
    public void SetRedirectUri_WithValidUri_SetsRedirectUri()
    {
        _builder.SetRedirectUri("http://redirect.example.com");

        var config = _builder.Build();

        config.RedirectUri.Should().Be("http://redirect.example.com");
    }

    [TestMethod]
    public void UseAuthorizationCode_WhenCalled_SetsGrantTypeToAuthorizationCode()
    {
        _builder.UseAuthorizationCode();

        var config = _builder.Build();

        config.GrantType.Should().Be(GrantTypes.AuthorizationCode);
    }

    [TestMethod]
    public void UseClientCredentials_WhenCalled_SetsGrantTypeToClientCredentials()
    {
        _builder.UseClientCredentials();

        var config = _builder.Build();

        config.GrantType.Should().Be(GrantTypes.ClientCredentials);
    }

    [TestMethod]
    public void UsePassword_WhenCalled_SetsGrantTypeToPassword()
    {
        _builder.UsePassword();

        var config = _builder.Build();

        config.GrantType.Should().Be(GrantTypes.Password);
    }

    [TestMethod]
    public void Build_WithMulipleGrantTypes_KeepsLastType()
    {
        _builder
            .UsePassword()
            .UseClientCredentials()
            .UseAuthorizationCode();

        var config = _builder.Build();

        config.GrantType.Should().Be(GrantTypes.AuthorizationCode);
    }

    [TestMethod]
    public void Build_WhenAllMethodsChained_ConfiguresAllProperties()
    {
        _builder
            .SetAccessTokenUrl("http://oauth2.example.com")
            .SetClientId("client123")
            .SetClientSecret("secret")
            .SetPassword("mypassword")
            .SetScope("read")
            .SetUsername("user")
            .SetCode("code123")
            .SetRedirectUri("http://redirect.example.com")
            .UseClientCredentials();

        var config = _builder.Build();
        config.AccessTokenUrl.Should().Be("http://oauth2.example.com");
        config.ClientId.Should().Be("client123");
        config.ClientSecret.Should().Be("secret");
        config.Password.Should().Be("mypassword");
        config.Scope.Should().Be("read");
        config.Username.Should().Be("user");
        config.Code.Should().Be("code123");
        config.RedirectUri.Should().Be("http://redirect.example.com");
        config.GrantType.Should().Be(GrantTypes.ClientCredentials);
    }
}
