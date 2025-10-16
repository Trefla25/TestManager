using eHub.PlugIn;
using eHub.PlugIn.Configuration;

namespace eHub.Scripting.Connectors.Configuration;

public class EndpointBuilder : IEndpointBuilder
{
    private readonly HttpConnectorEndpointConfig _config = new();
    private readonly HashSet<string> _acceptedContentTypes = [];

    public IEndpointBuilder AddAcceptedContentType(string contentType)
    {
        _acceptedContentTypes.Add(contentType);
        return this;
    }

    public IEndpointBuilder RequireAuthorization(bool require)
    {
        _config.Authorize = require;
        return this;
    }
    public IEndpointBuilder SetHttpMethod(string method)
    {
        _config.HttpMethod = method;
        return this;
    }

    public IEndpointBuilder SetPath(string path)
    {
        _config.Path = path;
        return this;
    }

    public IEndpointBuilder SetRequestSizeLimit(long requestSizeLimit)
    {
        _config.RequestSizeLimit = requestSizeLimit;
        return this;
    }

    public IEndpointBuilder EnableResendOnCommunicationError()
    {
        _config.ResendPacketsOnCommunicationError = true;
        return this;
    }

    public IEndpointBuilder SetStoreMode(StoreMode mode)
    {
        _config.StoreMode = mode;
        return this;
    }

    public IEndpointBuilder SetTopic(string topic)
    {
        _config.Topic = topic;
        return this;
    }

    public IEndpointBuilder UseApi(string apiName)
    {
        _config.Api = apiName;
        return this;
    }

    public HttpConnectorEndpointConfig Build()
    {
        _config.ContentTypes = [.. _acceptedContentTypes];
        return _config;
    }
}
