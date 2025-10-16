using System.Security.Claims;
using System.Text.Encodings.Web;
using eHub.PlugIn.Communication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace eHub.Authentication;

public class SignatureAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "eHubSignatureAuthentication";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ConnectorHeaders.PacketIdHeader, out _) ||
            !Request.Headers.TryGetValue(ConnectorHeaders.SignatureHeader, out _))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[] { new Claim(ClaimTypes.Name, "eHub") };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
