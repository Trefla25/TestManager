using System.Net.Http.Headers;

namespace eHub.PlugIn.Authorization;

/// <summary>
/// The IAuthorizationService interface is designed TO manage custom authorization setups when configuring connectors for outgoing Http requests.
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// Configures the authorization headers for an outgoing HTTP request.
    /// </summary>
    /// <param name="headers"></param>
    /// <returns></returns>
    public Task ApplyAuthorizationAsync(HttpRequestHeaders headers);
}
