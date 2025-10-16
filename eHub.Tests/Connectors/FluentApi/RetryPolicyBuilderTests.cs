using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;

namespace eHub.Tests.Connectors.FluentApi;

[TestClass]
public class RetryPolicyBuilderTests
{
    private readonly RetryPolicyBuilder _builder = new();

    [TestMethod]
    public void AddRetryResponseStatus_WithSingleStatus_AddsResponseStatus()
    {
        _builder.EnableRetryOnStatusCodes("500", "501");

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.ResponseStatuses.Should().HaveCount(2);
        config.ResponseStatuses.Should().Contain("500", "501");
    }

    [TestMethod]
    public void AddRetryResponseStatus_WithMultipleStatuses_AddsResponseStatuses()
    {
        _builder.EnableRetryOnStatusCodes("500", "501");
        _builder.EnableRetryOnStatusCodes("41X");
        _builder.EnableRetryOnStatusCodes("490")
            .EnableRetryOnStatusCodes("402")
            .EnableRetryOnStatusCodes("XX9");

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.ResponseStatuses.Should().HaveCount(6);
        config.ResponseStatuses.Should().BeEquivalentTo(["500" , "501", "41X", "490", "402", "XX9"]);
    }

    [TestMethod]
    public void RetryOnException_WithTrue_SetsRetryOnException()
    {
        _builder.EnableRetryOnException();

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.RetryOnException.Should().BeTrue();
    }

    [TestMethod]
    public void SetRetryCount_WithValidCount_SetsRetryCount()
    {
        _builder.SetRetryCount(5);

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.RetryCount.Should().Be(5);
    }

    [TestMethod]
    public void SetRetryDelay_WithValidDelay_SetsRetryDelay()
    {
        _builder.SetRetryDelay(TimeSpan.FromSeconds(5));

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.RetryDelay.Should().Be(TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public void Build_WhenAllMethodsChained_ConfiguresAllProperties()
    {
        _builder
            .EnableRetryOnStatusCodes("500")
            .EnableRetryOnStatusCodes("404")
            .EnableRetryOnException()
            .SetRetryCount(3)
            .SetRetryDelay(TimeSpan.FromSeconds(2));

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.ResponseStatuses.Should().BeEquivalentTo([ "500", "404" ]);
        config.RetryOnException.Should().BeTrue();
        config.RetryCount.Should().Be(3);
        config.RetryDelay.Should().Be(TimeSpan.FromSeconds(2));
    }

}
