using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;

namespace eHub.Tests.Connectors.FluentApi;

[TestClass]
public class AuthenticationBuilderTests
{
    private readonly AuthenticationBuilder _builder = new();

    [TestMethod]
    public void UseBasic_WhenCalled_AddsBasicAuthentication()
    {
        var configured = false;
        _builder.AddBasic(b => configured = true);

        var authConfig = _builder.Build();

        authConfig.Basic.Should().NotBeNull();
        configured.Should().BeTrue();
    }

    [TestMethod]
    public void UseJwtBearer_WithValidSettings_AddsJwtBearerAuthentication()
    {
        var configured = false;
        _builder.AddJwtBearer(jwt => configured = true);

        var authConfig = _builder.Build();

        authConfig.JWT.Should().NotBeNull();
        configured.Should().BeTrue();
    }

    [TestMethod]
    public void Build_WhenAllMethodsChained_ConfiguresAllProperties()
    {
        _builder
            .AddBasic(b => { })
            .AddJwtBearer(jwt => { });

        var authConfig = _builder.Build();

        authConfig.Basic.Should().NotBeNull();
        authConfig.JWT.Should().NotBeNull();
    }
}
