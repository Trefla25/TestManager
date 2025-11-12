using eHub.Config;
using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;

namespace eHub.Tests.Connectors.FluentApi;

public class ApiBuilderTests
{
    private readonly ApiBuilder _builder = new();

    [Fact]
    public void AddHeader_WithSingle_AddsHeader()
    {
        // Arrange
        _builder.AddHeader("Header1", "Value1", "Value2");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.Headers.Should().HaveCount(1);
        config.Headers.Should().ContainKey("Header1");
        config.Headers["Header1"].Should().BeEquivalentTo(["Value1", "Value2"]);     
    }

    [Fact]
    public void AddHeader_WithMultipleHeaders_AddsHeaders()
    {
        // Arrange
        _builder.AddHeader("Header1", "Value1", "Value2");
        _builder.AddHeader("Header2", "Value3")
            .AddHeader("Header3", ["Value4", "Value5"]);
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.Headers.Should().HaveCount(3);
        config.Headers.Should().ContainKey("Header1");
        config.Headers.Should().ContainKey("Header2");
        config.Headers.Should().ContainKey("Header3");
        config.Headers["Header1"].Should().BeEquivalentTo(["Value1", "Value2"]);
        config.Headers["Header2"].Should().BeEquivalentTo(["Value3"]);
        config.Headers["Header3"].Should().BeEquivalentTo(["Value4", "Value5"]);
    }

    [Fact]
    public void AddHeader_WithEmptyValues_AddsHeaders()
    {
        // Arrange
        _builder.AddHeader("Header1");
        _builder.AddHeader("Header2", []);
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.Headers.Should().HaveCount(2);
        config.Headers.Should().ContainKey("Header1");
        config.Headers.Should().ContainKey("Header2");
        config.Headers["Header1"].Should().BeEmpty();
        config.Headers["Header2"].Should().BeEmpty();
    }

    [Fact]
    public void AddHeader_WhenCalledMultipleTimes_AddsHeaders()
    {
        // Arrange
        _builder.AddHeader("Header1", "Value1");
        _builder.AddHeader("Header1", "Value2")
            .AddHeader("Header1", "Value3");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.Headers.Should().HaveCount(1);
        config.Headers.Should().ContainKey("Header1");
        config.Headers["Header1"].Should().BeEquivalentTo(["Value1", "Value2", "Value3"]);
    }

    [Fact]
    public void SetBaseAddress_WithValidUrl_SetsBaseAddress()
    {
        // Arrange
        _builder.SetBaseAddress("http://localhost:5000");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.BaseAddress.Should().Be("http://localhost:5000");
    }

    [Fact]
    public void SetBaseAddress_WhenCalledMultipleTimes_OverridesBaseAddress()
    {
        // Arrange
        _builder.SetBaseAddress("http://localhost:5000");
        _builder.SetBaseAddress("http://localhost:5001")
            .SetBaseAddress("http://localhost:5002");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.BaseAddress.Should().Be("http://localhost:5002");
    }

    [Fact]
    public void SetTimeout_WithValidTimeout_SetsTimeout()
    {
        // Arrange
        _builder.SetTimeout(TimeSpan.FromSeconds(30));
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.Timeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void SetTimeout_WhenCalledMultipleTimes_OverridesTimeout()
    {
        // Arrange
        _builder.SetTimeout(TimeSpan.FromSeconds(30));
        _builder.SetTimeout(TimeSpan.FromSeconds(60));
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.Timeout.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void ConfigureRetryPolicy_WhenCalled_SetsRetryPolicy()
    {
        // Arrange
        var configured = false;
        _builder.UseRetryPolicy(_ => configured = true);
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.RetryPolicy.Should().NotBeNull();
        configured.Should().BeTrue();
    }

    [Fact]
    public void UseBasicAuthorization_WhenCalled_SetsBasicAuthorization()
    {
        // Arrange
        _builder.UseBasicAuthorization("password", "user");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.Authorization.Should().NotBeNull();
        config.Authorization.Type.Should().Be(AuthorizationType.Basic);
        config.Authorization.Basic.Should().NotBeNull();
    }

    [Fact]
    public void UseBearerTokenAuthorization_WhenCalled_SetsBearerTokenAuthorization()
    {
        // Arrange
        var configured = false;
        _builder.UseBearerAuthorization(_ => configured = true);
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.Authorization.Should().NotBeNull();
        config.Authorization.Type.Should().Be(AuthorizationType.BearerToken);
        config.Authorization.BearerToken.Should().NotBeNull();
        configured.Should().BeTrue();
    }

    [Fact]
    public void UseOAuth2Authorization_WhenCalled_SetsOAuth2Authorization()
    {
        // Arrange
        var configured = false;
        _builder.UseOAuth2Authorization(_ => configured = true);
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.Authorization.Should().NotBeNull();
        config.Authorization.Type.Should().Be(AuthorizationType.OAuth2);
        config.Authorization.OAuth2.Should().NotBeNull();
        configured.Should().BeTrue();
    }

    [Fact]
    public void Build_WithMultipleAuthorizations_KeepsLastAuthorization()
    {
        // Arrange
        _builder
            .UseBasicAuthorization("password", "user")
            .UseOAuth2Authorization(_ => { })
            .UseBearerAuthorization(_ => { });
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.Authorization.Should().NotBeNull();
        config.Authorization.Type.Should().Be(AuthorizationType.BearerToken);
        config.Authorization.BearerToken.Should().NotBeNull();
    }

    [Fact]
    public void Build_WhenAllMethodsChained_ConfiguresAllProperties()
    {
        // Arrange
        _builder
            .AddHeader("Header1", "Value1")
            .AddHeader("Header2", "Value2", "Value3")
            .SetBaseAddress("https://api.example.com")
            .SetTimeout(TimeSpan.FromSeconds(45))
            .UseRetryPolicy(rp => rp
                .EnableRetryOnStatusCodes("500")
                .EnableRetryOnException()
                .SetRetryCount(3)
                .SetRetryDelay(TimeSpan.FromSeconds(2)))
            .UseBasicAuthorization("user", "pass")
            .UseBearerAuthorization(bt => bt
                .SetAccessTokenUrl("https://token.example.com")
                .UseQuery())
            .UseOAuth2Authorization(oauth => oauth
                .SetAccessTokenUrl("https://oauth.example.com")
                .SetClientId("client")
                .SetClientSecret("secret")
                .UseClientCredentials());
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();

        // Verify Headers
        config.Headers.Should().ContainKey("Header1").And.ContainKey("Header2");
        config.Headers["Header1"].Should().BeEquivalentTo(["Value1"]);
        config.Headers["Header2"].Should().BeEquivalentTo(["Value2", "Value3"]);

        // Verify BaseAddress and Timeout
        config.BaseAddress.Should().Be("https://api.example.com");
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
        config.Authorization.OAuth2.AccessTokenUrl.Should().Be("https://oauth.example.com");
        config.Authorization.OAuth2.ClientId.Should().Be("client");
        config.Authorization.OAuth2.ClientSecret.Should().Be("secret");
        config.Authorization.OAuth2.GrantType.Should().Be(GrantTypes.ClientCredentials);
    }
}
