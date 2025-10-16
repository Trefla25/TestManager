using eHub.PlugIn.Authorization;

namespace eHub.Scripting.Connectors.Authorization;

public class CustomAuthorizationHandler(IAuthorizationService authorizationService) : DelegatingHandler 
{
    private readonly IAuthorizationService _authorizationService = authorizationService;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
       await _authorizationService.ApplyAuthorizationAsync(request.Headers);

        return await base.SendAsync(request, cancellationToken);
    }
}
