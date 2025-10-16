using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;

namespace eHub.Tests.Connectors.FluentApi;

[TestClass]
public class JwtBearerAuthBuilderTests
{
    private readonly JwtBearerAuthBuilder _builder = new();

    [TestMethod]
    public void SetAudience_WithValidAudience_SetsAudience()
    {
        _builder.SetAudience("audienceValue");

        var config = _builder.Build();

        config.Audience.Should().Be("audienceValue");
    }

    [TestMethod]
    public void SetAuthority_WithValidAuthority_SetsAuthority()
    {
        _builder.SetAuthority("authorityValue");

        var config = _builder.Build();

        config.Authority.Should().Be("authorityValue");
    }

    [TestMethod]
    public void Build_WhenAllMethodsChained_ConfiguresAllProperties()
    {
        _builder
            .SetAudience("audienceValue")
            .SetAuthority("authorityValue");

        var config = _builder.Build();

        config.Audience.Should().Be("audienceValue");
        config.Authority.Should().Be("authorityValue");
    }
}
