using eHub.Config;
using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;

namespace eHub.Tests.Connectors.FluentApi;

[TestClass]
public class BearerTokenConfigBuilderTests
{
    private readonly BearerTokenConfigBuilder _builder = new();

    [TestMethod]
    public void AddParameter_WithSingleParameter_AddsParameter()
    {
        _builder.AddParameter("client_id", "abc123");

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.Parameters.Should().ContainKey("client_id");
        config.Parameters["client_id"].Should().Be("abc123");
    }

    [TestMethod]
    public void AddParameter_WithMultipleParameters_AddsParameters()
    {
        _builder.AddParameter("client_id", "abc123");
        _builder.AddParameter("client_secret", "456cef");

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.Parameters.Should().ContainKey("client_id");
        config.Parameters.Should().ContainKey("client_secret");
        config.Parameters["client_id"].Should().Be("abc123");
        config.Parameters["client_secret"].Should().Be("456cef");
    }

    [TestMethod]
    public void AddParameter_WhenCalledMultipleTimes_ThrowsException()
    {
        _builder.AddParameter("client_id", "abc123");

        Action act = () => _builder.AddParameter("client_id", "456cef");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void SetAccessTokenUrl_WithValidUrl_SetsAccessTokenUrl()
    {
        _builder.SetAccessTokenUrl("http://token.example.com");

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.AccessTokenUrl.Should().Be("http://token.example.com");
    }

    [TestMethod]
    public void SetTokenExpirationTime_WithValidTime_SetsTokenExpirationTime()
    {
        _builder.SetTokenExpirationTime(TimeSpan.FromMinutes(60));

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.TokenExpirationTime.Should().Be(TimeSpan.FromMinutes(60));
    }

    [TestMethod]
    public void UseQuery_WhenCalled_SetsContentTypeToQuery()
    {
        _builder.UseQuery();

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.ContentType.Should().Be(ParameterContentType.Query);
    }

    [TestMethod]
    public void UseFormUrlEncoded_WhenCalled_SetsContentTypeToFormUrlEncoded()
    {
        _builder.UseFormUrlEncoded();

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.ContentType.Should().Be(ParameterContentType.FormUrlEncoded);
    }

    [TestMethod]
    public void UseHeaders_WhenCalled_SetsContentTypeToHeaders()
    {
        _builder.UseHeaders();

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.ContentType.Should().Be(ParameterContentType.Headers);
    }

    [TestMethod]
    public void Build_WithMulipleParameterContentTypes_KeepsLastType()
    {
        _builder
            .UseFormUrlEncoded()
            .UseQuery()
            .UseHeaders();

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.ContentType.Should().Be(ParameterContentType.Headers);
    }

    [TestMethod]
    public void Build_WhenAllMethodsChained_ConfiguresAllProperties()
    {
        _builder
            .AddParameter("client_id", "abc123")
            .AddParameter("client_secret", "456cef")
            .SetAccessTokenUrl("http://token.example.com")
            .SetTokenExpirationTime(TimeSpan.FromMinutes(30))
            .UseFormUrlEncoded();

        var config = _builder.Build();
        config.Parameters.Should().ContainKey("client_id");
        config.Parameters.Should().ContainKey("client_secret");
        config.Parameters["client_id"].Should().Be("abc123");
        config.Parameters["client_secret"].Should().Be("456cef");
        config.AccessTokenUrl.Should().Be("http://token.example.com");
        config.TokenExpirationTime.Should().Be(TimeSpan.FromMinutes(30));
        config.ContentType.Should().Be(ParameterContentType.FormUrlEncoded);
    }
}
