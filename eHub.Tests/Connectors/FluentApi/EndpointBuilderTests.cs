using eHub.PlugIn;
using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;

namespace eHub.Tests.Connectors.FluentApi;

[TestClass]
public class EndpointBuilderTests
{
    private readonly EndpointBuilder _builder = new();

    [TestMethod]
    public void AddAcceptedContentType_WithValidContentType_AddsContentType()
    {
        _builder.AddAcceptedContentType("application/json");

        var endpointConfig = _builder.Build();

        endpointConfig.Should().NotBeNull();
        endpointConfig.ContentTypes.Should().Contain("application/json");
    }

    [TestMethod]
    public void RequireAuthorization_WithTrue_SetsAuthorize()
    {
        _builder.RequireAuthorization(true);

        var endpointConfig = _builder.Build();

        endpointConfig.Should().NotBeNull();
        endpointConfig.Authorize.Should().BeTrue();
    }

    [TestMethod]
    public void SetHttpMethod_WithValidMethod_SetsHttpMethod()
    {
        _builder.SetHttpMethod("GET");

        var endpointConfig = _builder.Build();

        endpointConfig.Should().NotBeNull();
        endpointConfig.HttpMethod.Should().Be("GET");
    }

    [TestMethod]
    public void SetPath_WithValidPath_SetsPath()
    {
        _builder.SetPath("/api/data");

        var endpointConfig = _builder.Build();

        endpointConfig.Should().NotBeNull();
        endpointConfig.Path.Should().Be("/api/data");
    }

    [TestMethod]
    public void SetRequestSizeLimit_WithValidLimit_SetsRequestSizeLimit()
    {
        _builder.SetRequestSizeLimit(1024);

        var endpointConfig = _builder.Build();

        endpointConfig.Should().NotBeNull();
        endpointConfig.RequestSizeLimit.Should().Be(1024);
    }

    [TestMethod]
    public void SetResendPacketsOnCommunicationError_WithTrue_SetsFlag()
    {
        _builder.EnableResendOnCommunicationError();

        var endpointConfig = _builder.Build();
        endpointConfig.Should().NotBeNull();

        endpointConfig.ResendPacketsOnCommunicationError.Should().BeTrue();
    }

    [TestMethod]
    public void SetStoreMode_WithValidStoreMode_SetsStoreMode()
    {
        _builder.SetStoreMode(StoreMode.Persistent);

        var endpointConfig = _builder.Build();

        endpointConfig.Should().NotBeNull();
        endpointConfig.StoreMode.Should().Be(StoreMode.Persistent);
    }

    [TestMethod]
    public void SetTopic_WithValidTopic_SetsTopic()
    {
        _builder.SetTopic("topic1");

        var endpointConfig = _builder.Build();

        endpointConfig.Should().NotBeNull();
        endpointConfig.Topic.Should().Be("topic1");
    }

    [TestMethod]
    public void UseApi_WithValidApiName_SetsApiName()
    {
        _builder.UseApi("MyApi");

        var endpointConfig = _builder.Build();

        endpointConfig.Should().NotBeNull();
        endpointConfig.Api.Should().Be("MyApi");
    }

    [TestMethod]
    public void Build_WhenAllMethodsChained_ConfiguresAllProperties()
    {
        _builder
            .AddAcceptedContentType("application/json")
            .RequireAuthorization(true)
            .SetHttpMethod("POST")
            .SetPath("/submit")
            .SetRequestSizeLimit(2048)
            .EnableResendOnCommunicationError()
            .SetStoreMode(StoreMode.Persistent)
            .SetTopic("topic2")
            .UseApi("MyApi");

        var endpointConfig = _builder.Build();

        endpointConfig.Should().NotBeNull();
        endpointConfig.ContentTypes.Should().Contain("application/json");
        endpointConfig.Authorize.Should().BeTrue();
        endpointConfig.HttpMethod.Should().Be("POST");
        endpointConfig.Path.Should().Be("/submit");
        endpointConfig.RequestSizeLimit.Should().Be(2048);
        endpointConfig.ResendPacketsOnCommunicationError.Should().BeTrue();
        endpointConfig.StoreMode.Should().Be(StoreMode.Persistent);
        endpointConfig.Topic.Should().Be("topic2");
        endpointConfig.Api.Should().Be("MyApi");
    }
}
