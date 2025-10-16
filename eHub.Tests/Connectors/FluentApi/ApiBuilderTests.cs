using eHub.Config;
using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;

namespace eHub.Tests.Connectors.FluentApi;

[TestClass]
public class ApiBuilderTests
{
    private readonly ApiBuilder _builder = new();

    [TestMethod]
    public void AddHeader_WithSingle_AddsHeader()
    {
        _builder.AddHeader("Header1", "Value1", "Value2");

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.Headers.Should().HaveCount(1);
        config.Headers.Should().ContainKey("Header1");
        config.Headers["Header1"].Should().BeEquivalentTo(["Value1", "Value2"]);     
    }

    [TestMethod]
    public void AddHeader_WithMultipleHeaders_AddsHeaders()
    {
        _builder.AddHeader("Header1", "Value1", "Value2");
        _builder.AddHeader("Header2", "Value3")
            .AddHeader("Header3", ["Value4", "Value5"]);

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.Headers.Should().HaveCount(3);
        config.Headers.Should().ContainKey("Header1");
        config.Headers.Should().ContainKey("Header2");
        config.Headers.Should().ContainKey("Header3");
        config.Headers["Header1"].Should().BeEquivalentTo(["Value1", "Value2"]);
        config.Headers["Header2"].Should().BeEquivalentTo(["Value3"]);
        config.Headers["Header3"].Should().BeEquivalentTo(["Value4", "Value5"]);
    }

    [TestMethod]
    public void AddHeader_WithEmptyValues_AddsHeaders()
    {
        _builder.AddHeader("Header1");
        _builder.AddHeader("Header2", []);

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.Headers.Should().HaveCount(2);
        config.Headers.Should().ContainKey("Header1");
        config.Headers.Should().ContainKey("Header2");
        config.Headers["Header1"].Should().BeEmpty();
        config.Headers["Header2"].Should().BeEmpty();
    }

    [TestMethod]
    public void AddHeader_WhenCalledMultipleTimes_AddsHeaders()
    {
        _builder.AddHeader("Header1", "Value1");
        _builder.AddHeader("Header1", "Value2")
            .AddHeader("Header1", "Value3");

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.Headers.Should().HaveCount(1);
        config.Headers.Should().ContainKey("Header1");
        config.Headers["Header1"].Should().BeEquivalentTo(["Value1", "Value2", "Value3"]);
    }

    [TestMethod]
    public void SetBaseAddress_WithValidUrl_SetsBaseAddress()
    {
        _builder.SetBaseAddress("http://localhost:5000");

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.BaseAddress.Should().Be("http://localhost:5000");
    }

    [TestMethod]
    public void SetBaseAddress_WhenCalledMultipleTimes_OverridesBaseAddress()
    {
        _builder.SetBaseAddress("http://localhost:5000");
        _builder.SetBaseAddress("http://localhost:5001")
            .SetBaseAddress("http://localhost:5002");

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.BaseAddress.Should().Be("http://localhost:5002");
    }

    [TestMethod]
    public void SetTimeout_WithValidTimeout_SetsTimeout()
    {
        _builder.SetTimeout(TimeSpan.FromSeconds(30));

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.Timeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [TestMethod]
    public void SetTimeout_WhenCalledMultipleTimes_OverridesTimeout()
    {
        _builder.SetTimeout(TimeSpan.FromSeconds(30));
        _builder.SetTimeout(TimeSpan.FromSeconds(60));
        var config = _builder.Build();
        config.Should().NotBeNull();
        config.Timeout.Should().Be(TimeSpan.FromSeconds(60));
    }

    [TestMethod]
    public void ConfigureRetryPolicy_WhenCalled_SetsRetryPolicy()
    {
        var configured = false;
        _builder.UseRetryPolicy(builder => configured = true);

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.RetryPolicy.Should().NotBeNull();
        configured.Should().BeTrue();
    }

    [TestMethod]
    public void UseBasicAuthorization_WhenCalled_SetsBasicAuthorization()
    {
        _builder.UseBasicAuthorization("password", "user");

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.Authorization.Should().NotBeNull();
        config.Authorization.Type.Should().Be(AuthorizationType.Basic);
        config.Authorization.Basic.Should().NotBeNull();
    }

    [TestMethod]
    public void UseBearerTokenAuthorization_WhenCalled_SetsBearerTokenAuthorization()
    {
        var configured = false;
        _builder.UseBearerAuthorization(builder => configured = true);

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.Authorization.Should().NotBeNull();
        config.Authorization.Type.Should().Be(AuthorizationType.BearerToken);
        config.Authorization.BearerToken.Should().NotBeNull();
        configured.Should().BeTrue();
    }

    [TestMethod]
    public void UseOAuth2Authorization_WhenCalled_SetsOAuth2Authorization()
    {
        var configured = false;
        _builder.UseOAuth2Authorization(builder => configured = true);

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.Authorization.Should().NotBeNull();
        config.Authorization.Type.Should().Be(AuthorizationType.OAuth2);
        config.Authorization.OAuth2.Should().NotBeNull();
        configured.Should().BeTrue();
    }

    [TestMethod]
    public void Build_WithMulipleAuthorizations_KeepsLastAuthorization()
    {
        _builder
            .UseBasicAuthorization("password", "user")
            .UseOAuth2Authorization(builder => { })
            .UseBearerAuthorization(builder => { });

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.Authorization.Should().NotBeNull();
        config.Authorization.Type.Should().Be(AuthorizationType.BearerToken);
        config.Authorization.BearerToken.Should().NotBeNull();
    }

    [TestMethod]
    public void Build_WhenAllMethodsChained_ConfiguresAllProperties()
    {
        _builder
            .AddHeader("Header1", "Value1")
            .AddHeader("Header2", "Value2", "Value3")
            .SetBaseAddress("http://api.example.com")
            .SetTimeout(TimeSpan.FromSeconds(45))
            .UseRetryPolicy(rp => rp
                .EnableRetryOnStatusCodes("500")
                .EnableRetryOnException()
                .SetRetryCount(3)
                .SetRetryDelay(TimeSpan.FromSeconds(2)))
            .UseBasicAuthorization("user", "pass")
            .UseBearerAuthorization(bt => bt
                .SetAccessTokenUrl("http://token.example.com")
                .UseQuery())
            .UseOAuth2Authorization(oauth => oauth
                .SetAccessTokenUrl("http://oauth.example.com")
                .SetClientId("client")
                .SetClientSecret("secret")
                .UseClientCredentials());

        var config = _builder.Build();

        config.Should().NotBeNull();

        // Verify Headers
        config.Headers.Should().ContainKey("Header1").And.ContainKey("Header2");
        config.Headers["Header1"].Should().BeEquivalentTo(["Value1"]);
        config.Headers["Header2"].Should().BeEquivalentTo(["Value2", "Value3"]);

        // Verify BaseAddress and Timeout
        config.BaseAddress.Should().Be("http://api.example.com");
        config.Timeout.Should().Be(TimeSpan.FromSeconds(45));

        // Verify RetryPolicy configuration
        config.RetryPolicy.Should().NotBeNull();
        config.RetryPolicy.RetryOnException.Should().BeTrue();
        config.RetryPolicy.RetryCount.Should().Be(3);
        config.RetryPolicy.RetryDelay.Should().Be(TimeSpan.FromSeconds(2));
        config.RetryPolicy.ResponseStatuses.Should().Contain("500");

        // Verify Authorization: since OAuth2 was called last, it should win.
        config.Authorization.Should().NotBeNull();
        config.Authorization.Type.Should().Be(AuthorizationType.OAuth2);
        config.Authorization.OAuth2.Should().NotBeNull();
        config.Authorization.OAuth2.AccessTokenUrl.Should().Be("http://oauth.example.com");
        config.Authorization.OAuth2.ClientId.Should().Be("client");
        config.Authorization.OAuth2.ClientSecret.Should().Be("secret");
        config.Authorization.OAuth2.GrantType.Should().Be(GrantTypes.ClientCredentials);
    }
}
