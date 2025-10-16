using eHub.Config;
using eHub.PlugIn.Authorization;
using eHub.PlugIn.Configuration;

namespace eHub.Scripting.Connectors.Configuration;

public class ApiBuilder : IApiBuilder
{
    private readonly ApiConfig _apiConfig = new();

    public IApiBuilder AddHeader(string headerName, params string[] values)
    {
        if (_apiConfig.Headers.TryGetValue(headerName, out var value))
        {
            value.UnionWith(values);
            return this;
        }

        _apiConfig.Headers.Add(headerName, [.. values]);
        return this;
    }

    public IApiBuilder UseRetryPolicy(Action<IRetryPolicyBuilder> configure)
    {
        var retryPolicyBuilder = new RetryPolicyBuilder();
        configure(retryPolicyBuilder);
        _apiConfig.RetryPolicy = retryPolicyBuilder.Build();

        return this;
    }

    public IApiBuilder SetBaseAddress(string baseAddress)
    {
        _apiConfig.BaseAddress = baseAddress;
        return this;
    }

    public IApiBuilder SetTimeout(TimeSpan timeout)
    {
        _apiConfig.Timeout = timeout;
        return this;
    }

    public IApiBuilder UseBasicAuthorization(string user, string password)
    {
        _apiConfig.Authorization ??= new();
        _apiConfig.Authorization.Type = AuthorizationType.Basic;
        _apiConfig.Authorization.Basic = new()
        {
            Username = user,
            Password = password
        };

        return this;
    }

    public IApiBuilder UseBearerAuthorization(Action<IBearerConfigBuilder> configure)
    {
        _apiConfig.Authorization ??= new();
        _apiConfig.Authorization.Type = AuthorizationType.BearerToken;

        var bearerTokenConfigBuilder = new BearerTokenConfigBuilder();
        configure(bearerTokenConfigBuilder);
        _apiConfig.Authorization.BearerToken = bearerTokenConfigBuilder.Build();

        return this;
    }

    public IApiBuilder UseOAuth2Authorization(Action<IOAuth2ConfigBuilder> configure)
    {
        _apiConfig.Authorization ??= new();
        _apiConfig.Authorization.Type = AuthorizationType.OAuth2;

        var oAuth2ConfigBuilder = new OAuth2ConfigBuilder();
        configure(oAuth2ConfigBuilder);
        _apiConfig.Authorization.OAuth2 = oAuth2ConfigBuilder.Build();

        return this;
    }

    public IApiBuilder UseAuthorization<T>() where T : class, IAuthorizationService
    {
        _apiConfig.Authorization ??= new();
        _apiConfig.Authorization.Type = AuthorizationType.Custom;
        _apiConfig.Authorization.Custom ??= new();
        _apiConfig.Authorization.Custom.TypeName = typeof(T).FullName;
        return this;
    }

    public ApiConfig Build() => _apiConfig;
}

public class RetryPolicyBuilder : IRetryPolicyBuilder
{
    private readonly RetryPolicyConfig _retryPolicyConfig = new();
    private readonly HashSet<string> _retryStatuses = [];

    public IRetryPolicyBuilder EnableRetryOnStatusCodes(params string[] patterns)
    {
        _retryStatuses.UnionWith(patterns);
        return this;
    }

    public IRetryPolicyBuilder EnableRetryOnException()
    {
        _retryPolicyConfig.RetryOnException = true;
        return this;
    }

    public IRetryPolicyBuilder SetRetryCount(int count)
    {
        _retryPolicyConfig.RetryCount = count;
        return this;
    }

    public IRetryPolicyBuilder SetRetryDelay(TimeSpan delay)
    {
        _retryPolicyConfig.RetryDelay = delay;
        return this;
    }

    public RetryPolicyConfig Build()
    {
        _retryPolicyConfig.ResponseStatuses = [.. _retryStatuses];
        return _retryPolicyConfig;
    }
}

public class BearerTokenConfigBuilder : IBearerConfigBuilder
{
    private readonly BearerTokenAuthConfig _bearerTokenAuthConfig = new();
    public IBearerConfigBuilder AddParameter(string key, string? value)
    {
        _bearerTokenAuthConfig.Parameters.Add(key, value);
        return this;
    }

    public IBearerConfigBuilder SetAccessTokenUrl(string url)
    {
        _bearerTokenAuthConfig.AccessTokenUrl = url;
        return this;
    }

    public IBearerConfigBuilder SetTokenExpirationTime(TimeSpan expiration)
    {
        _bearerTokenAuthConfig.TokenExpirationTime = expiration;
        return this;
    }

    public IBearerConfigBuilder UseQuery()
    {
        _bearerTokenAuthConfig.ContentType = ParameterContentType.Query;
        return this;
    }

    public IBearerConfigBuilder UseFormUrlEncoded()
    {
        _bearerTokenAuthConfig.ContentType = ParameterContentType.FormUrlEncoded;
        return this;
    }

    public IBearerConfigBuilder UseHeaders()
    {
        _bearerTokenAuthConfig.ContentType = ParameterContentType.Headers;
        return this;
    }

    public BearerTokenAuthConfig Build() => _bearerTokenAuthConfig;
}

public class OAuth2ConfigBuilder : IOAuth2ConfigBuilder
{
    private readonly OAuth2Config _oAuth2Config = new();

    public IOAuth2ConfigBuilder SetAccessTokenUrl(string accessTokenUrl)
    {
        _oAuth2Config.AccessTokenUrl = accessTokenUrl;
        return this;
    }

    public IOAuth2ConfigBuilder SetClientId(string clientId)
    {
        _oAuth2Config.ClientId = clientId;
        return this;
    }

    public IOAuth2ConfigBuilder SetClientSecret(string clientSecret)
    {
        _oAuth2Config.ClientSecret = clientSecret;
        return this;
    }

    public IOAuth2ConfigBuilder SetPassword(string password)
    {
        _oAuth2Config.Password = password;
        return this;
    }

    public IOAuth2ConfigBuilder SetScope(string scope)
    {
        _oAuth2Config.Scope = scope;
        return this;
    }

    public IOAuth2ConfigBuilder SetUsername(string username)
    {
        _oAuth2Config.Username = username;
        return this;
    }

    public IOAuth2ConfigBuilder SetCode(string code)
    {
        _oAuth2Config.Code = code;
        return this;
    }

    public IOAuth2ConfigBuilder SetRedirectUri(string redirectUri)
    {
        _oAuth2Config.RedirectUri = redirectUri;
        return this;
    }

    public IOAuth2ConfigBuilder UseAuthorizationCode()
    {
        _oAuth2Config.GrantType = GrantTypes.AuthorizationCode;
        return this;
    }

    public IOAuth2ConfigBuilder UseClientCredentials()
    {
        _oAuth2Config.GrantType = GrantTypes.ClientCredentials;
        return this;
    }

    public IOAuth2ConfigBuilder UsePassword()
    {
        _oAuth2Config.GrantType = GrantTypes.Password;
        return this;
    }

    public OAuth2Config Build() => _oAuth2Config;
}
