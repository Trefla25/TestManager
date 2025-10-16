using eHub.Config;
using eHub.PlugIn.Configuration;
using Microsoft.AspNetCore.Authentication;

namespace eHub.Scripting.Connectors.Configuration;

public class AuthenticationBuilder : IAuthenticationBuilder
{
    private readonly AuthenticationConfig _authenticationConfig = new();

    public IAuthenticationBuilder AddBasic(Action<IBasicAuthBuilder> configure)
    {
        var basicAuthBuilder = new BasicAuthBuilder();
        configure(basicAuthBuilder);
        _authenticationConfig.Basic = basicAuthBuilder.Build();

        return this;
    }

    public IAuthenticationBuilder AddJwtBearer(Action<IJwtBearerAuthBuilder> configure)
    {
        var jwtBearerAuthBuilder = new JwtBearerAuthBuilder();
        configure(jwtBearerAuthBuilder);
        _authenticationConfig.JWT = jwtBearerAuthBuilder.Build();
        return this;
    }

    public IAuthenticationBuilder AddScheme<TOptions, THandler>(string schemeName, Action<TOptions>? configure = null)
        where TOptions : AuthenticationSchemeOptions, new()
        where THandler : AuthenticationHandler<TOptions>
    {
        var config = new AuthenticationSchemeConfig()
        {
            SchemeName = schemeName,
        };

        config.SetHandler<THandler, TOptions>(configure);

        _authenticationConfig.Schemes.Add(config);

        return this;
    }

    public AuthenticationConfig Build() => _authenticationConfig;
}

public class BasicAuthBuilder : IBasicAuthBuilder
{
    private readonly List<DummyUser> _users = [];

    public IBasicAuthBuilder AddDummyUser(string username, string password)
    {
        _users.Add(new DummyUser
        {
            Username = username,
            Password = password
        });

        return this;
    }

    public BasicAuthenticationConfig Build() => new()
    {
        DummyUsers = [.. _users]
    };
}

public class JwtBearerAuthBuilder : IJwtBearerAuthBuilder
{
    private readonly JWTAuthenticationConfig _jwtAuthenticationConfig = new();

    public IJwtBearerAuthBuilder SetAudience(string audience)
    {
        _jwtAuthenticationConfig.Audience = audience;
        return this;
    }

    public IJwtBearerAuthBuilder SetAuthority(string authority)
    {
        _jwtAuthenticationConfig.Authority = authority;
        return this;
    }

    public JWTAuthenticationConfig Build() => _jwtAuthenticationConfig;
}
