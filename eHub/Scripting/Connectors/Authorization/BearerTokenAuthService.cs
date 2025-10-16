using System.Net.Http.Headers;
using eHub.Config;
using eHub.PlugIn.Authorization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace eHub.Scripting.Connectors.Authorization;

public class BearerTokenAuthService(
    IHttpClientFactory httpClientFactory,
    IOptions<BearerTokenAuthConfig> options)
    : IAuthorizationService
{
    private readonly BearerTokenAuthConfig _config = options.Value;

    private AuthToken? _authToken;

    public async Task ApplyAuthorizationAsync(HttpRequestHeaders headers)
    {
        var accessToken = await GetAccessToken();
        headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public async ValueTask<string> GetAccessToken()
    {
        if (_authToken == null || _authToken.IsExpired)
        {
            var accessTokenUrl = _config.AccessTokenUrl
                ?? throw new Exception("Token auth access token url is missing.");

            var response = _config.ContentType switch
            {
                ParameterContentType.Query => await PostAsQueryParameters(accessTokenUrl, _config.Parameters),
                ParameterContentType.FormUrlEncoded => await PostAsFormUrlEncoded(accessTokenUrl, _config.Parameters),
                ParameterContentType.Headers => await PostAsHeadersParameters(accessTokenUrl, _config.Parameters),
                _ => throw new NotImplementedException(_config.ContentType.ToString())
            };

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to request access token. Status Code: {response.StatusCode}. Error Message: {errorMessage}.");
            }

            var content = await response.Content.ReadAsStringAsync()
                ?? throw new Exception("Failed to parse the access token response.");

            _authToken = new AuthToken
            {
                AccessToken = content,
                TokenType = "Bearer",
                ExpiresIn = _config.TokenExpirationTime.Seconds,
                DateCreated = DateTime.Now
            };

        }

        return _authToken.AccessToken;
    }

    public void ClearAccessToken()
    {
        _authToken = null;
    }

    public async ValueTask<HttpResponseMessage> PostAsQueryParameters(string accessTokenUrl, Dictionary<string, string?> parameters)
    {
        var url = new Uri(QueryHelpers.AddQueryString(accessTokenUrl, parameters));

        using var httpClient = httpClientFactory.CreateClient();
        return await httpClient.PostAsync(url, null);
    }

    public async ValueTask<HttpResponseMessage> PostAsFormUrlEncoded(string accessTokenUrl, Dictionary<string, string?> parameters)
    {
        var content = new FormUrlEncodedContent(parameters);
        using var httpClient = httpClientFactory.CreateClient();
        return await httpClient.PostAsync(accessTokenUrl, content);
    }

    public async ValueTask<HttpResponseMessage> PostAsHeadersParameters(string accessTokenUrl, Dictionary<string, string?> parameters)
    {
        using var httpClient = httpClientFactory.CreateClient();

        foreach (var parameter in parameters)
        {
            if (!string.IsNullOrEmpty(parameter.Key) && !string.IsNullOrEmpty(parameter.Value))
            {
                httpClient.DefaultRequestHeaders.Add(parameter.Key, parameter.Value);
            }
        }

        return await httpClient.PostAsync(accessTokenUrl, null);
    }
}
