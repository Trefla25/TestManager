using System.Net;

namespace eHub.Scripting.Connectors.Authorization;

public class OAuth2Handler(OAuth2Service oAuth2Service) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await oAuth2Service.ApplyAuthorizationAsync(request.Headers);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized)
        {
            // Retry request with new authorization token
            oAuth2Service.ClearAccessToken();
            await oAuth2Service.ApplyAuthorizationAsync(request.Headers);
            response = await base.SendAsync(request, cancellationToken);
        }

        return response;
    }
}
