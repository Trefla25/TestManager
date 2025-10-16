using Microsoft.AspNetCore.Authentication;

namespace eHub.PlugIn.Configuration;

/// <summary>
/// Provides methods to configure authentication for incoming HTTP requests.
/// </summary>
public interface IAuthenticationBuilder
{
    /// <summary>
    /// Enables Basic authentication and provides a sub-builder for defining dummy users.
    /// </summary>
    /// <param name="configure">
    /// An action that receives an <see cref="IBasicAuthBuilder"/> for fluent configuration.
    /// </param>
    /// <returns>This <see cref="IAuthenticationBuilder"/> for chaining.</returns>
    IAuthenticationBuilder AddBasic(Action<IBasicAuthBuilder> configure);
    /// <summary>
    /// Enables JWT Bearer authentication, providing a sub-builder for
    /// setting the JWT options (e.g., Audience, Authority).
    /// </summary>
    /// <param name="configure">
    /// An action that receives an <see cref="IJwtBearerAuthBuilder"/> for fluent configuration.
    /// </param>
    /// <returns>This <see cref="IAuthenticationBuilder"/> for chaining.</returns>
    IAuthenticationBuilder AddJwtBearer(Action<IJwtBearerAuthBuilder> configure); // TODO: we could use JwtBearerOptions instead of a builder here?
    /// <summary>
    /// Adds a custom authentication scheme with the given name, options type, and handler.
    /// </summary>
    /// <typeparam name="TOptions">Type of scheme options.</typeparam>
    /// <typeparam name="THandler">AuthenticationHandler type that handles this scheme.</typeparam>
    /// <param name="schemeName">Unique name for the scheme.</param>
    /// <param name="configure">Optional setup for the scheme options.</param>
    /// <returns>This <see cref="IAuthenticationBuilder"/> for chaining.</returns>
    public IAuthenticationBuilder AddScheme<TOptions, THandler>(string schemeName, Action<TOptions>? configure = null)
        where TOptions : AuthenticationSchemeOptions, new()
        where THandler : AuthenticationHandler<TOptions>;
}

/// <summary>
/// Provides methods for setting up Basic authentication.
/// </summary>
public interface IBasicAuthBuilder
{
    /// <summary>
    /// Adds a dummy user with username and password. These can be used
    /// for simple or development scenarios.
    /// </summary>
    /// <param name="username">The username for the dummy user.</param>
    /// <param name="password">The password for the dummy user.</param>
    /// <returns>This <see cref="IBasicAuthBuilder"/> for chaining.</returns>
    IBasicAuthBuilder AddDummyUser(string username, string password);
}

/// <summary>
/// Provides methods for setting up JWT Bearer authentication.
/// </summary>
public interface IJwtBearerAuthBuilder
{
    /// <summary>
    /// Sets the Authority (e.g., the issuer or IDP) for validating tokens.
    /// Typically a URL to your identity provider.
    /// </summary>
    /// <param name="authority">The Authority for JWT validation.</param>
    /// <returns>This <see cref="IJwtBearerAuthBuilder"/> for chaining.</returns>
    IJwtBearerAuthBuilder SetAuthority(string authority);
    /// <summary>
    /// Sets the expected Audience in the JWT token. This typically indicates
    /// the intended recipient of the token.
    /// </summary>
    /// <param name="audience">The JWT Audience claim value.</param>
    /// <returns>This <see cref="IJwtBearerAuthBuilder"/> for chaining.</returns>
    IJwtBearerAuthBuilder SetAudience(string audience);
}
