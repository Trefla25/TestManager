namespace eHub.Scripting.Connectors.Authorization;

public class BasicAuthHandler(BasicAuthService basicAuthService) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await basicAuthService.ApplyAuthorizationAsync(request.Headers);

        return await base.SendAsync(request, cancellationToken);
    }
}
