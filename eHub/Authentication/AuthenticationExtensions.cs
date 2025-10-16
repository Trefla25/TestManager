using eHub.Authentication.Service;
using eHub.Config;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace eHub.Authentication;

public static class AuthenticationExtensions
{
    public static void AddEHubAuthentication(this IServiceCollection services, AuthenticationConfig authConfig)
    {
        if (!authConfig.Enabled)
        {
            return;
        }

        var policyBuilder = new AuthorizationPolicyBuilder().RequireAuthenticatedUser();

        var authBuilder = services.AddAuthentication();

        if (authConfig.Basic is not null)
        {
            services.AddScoped<IUserService, UserService>();

            authBuilder.AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>(
                BasicAuthenticationHandler.SchemeName, _ => { });

            policyBuilder.AddAuthenticationSchemes(BasicAuthenticationHandler.SchemeName);
        }

        if (authConfig.JWT is not null)
        {
            authBuilder.AddJwtBearer("Bearer", options =>
            {
                options.Authority = authConfig.JWT.Authority;
                options.Audience = authConfig.JWT.Audience;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidateIssuer = true,
                    ValidateAudience = true
                };
            });

            policyBuilder.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
        }

        foreach (var scheme in authConfig.Schemes)
        {
            scheme.Builder?.Invoke(authBuilder);
            policyBuilder.AddAuthenticationSchemes(scheme.SchemeName);
        }

        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(policyBuilder.Build());
    }
}
