using System.Net.Http.Headers;
using System.Text;
using eHub.Config;
using eHub.PlugIn.Authorization;
using Microsoft.Extensions.Options;

namespace eHub.Scripting.Connectors.Authorization;

public class BasicAuthService(
    IOptions<BasicAuthorizationConfig> options)
    : IAuthorizationService
{
    private readonly BasicAuthorizationConfig _config = options.Value;

    public Task ApplyAuthorizationAsync(HttpRequestHeaders headers)
    {
        var username = _config.Username ?? throw new Exception("Basic auth username is missing.");
        var password = _config.Password ?? throw new Exception("Basic auth password is missing.");
        var accessToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));

        headers.Authorization = new AuthenticationHeaderValue("Basic", accessToken);

        return Task.CompletedTask;
    }
}
