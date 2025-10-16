using eHub.Config;
using eHub.PlugIn;
using eHub.PlugIn.Configuration;

namespace eHub.Scripting.Connectors.Configuration;

internal class HttpOutgoingBuilder : IHttpOutgoingBuilder
{
    private readonly HttpOutgoing _httpOutgoing = new();
    private readonly HashSet<string> _excludedTopics = [];

    public IHttpOutgoingBuilder AddApi(string name, Action<IApiBuilder> configure)
    {
        _httpOutgoing.Api ??= [];

        var apiBuilder = new ApiBuilder();
        configure(apiBuilder);
        _httpOutgoing.Api.Add(name, apiBuilder.Build());

        return this;
    }

    public IHttpOutgoingBuilder AddEndpoint(string name, Action<IEndpointBuilder> configure)
    {
        var endpointBuilder = new EndpointBuilder();
        configure(endpointBuilder);
        _httpOutgoing.Endpoints.Add(name, endpointBuilder.Build());

        return this;
    }

    public IHttpOutgoingBuilder AddEndpoint(string name, HttpConnectorEndpointConfig config)
    {
        _httpOutgoing.Endpoints.Add(name, config);
        return this;
    }

    public IHttpOutgoingBuilder ExcludeTopicFromPacketTransfer(string topic)
    {
        _excludedTopics.Add(topic);
        return this;
    }

    public IHttpOutgoingBuilder EnableResendOnInterrupt()
    {
        _httpOutgoing.ResendInProgressPacketsOnInterrupt = true;
        return this;
    }

    public IHttpOutgoingBuilder EnableResendOnCommunicationError()
    {
        _httpOutgoing.ResendPacketsOnCommunicationError = true;
        return this;
    }

    public IHttpOutgoingBuilder SetStoreMode(StoreMode mode)
    {
        _httpOutgoing.StoreMode = mode;
        return this;
    }

    public HttpOutgoing Build()
    {
        _httpOutgoing.ExcludedEndpointsFromPacketTransfer = _excludedTopics;
        return _httpOutgoing;
    }
}
