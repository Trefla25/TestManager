using System.Net.Http.Headers;
using eHub.Config;
using eHub.PlugIn.Authorization;
using Microsoft.Extensions.Options;

namespace eHub.Scripting.Connectors.Authorization;

public class OAuth2Service(
    IHttpClientFactory httpClientFactory,
    IOptions<OAuth2Config> options)
    : IAuthorizationService
{
    private readonly OAuth2Config _config = options.Value;

    private AuthToken? _authToken;

    public async Task ApplyAuthorizationAsync(HttpRequestHeaders headers)
    {
        var accessToken = await GetAccessToken();
        headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public async ValueTask<string> GetAccessToken()
    {
        if (_authToken == null
            || _authToken.IsExpired)
        {
            _authToken = _config.GrantType switch
            {
                GrantTypes.ClientCredentials => await GetClientCredentialsAccessToken(),
                GrantTypes.Password => await GetPasswordAccessToken(),
                _ => throw new NotImplementedException($"Grant type \"{_config.GrantType}\" not implemented.")
            };

            _authToken.DateCreated = DateTime.Now;
        }

        return _authToken.AccessToken;
    }

    public void ClearAccessToken()
    {
        _authToken = null;
    }

    private async ValueTask<AuthToken> GetClientCredentialsAccessToken()
    {
        var clientId = _config.ClientId
            ?? throw new Exception("OAuth2 client id is missing.");
        var clientSecret = _config.ClientSecret
            ?? throw new Exception("OAuth2 client secret is missing.");
        var accessTokenUrl = _config.AccessTokenUrl
            ?? throw new Exception("OAuth2 access token url is missing.");

        using var httpClient = httpClientFactory.CreateClient();
        var response = await httpClient.PostAsync(accessTokenUrl, new FormUrlEncodedContent([
            new KeyValuePair<string, string>("grant_type", GrantTypes.ClientCredentials),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret)]));

        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to request access token. Status Code: {response.StatusCode}. Error Message: {errorMessage}.");
        }

        var content = await response.Content.ReadFromJsonAsync<AuthToken>()
            ?? throw new Exception("Failed to parse the access token response.");

        return content;
    }

    private async ValueTask<AuthToken> GetPasswordAccessToken()
    {
        var username = _config.Username
            ?? throw new Exception("OAuth2 username is missing.");
        var password = _config.Password
            ?? throw new Exception("OAuth2 password is missing.");
        var accessTokenUrl = _config.AccessTokenUrl
            ?? throw new Exception("OAuth2 access token url is missing.");

        using var httpClient = httpClientFactory.CreateClient();
        var response = await httpClient.PostAsync(accessTokenUrl, new FormUrlEncodedContent([
            new KeyValuePair<string, string>("grant_type", GrantTypes.Password),
            new KeyValuePair<string, string>("username", username),
            new KeyValuePair<string, string>("password", password)]));

        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to request access token. Status Code: {response.StatusCode}. Error Message: {errorMessage}.");
        }

        var content = await response.Content.ReadFromJsonAsync<AuthToken>()
            ?? throw new Exception("Failed to parse the access token response.");

        return content;
    }
}
