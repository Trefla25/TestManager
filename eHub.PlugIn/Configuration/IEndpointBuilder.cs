namespace eHub.PlugIn.Configuration;

/// <summary>
/// Provides methods to configure an endpoint
/// </summary>
public interface IEndpointBuilder
{
    /// <summary>
    /// Sets the path or route pattern for this endpoint. For incoming, it's the
    /// route Kestrel should match; for outgoing, it's used to create an HTTP request (appended to the API's base address if used, or treated as absolute path if not).
    /// </summary>
    /// <param name="path">The route path.</param>
    /// <returns>This <see cref="IEndpointBuilder"/> for chaining.</returns>
    IEndpointBuilder SetPath(string path);
    /// <summary>
    /// Sets the HTTP method (GET, POST, etc.). For incoming, it's the method Kestrel should match;
    /// for outgoing, it's used in the outgoing HTTP request.
    /// </summary>
    /// <param name="method">The HTTP method string.</param>
    /// <returns>This <see cref="IEndpointBuilder"/> for chaining.</returns>
    IEndpointBuilder SetHttpMethod(string method);
    /// <summary>
    /// Sets the eMessenger topic. For incoming endpoints, this is the topic where messages are redirected to;
    /// for outgoing endpoints, it specifies the topic from which messages are received for further processing.
    /// </summary>
    /// <param name="topic">The eMessenger topic name.</param>
    /// <returns>This <see cref="IEndpointBuilder"/> for chaining.</returns>
    IEndpointBuilder SetTopic(string topic);
    /// <summary>
    /// Controls whether this endpoint requires authorization. If set to true, 
    /// incoming requests must be authenticated; otherwise, it's open.
    /// </summary>
    /// <param name="require"><see langword="true"/> to require auth; <see langword="false"/> to disable auth checks.</param>
    /// <returns>This <see cref="IEndpointBuilder"/> for chaining.</returns>
    IEndpointBuilder RequireAuthorization(bool require);
    /// <summary>
    /// Adds an accepted content type (MIME type) for this endpoint.
    /// </summary>
    /// <param name="contentType">The MIME type to add (e.g., "application/json").</param>
    /// <returns>This <see cref="IEndpointBuilder"/> for chaining.</returns>
    IEndpointBuilder AddAcceptedContentType(string contentType);
    /// <summary>
    /// For outgoing endpoints, references an existing named API to use it's predefined configurations (e.g., base address, authorization).
    /// </summary>
    /// <param name="apiName">The name of the API defined in <see cref="IHttpOutgoingBuilder.AddApi"/>.</param>
    /// <returns>This <see cref="IEndpointBuilder"/> for chaining.</returns>
    IEndpointBuilder UseApi(string apiName);
    /// <summary>
    /// Sets the maximum size of the HTTP request body for this endpoint.
    /// </summary>
    /// <param name="requestSizeLimit">The maximum request body size (e.g., 1048576 for 1 MB).</param>
    /// <returns>This <see cref="IEndpointBuilder"/> for chaining.</returns>
    IEndpointBuilder SetRequestSizeLimit(long requestSizeLimit);
    /// <summary>
    /// Enables reprocessing of packets upon communication errors such as network issues or server downtime.
    /// <returns>This <see cref="IEndpointBuilder"/> for chaining.</returns>
    IEndpointBuilder EnableResendOnCommunicationError();
    /// <summary>
    /// Specifies the store mode for the endpoint configuration, controlling 
    /// whether it's stored dynamically or persistently in the database metadata.
    /// </summary>
    /// <param name="mode">The <see cref="StoreMode"/> to use.</param>
    /// <returns>This <see cref="IEndpointBuilder"/> for chaining.</returns>
    IEndpointBuilder SetStoreMode(StoreMode mode);
}
