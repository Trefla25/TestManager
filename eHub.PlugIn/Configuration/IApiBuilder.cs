using eHub.PlugIn.Authorization;

namespace eHub.PlugIn.Configuration;

/// <summary>
/// Provides methods to configure a named API.
/// </summary>
public interface IApiBuilder
{
    /// <summary>
    /// Sets the base address (URL) for the API requests. 
    /// </summary>
    /// <param name="baseAddress">The base URL for this API.</param>
    /// <returns>This <see cref="IApiBuilder"/> for chaining.</returns>
    IApiBuilder SetBaseAddress(string baseAddress);
    /// <summary>
    /// Sets the timeout for requests sent to this API.
    /// </summary>
    /// <param name="timeout">The desired timeout duration.</param>
    /// <returns>This <see cref="IApiBuilder"/> for chaining.</returns>
    IApiBuilder SetTimeout(TimeSpan timeout);
    /// <summary>
    /// Configures an optional retry policy for calls to this API.
    /// </summary>
    /// <param name="configure">
    /// An action that receives an <see cref="IRetryPolicyBuilder"/> for fluent configuration.
    /// </param>
    /// <returns>This <see cref="IApiBuilder"/> for chaining.</returns>
    IApiBuilder UseRetryPolicy(Action<IRetryPolicyBuilder> configure);
    /// <summary>
    /// Adds additional default headers that will be sent with every request to this API.
    /// Each header can have one or more values.
    /// </summary>
    /// <param name="headerName">The HTTP header name (e.g., "X-My-Custom-Header").</param>
    /// <param name="values">One or more values for this header.</param>
    /// <returns>This <see cref="IApiBuilder"/> for chaining.</returns>
    IApiBuilder AddHeader(string headerName, params string[] values);
    /// <summary>
    /// Sets this API to use Basic Authorization. Only one auth type is allowed,
    /// so calling this will override any previously configured authorization.
    /// /// </summary>
    /// <param name="user">The username for Basic authentication.</param>
    /// <param name="password">The password for Basic authentication.</param>
    /// <returns>This <see cref="IApiBuilder"/> for chaining.</returns>
    IApiBuilder UseBasicAuthorization(string user, string password);
    /// <summary>
    /// Sets this API to use OAuth2 Authorization. Only one auth type is allowed,
    /// so calling this will override any previously configured authorization.
    /// </summary>
    /// <param name="configure">
    /// An action that receives an <see cref="IOAuth2ConfigBuilder"/> to configure details
    /// such as token URL, client credentials, or grant type.
    /// </param>
    /// <returns>This <see cref="IApiBuilder"/> for chaining.</returns>
    IApiBuilder UseOAuth2Authorization(Action<IOAuth2ConfigBuilder> configure);
    /// <summary>
    /// Sets this API to use Bearer Token Authorization. Only one auth type is allowed,
    /// so calling this will override any previously configured authorization.
    /// </summary>
    /// <param name="configure">
    /// An action that receives an <see cref="IBearerConfigBuilder"/> to configure 
    /// how the bearer token is requested or refreshed.
    /// </param>
    /// <returns>This <see cref="IApiBuilder"/> for chaining.</returns>
    IApiBuilder UseBearerAuthorization(Action<IBearerConfigBuilder> configure);

    /// <summary>
    /// Sets authorization type. Only one auth type is allowed,
    /// so calling this with another type will override any previously configured authorization 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    IApiBuilder UseAuthorization<T>() where T : class, IAuthorizationService;
}

/// <summary>
/// Provides fluent methods to configure retry policies.
/// </summary>
public interface IRetryPolicyBuilder
{
    /// <summary>
    /// Sets how many times a request should be retried if it fails.
    /// </summary>
    /// <param name="count">Number of retries.</param>
    /// <returns>This <see cref="IRetryPolicyBuilder"/> for chaining.</returns>
    IRetryPolicyBuilder SetRetryCount(int count);
    /// <summary>
    /// Sets the delay between retry attempts.
    /// </summary>
    /// <param name="delay">The delay as a <see cref="TimeSpan"/>.</param>
    /// <returns>This <see cref="IRetryPolicyBuilder"/> for chaining.</returns>
    IRetryPolicyBuilder SetRetryDelay(TimeSpan delay);
    /// <summary>
    /// Enables the client to retry when a communication exception occurs (i.e, the host is down).
    /// </summary>
    /// <returns>This <see cref="IRetryPolicyBuilder"/> for chaining.</returns>
    IRetryPolicyBuilder EnableRetryOnException();
    /// <summary>
    /// Enables the client to retry when specific statuses are returned.
    /// </summary>
    /// <param name="patterns">
    /// One or more status patterns to match an HTTP status code. 
    /// 'X' can be used as a wildcard digit (e.g., "4XX" or "5x3").
    /// </param>
    /// <returns>This <see cref="IRetryPolicyBuilder"/> for chaining.</returns>
    IRetryPolicyBuilder EnableRetryOnStatusCodes(params string[] patterns);
}

/// <summary>
/// Provides fluent methods to configure OAuth2 authorization.
/// </summary>
public interface IOAuth2ConfigBuilder
{
    /// <summary>
    /// Specifies the URL of the token endpoint where OAuth2 tokens will be requested.
    /// </summary>
    /// <param name="accessTokenUrl">The token endpoint URL.</param>
    /// <returns>This <see cref="IOAuth2ConfigBuilder"/> for chaining.</returns>
    IOAuth2ConfigBuilder SetAccessTokenUrl(string accessTokenUrl);
    /// <summary>
    /// Specifies the scope(s) that will be requested as part of the OAuth2 token request.
    /// Multiple scopes can be specified as a space-separated string.
    /// </summary>
    /// <param name="scope">The scope string (e.g., "read write").</param>
    /// <returns>This <see cref="IOAuth2ConfigBuilder"/> for chaining.</returns>
    IOAuth2ConfigBuilder SetScope(string scope);
    /// <summary>
    /// Specifies the client identifier used in OAuth2 requests.
    /// </summary>
    /// <param name="clientId">The client identifier.</param>
    /// <returns>This <see cref="IOAuth2ConfigBuilder"/> for chaining.</returns>
    IOAuth2ConfigBuilder SetClientId(string clientId);
    /// <summary>
    /// Specifies the client secret used in OAuth2 requests.
    /// </summary>
    /// <param name="clientSecret">The client secret.</param>
    /// <returns>This <see cref="IOAuth2ConfigBuilder"/> for chaining.</returns>
    IOAuth2ConfigBuilder SetClientSecret(string clientSecret);
    /// <summary>
    /// Specifies the authorization code received from the OAuth2 authorization server.
    /// This is used in the authorization code grant flow.
    /// </summary>
    /// <param name="code">The authorization code.</param>
    /// <returns>This <see cref="IOAuth2ConfigBuilder"/> for chaining.</returns>
    IOAuth2ConfigBuilder SetCode(string code);
    /// <summary>
    /// Specifies the redirect URI that was registered with the OAuth2 authorization server.
    /// </summary>
    /// <param name="redirectUri">The redirect URI.</param>
    /// <returns>This <see cref="IOAuth2ConfigBuilder"/> for chaining.</returns>
    IOAuth2ConfigBuilder SetRedirectUri(string redirectUri);
    /// <summary>
    /// Specifies the username for OAuth2 flows that require user credentials (e.g., password grant).
    /// </summary>
    /// <param name="username">The username.</param>
    /// <returns>This <see cref="IOAuth2ConfigBuilder"/> for chaining.</returns>
    IOAuth2ConfigBuilder SetUsername(string username);
    /// <summary>
    /// Specifies the password for OAuth2 flows that require user credentials (e.g., password grant).
    /// </summary>
    /// <param name="password">The password.</param>
    /// <returns>This <see cref="IOAuth2ConfigBuilder"/> for chaining.</returns>
    IOAuth2ConfigBuilder SetPassword(string password);
    /// <summary>
    /// Configures the builder to use the OAuth2 authorization code grant type.
    /// </summary>
    /// <returns>This <see cref="IOAuth2ConfigBuilder"/> for chaining.</returns>
    IOAuth2ConfigBuilder UseAuthorizationCode();
    /// <summary>
    /// Configures the builder to use the OAuth2 client credentials grant type.
    /// </summary>
    /// <returns>This <see cref="IOAuth2ConfigBuilder"/> for chaining.</returns>
    IOAuth2ConfigBuilder UseClientCredentials();
    /// <summary>
    /// Configures the builder to use the OAuth2 resource owner password grant type.
    /// </summary>
    /// <returns>This <see cref="IOAuth2ConfigBuilder"/> for chaining.</returns>
    IOAuth2ConfigBuilder UsePassword();
}

/// <summary>
/// Provides fluent methods to configure Bearer Token authorization.
/// </summary>
public interface IBearerConfigBuilder
{
    /// <summary>
    /// Sets the URL endpoint for obtaining or refreshing a bearer token.
    /// </summary>
    /// <param name="url">The URL endpoint for the token request.</param>
    /// <returns>This <see cref="IBearerConfigBuilder"/> for chaining.</returns>
    IBearerConfigBuilder SetAccessTokenUrl(string url);
    /// <summary>
    /// Sets the expected lifetime of the token.
    /// After this duration, a new token must be requested.
    /// </summary>
    /// <param name="expiration">The token expiration time as a <see cref="TimeSpan"/>.</param>
    /// <returns>This <see cref="IBearerConfigBuilder"/> for chaining.</returns>
    IBearerConfigBuilder SetTokenExpirationTime(TimeSpan expiration);
    /// <summary>
    /// Adds a parameter key/value pair that will be sent along with the bearer token request.
    /// These parameters might include client credentials or other necessary data.
    /// </summary>
    /// <param name="key">The key for the parameter.</param>
    /// <param name="value">The value for the parameter.</param>
    /// <returns>This <see cref="IBearerConfigBuilder"/> for chaining.</returns>
    IBearerConfigBuilder AddParameter(string key, string? value);
    /// <summary>
    /// Configures the bearer token request to send its parameters via the query string.
    /// </summary>
    /// <returns>This <see cref="IBearerConfigBuilder"/> for chaining.</returns>
    IBearerConfigBuilder UseQuery();
    /// <summary>
    /// Configures the bearer token request to send its parameters using form URL-encoded content.
    /// </summary>
    /// <returns>This <see cref="IBearerConfigBuilder"/> for chaining.</returns>
    IBearerConfigBuilder UseFormUrlEncoded();
    /// <summary>
    /// Configures the bearer token request to send its parameters in the HTTP headers.
    /// </summary>
    /// <returns>This <see cref="IBearerConfigBuilder"/> for chaining.</returns>
    IBearerConfigBuilder UseHeaders();
}
