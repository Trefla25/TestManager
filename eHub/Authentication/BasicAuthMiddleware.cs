using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using eHub.Authentication.Service;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace eHub.Authentication;

/// <summary>Provides authentication support for the 'Basic' scheme in ASP.NET.</summary>
/// <remarks></remarks>
public class BasicAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IUserService userService)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary></summary>
    public const string SchemeName = "BasicAuthentication";
    private readonly IUserService _userService = userService;

    /// <summary></summary>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            Response.Headers.WWWAuthenticate = "Basic";
            return AuthenticateResult.Fail("Authorization header missing.");
        }

        // Get authorization key
        var authorizationHeader = authHeader.ToString();
        const string BasicPrefix = "Basic ";

        if (!authorizationHeader.StartsWith(BasicPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.Fail("Authorization code not formatted properly.");
        }

        string username, password;

        try
        {
            var authBase64 = Encoding.UTF8.GetString(Convert.FromBase64String(authorizationHeader[BasicPrefix.Length..]));
            var authSplit = authBase64.Split(':', 2);
            username = authSplit[0];
            password = authSplit[1];
        }
        catch
        {
            return AuthenticateResult.Fail("Authorization data not formatted properly.");
        }

        var user = await _userService.Authenticate(username, password);
        if (user == null)
        {
            return AuthenticateResult.Fail("The username or password is not correct.");
        }

        var claims = new[] {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
