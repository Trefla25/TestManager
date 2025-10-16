namespace eHub.PlugIn.Configuration;

/// <summary>
/// Provides fluent methods to configure HTTP Outgoing options.
/// </summary>
public interface IHttpOutgoingBuilder
{
    /// <summary>
    /// Defines or configures an API by a specified name. These APIs can be referenced
    /// by outgoing endpoints via <see cref="IEndpointBuilder.UseApi(string)"/> and can also be retrieved directly
    /// using <see cref="IHttpClientFactory.CreateClient(string)"/> for sending HTTP requests with predefined settings (e.g., base address, authorization).
    /// </summary>
    /// <param name="name">A unique name to reference this API.</param>
    /// <param name="configure">
    /// An action that receives an <see cref="IApiBuilder"/> for fluent API configuration.
    /// </param>
    /// <returns>This <see cref="IHttpOutgoingBuilder"/> for chaining.</returns>
    IHttpOutgoingBuilder AddApi(string name, Action<IApiBuilder> configure);
    /// <summary>
    /// Adds a named endpoint to handle incoming requests, providing a fluent sub-builder.
    /// </summary>
    /// <param name="name">A unique name identifying this endpoint.</param>
    /// <param name="configure">
    /// An action that receives an <see cref="IEndpointBuilder"/> for fluent configuration.
    /// </param>
    /// <returns>This <see cref="IHttpOutgoingBuilder"/> for chaining.</returns>
    IHttpOutgoingBuilder AddEndpoint(string name, Action<IEndpointBuilder> configure);
    /// <summary>
    /// Adds a named endpoint by directly passing a <see cref="HttpConnectorEndpointConfig"/> object.
    /// </summary>
    /// <param name="name">A unique name identifying this endpoint.</param>
    /// <param name="config">The endpoint configuration object.</param>
    /// <returns>This <see cref="IHttpOutgoingBuilder"/> for chaining.</returns>
    IHttpOutgoingBuilder AddEndpoint(string name, HttpConnectorEndpointConfig config);
    /// <summary>
    /// Excludes a specific eMessenger topic from packet transfer logic. This is often used for
    /// endpoints like health checks that shouldn't always be stored or processed as packets.
    /// </summary>
    /// <param name="topic">The topic to exclude.</param>
    /// <returns>This <see cref="IHttpOutgoingBuilder"/> for chaining.</returns>
    IHttpOutgoingBuilder ExcludeTopicFromPacketTransfer(string topic);
    /// <summary>
    /// Specifies that packets that were in-progress during an ehub interruption (i.e., eHub shuts down) should be resent.
    /// </summary>
    /// <returns>This <see cref="IHttpOutgoingBuilder"/> for chaining.</returns>
    IHttpOutgoingBuilder EnableResendOnInterrupt();
    /// <summary>
    /// Enables reprocessing of packets upon communication errors such as network issues or server downtime.
    /// <returns>This <see cref="IEndpointBuilder"/> for chaining.</returns>
    IHttpOutgoingBuilder EnableResendOnCommunicationError();
    /// <summary>
    /// Sets the default store mode for all endpoint configurations, which determines how the endpoint
    /// configuration is persisted in the packet metadata. For example, in <see cref="StoreMode.Dynamic"/> only
    /// the key is stored, while in <see cref="StoreMode.Persistent"/> the full configuration is stored.
    /// </summary>
    /// <param name="mode">The <see cref="StoreMode"/> to use (e.g., Dynamic or Persistent).</param>
    /// <returns>This <see cref="IHttpOutgoingBuilder"/> for chaining.</returns>
    IHttpOutgoingBuilder SetStoreMode(StoreMode mode);
}
