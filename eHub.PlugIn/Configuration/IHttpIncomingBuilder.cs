using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace eHub.PlugIn.Configuration;

/// <summary>
/// Provides fluent methods to configure HTTP Incoming options.
/// </summary>
public interface IHttpIncomingBuilder
{
    /// <summary>
    /// Configures the internal Kestrel server options.
    /// </summary>
    /// <param name="configure">
    /// An action that receives an <see cref="IKestrelBuilder"/> for fluent configuration.
    /// </param>
    /// <returns>This <see cref="IHttpIncomingBuilder"/> for chaining.</returns>
    IHttpIncomingBuilder ConfigureKestrel(Action<IKestrelBuilder> configure);
    /// <summary>
    /// Configures authentication settings for incoming HTTP requests.
    /// If authentication is not needed, this step can be omitted.
    /// </summary>
    /// <param name="configure">
    /// An action that receives an <see cref="IAuthenticationBuilder"/> for fluent configuration.
    /// </param>
    /// <returns>This <see cref="IHttpIncomingBuilder"/> for chaining.</returns>
    IHttpIncomingBuilder ConfigureAuthentication(Action<IAuthenticationBuilder> configure);
    /// <summary>
    /// Adds a named endpoint to handle incoming requests, providing a fluent sub-builder.
    /// </summary>
    /// <param name="name">A unique name identifying this endpoint.</param>
    /// <param name="configure">
    /// An action that receives an <see cref="IEndpointBuilder"/> for fluent configuration.
    /// </param>
    /// <returns>This <see cref="IHttpIncomingBuilder"/> for chaining.</returns>
    IHttpIncomingBuilder AddEndpoint(string name, Action<IEndpointBuilder> configure);
    /// <summary>
    /// Adds a named endpoint by directly passing a <see cref="HttpConnectorEndpointConfig"/> object.
    /// </summary>
    /// <param name="name">A unique name identifying this endpoint.</param>
    /// <param name="config">The endpoint configuration object.</param>
    /// <returns>This <see cref="IHttpIncomingBuilder"/> for chaining.</returns>
    IHttpIncomingBuilder AddEndpoint(string name, HttpConnectorEndpointConfig config);
    /// <summary>
    /// Excludes a specific HTTP path from packet transfer logic. This is often used for 
    /// paths like health checks that shouldn't always be stored or processed as packets.
    /// </summary>
    /// <param name="path">The HTTP path to exclude.</param>
    /// <returns>This <see cref="IHttpIncomingBuilder"/> for chaining.</returns>
    IHttpIncomingBuilder ExcludePathFromPacketTransfer(string path);
    /// <summary>
    /// Specifies that incoming unauthorized requests should be inserted as packets.
    /// Typically <see langword="false"/> by default, but can be enabled if needed for debugging or other purposes.
    /// </summary>
    /// <returns>This <see cref="IHttpIncomingBuilder"/> for chaining.</returns>
    IHttpIncomingBuilder StoreUnauthorizedPackets();
    /// <summary>
    /// Specifies that packets that were in-progress during an ehub interruption (i.e., eHub shuts down) should be resent.
    /// </summary>
    /// <returns>This <see cref="IHttpIncomingBuilder"/> for chaining.</returns>
    IHttpIncomingBuilder EnableResendOnInterrupt();
    /// <summary>
    /// Sets the default store mode for all endpoint configurations, which determines how the endpoint
    /// configuration is persisted in the packet metadata. For example, in <see cref="StoreMode.Dynamic"/> only
    /// the key is stored, while in <see cref="StoreMode.Persistent"/> the full configuration is stored.
    /// </summary>
    /// <param name="mode">The <see cref="StoreMode"/> to use (e.g., Dynamic or Persistent).</param>
    /// <returns>This <see cref="IHttpIncomingBuilder"/> for chaining.</returns>
    IHttpIncomingBuilder SetStoreMode(StoreMode mode);
}

/// <summary>
/// Provides methods to configure the internal Kestrel server. 
/// </summary>
public interface IKestrelBuilder
{
    /// <summary>
    /// Configures the server limits for Kestrel.
    /// </summary>
    /// <param name="configure">
    /// An action that receives a <see cref="KestrelServerLimits"/> instance for customization.
    /// </param>
    /// <returns>This <see cref="IKestrelBuilder"/> for chaining.</returns>
    IKestrelBuilder ConfigureServerLimits(Action<KestrelServerLimits> configure);
    /// <summary>
    /// Configures Kestrel to listen on a specific address or URL.
    /// Multiple calls can be made to listen on multiple addresses.
    /// </summary>
    /// <param name="url">
    /// The URL or address to listen on (e.g., http://0.0.0.0:5000).
    /// </param>
    /// <param name="configure">
    /// An optional configuration action for <see cref="ListenOptions"/>, allowing you to further customize the endpoint,
    /// such as setting HTTPS options.
    /// </param>
    /// <returns>This <see cref="IKestrelBuilder"/> for chaining.</returns>
    IKestrelBuilder Listen(string url, Action<ListenOptions>? configure = null);
}
