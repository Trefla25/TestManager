using Microsoft.Extensions.Configuration;

namespace eHub.PlugIn.Configuration;

/// <summary>
/// Provides a top-level builder for configuring a Connector.
/// </summary>
public interface IConnectorConfigBuilder
{
    /// <summary>
    /// Gets the configuration instance built from the Config section in your connector configuration file.
    /// </summary>
    IConfiguration Configuration { get; }
    /// <summary>
    /// Configures Packet Transfer settings (e.g., database path, channel groups)
    /// for connectors that implement <see cref="IPacketTransfer"/>.
    /// </summary>
    /// <param name="configure">
    /// An action that receives an <see cref="IPacketTransferBuilder"/> for fluent configuration.
    /// </param>
    /// <returns>This <see cref="IConnectorConfigBuilder"/> for chaining.</returns>
    IConnectorConfigBuilder ConfigurePacketTransfer(Action<IPacketTransferBuilder> configure);
    /// <summary>
    /// Configures incoming HTTP settings (e.g., kestrel, endpoints, authentication)
    /// for connectors that implement <see cref="IHttpConnector"/> to handle incoming requests.
    /// </summary>
    /// <param name="configure">
    /// An action that receives an <see cref="IHttpIncomingBuilder"/> for fluent configuration.
    /// </param>
    /// <returns>This <see cref="IConnectorConfigBuilder"/> for chaining.</returns>
    IConnectorConfigBuilder ConfigureHttpIncoming(Action<IHttpIncomingBuilder> configure);
    /// <summary>
    /// Configures outgoing HTTP settings (e.g., APIs, endpoints, retry policies) 
    /// for connectors that implement <see cref="IHttpConnector"/> to handle outgoing requests. 
    /// </summary>
    /// <param name="configure">
    /// An action that receives an <see cref="IHttpOutgoingBuilder"/> for fluent configuration.
    /// </param>
    /// <returns>This <see cref="IConnectorConfigBuilder"/> for chaining.</returns>
    IConnectorConfigBuilder ConfigureHttpOutgoing(Action<IHttpOutgoingBuilder> configure);
    /// <summary>
    /// Sets a raw configuration value by specifying its full path as a key.
    /// This allows direct assignment of configuration values without using the builder pattern.
    /// </summary>
    /// <typeparam name="T">The type of the configuration value.</typeparam>
    /// <param name="key">
    /// The hierarchical key representing the configuration path.
    /// </param>
    /// <param name="value">The value to be assigned to the specified key.</param>
    /// <returns>This <see cref="IConnectorConfigBuilder"/> for chaining.</returns>
    IConnectorConfigBuilder SetRawConfigValue<T>(string key, T? value);
}
