using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;

namespace eHub.Config;

#pragma warning disable CS8618 // Disable nullability warning for ORM-Wrapped config stuff

/// <summary>Authorization Configuration for the IntegrationHub.</summary>
public class AuthenticationConfig
{
    /// <summary>Path to the config object in the appsettings.json</summary>
    public const string DefaultKey = "Auth";
    public bool Enabled { get; set; } = true;
    public BasicAuthenticationConfig? Basic { get; set; }
    public JWTAuthenticationConfig? JWT { get; set; }
    public List<AuthenticationSchemeConfig> Schemes { get; set; } = [];
}

public class BasicAuthenticationConfig
{
    /// <summary>Users provided from the config which can be used to authenticate during development.</summary>
    public DummyUser[] DummyUsers { get; set; } = [];
}

public class JWTAuthenticationConfig
{
    public string Authority { get; set; }
    public string Audience { get; set; }
}

public class AuthenticationSchemeConfig
{
    public string SchemeName { get; set; } = string.Empty;

    [JsonIgnore]
    internal Action<AuthenticationBuilder>? Builder { get; private set; }
    public void SetHandler<THandler, TOptions>(Action<TOptions>? configureOptions = null)
        where THandler : AuthenticationHandler<TOptions>
        where TOptions : AuthenticationSchemeOptions, new()
    {
        Builder = builder => builder.AddScheme<TOptions, THandler>(this.SchemeName, configureOptions);
    }
}

/// <summary></summary>
public class DummyUser
{
    /// <summary></summary>
    public string Username { get; set; }
    /// <summary></summary>
    public string Password { get; set; }
}

#pragma warning restore CS8618
