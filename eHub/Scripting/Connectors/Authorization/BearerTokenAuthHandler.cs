using System.Net;

namespace eHub.Scripting.Connectors.Authorization;

public class BearerTokenAuthHandler(BearerTokenAuthService bearerTokenAuthService) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await bearerTokenAuthService.ApplyAuthorizationAsync(request.Headers);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized)
        {
            // Retry request with new authorization token
            bearerTokenAuthService.ClearAccessToken();
            await bearerTokenAuthService.ApplyAuthorizationAsync(request.Headers);
            response = await base.SendAsync(request, cancellationToken);
        }

        return response;
    }
}
