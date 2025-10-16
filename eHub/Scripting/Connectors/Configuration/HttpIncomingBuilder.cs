using eHub.Config;
using eHub.PlugIn;
using eHub.PlugIn.Configuration;

namespace eHub.Scripting.Connectors.Configuration;

internal class HttpIncomingBuilder : IHttpIncomingBuilder
{
    private readonly HttpIncoming _httpIncoming = new();
    private readonly HashSet<string> _excludedPaths = [];

    public IHttpIncomingBuilder AddEndpoint(string name, Action<IEndpointBuilder> configure)
    {
        var endpointBuilder = new EndpointBuilder();
        configure(endpointBuilder);
        _httpIncoming.Endpoints.Add(name, endpointBuilder.Build());

        return this;
    }

    public IHttpIncomingBuilder AddEndpoint(string name, HttpConnectorEndpointConfig config)
    {
        _httpIncoming.Endpoints.Add(name, config);
        return this;
    }

    public IHttpIncomingBuilder ConfigureAuthentication(Action<IAuthenticationBuilder> configure)
    {
        var authenticationBuilder = new AuthenticationBuilder();
        configure(authenticationBuilder);
        _httpIncoming.Auth = authenticationBuilder.Build();
        _httpIncoming.Auth.Enabled = true;

        return this;
    }

    public IHttpIncomingBuilder ConfigureKestrel(Action<IKestrelBuilder> configure)
    {
        _httpIncoming.KestrelBuilder = new KestrelBuilder();
        configure(_httpIncoming.KestrelBuilder);

        return this;
    }

    public IHttpIncomingBuilder ExcludePathFromPacketTransfer(string path)
    {
        _excludedPaths.Add(path);
        return this;
    }

    public IHttpIncomingBuilder StoreUnauthorizedPackets()
    {
        _httpIncoming.InsertUnauthorizedPackets = true;
        return this;
    }

    public IHttpIncomingBuilder EnableResendOnInterrupt()
    {
        _httpIncoming.ResendInProgressPacketsOnInterrupt = true;
        return this;
    }

    public IHttpIncomingBuilder SetStoreMode(StoreMode mode)
    {
        _httpIncoming.StoreMode = mode;
        return this;
    }

    public HttpIncoming Build()
    {
        _httpIncoming.ExcludedEndpointsFromPacketTransfer = _excludedPaths;

        return _httpIncoming;
    }
}
