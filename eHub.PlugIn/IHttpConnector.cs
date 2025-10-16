using System.Net.Mime;
using eHub.PlugIn.Communication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace eHub.PlugIn;

/// <summary>
/// Implement this interface in a public class to expose a http connector.<br/>
/// A connector can be instantiated multiple times with different run parameters via connector config files.
/// 
/// <para>
/// You can request any shared component via dependency injection to the constructor.<br/>
/// Common utilities like 
/// <see href="https://docs.microsoft.com/en-us/dotnet/core/extensions/logging?tabs=command-line#create-logs">Logging</see> and
/// <see href="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-6.0#options-interfaces">Options</see>
/// are available.
/// </para>
/// <para>
/// Use <see cref="IOptions{TOptions}"/>, <see cref="IOptionsSnapshot{TOptions}"/>
/// or <see cref="IOptionsMonitor{TOptions}"/> with a custom object annotated with
/// <see cref="ScriptOptionsAttribute"/> to map the passed config.<br/>
/// You can refer to the 
/// <see href="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-6.0#options-interfaces">Options pattern in ASP.NET Core Documentation</see>
/// for usage.
/// </para>
/// </summary>
public interface IHttpConnector : IConnector
{
    /// <summary>
    /// Configures HTTP routes programmatically, using <see cref="IEndpointRouteBuilder"/> to dynamically map endpoints. 
    /// This method offers greater flexibility than configuration-based approaches, allowing better URL parameters handling.
    /// </summary>
    /// <param name="routeBuilder">The route builder used to map endpoints dynamically.</param>
    /// <remarks>
    /// Using <see cref="HttpSetup"/> is an alternative to the HttpIncoming configuration settings for incoming requests. 
    /// Avoid mapping the same endpoint in both <see cref="HttpSetup"/> and configuration to prevent request conflicts.
    /// </remarks>
    void HttpSetup(IEndpointRouteBuilder routeBuilder);

    /// <summary>
    /// Gets or sets the HTTP connector options.
    /// <para>
    /// Contains options for overriding the default implementations of the HTTP connector.
    /// </para>
    /// </summary>
    HttpConnectorOptions HttpConnectorOptions { get; }
}

/// <summary>
/// Options for overriding the default implementations of the HTTP connector.
/// </summary>
public class HttpConnectorOptions
{
    /// <summary>
    /// Gets the delegate responsible for executing an incoming HTTP request directed to this connector. 
    /// </summary>
    /// <remarks>
    /// Set this delegate using <see cref="SetIncomingHttpRequestHandler"/> in <see cref="IConnector.Run"/> to customize the handling of incoming HTTP requests.
    /// If this property is not set, the default eHub implementation will be used.
    /// <para>
    /// <b>Default Implementation</b>: If not set, the default eHub implementation will take the content, path (including query and route parameters), and headers from the incoming HTTP request
    /// and forward them via <c>eMessenger.Ask</c> to another connector. The response from the other connector is then used to respond to the original HTTP request.
    /// </para>
    /// </remarks>
    public ExecuteIncomingHttpRequestDelegate? IncomingHttpRequestHandler { get; set; }

    /// <summary>
    /// Gets the delegate responsible for executing an outgoing HTTP request.
    /// </summary>
    /// <remarks>
    /// Set this delegate using <see cref="SetOutgoingHttpRequestHandler"/> in <see cref="IConnector.Run"/> to customize the handling of outgoing HTTP requests.
    /// If this property is not set, the default eHub implementation will be used.
    /// <para>
    /// <b>Default Implementation</b>: If not set, the default eHub implementation will use the content, path (including query and route parameters), and headers from an <c>eMessenger.Ask</c> request,
    /// along with the endpoint configuration details, to construct and execute an HTTP request. The response is converted into a <see cref="ConnectorResponse"/> and returned to the initiating connector through the <c>eMessenger.AnswerAsync</c>.
    /// </para>
    /// </remarks>
    public ExecuteOutgoingHttpRequestDelegate? OutgoingHttpRequestHandler { get; set; }

    /// <summary>
    /// Legacy delegate for handling incoming HTTP requests.
    /// <para>
    /// Set this delegate in <see cref="IConnector.Run(CancellationToken)"/> to customize the handling of incoming HTTP requests.
    /// </para>
    /// <para>
    /// If this property is not set, the default eHub implementation will be used for handling HTTP requests.
    /// </para>
    /// </summary>
    [Obsolete("Use IncomingHttpRequestHandler instead.")]
    public HandleIncomingEndpointDelegate? HandleIncomingEndpoint
    {
        get => async (request, endpoint, cancellationToken) => (await ExecuteIncomingHttpRequest(new ConnectorRequest(request, endpoint.ContentType ?? MediaTypeNames.Application.Octet, ""), endpoint, cancellationToken)).ToIResult();
        set => IncomingHttpRequestHandler = value is { } ? new ExecuteIncomingHttpRequestDelegate(async (request, endpoint, cancellationToken) => await HttpUtil.GetConnectorResponseFromIResult(await value(request.Content, endpoint, cancellationToken))) : null;
    }


    /// <summary>
    /// Legacy delegate for handling outgoing HTTP requests.
    /// <para>
    /// Set this delegate in <see cref="IConnector.Run(CancellationToken)"/> to customize the handling of outgoing HTTP requests.
    /// </para>
    /// <para>
    /// If this property is not set, the default eHub implementation will be used for handling HTTP requests.
    /// </para>
    /// </summary>
    [Obsolete("Use OutgoingHttpRequestHandler instead.")]
    public HandleOutgoingEndpointDelegate? HandleOutgoingEndpoint
    {
        get => async (request, endpoint, cancellationToken) => (await ExecuteOutgoingHttpRequest(new ConnectorRequest(request.Content, endpoint.ContentType ?? MediaTypeNames.Application.Octet, ""), endpoint, cancellationToken)).ToHttpConnectorResponseDto();
        set => OutgoingHttpRequestHandler = value is { } ? new ExecuteOutgoingHttpRequestDelegate(async (request, endpoint, cancellationToken) => (await value(request.ToHttpConnectorRequestDto(), endpoint, cancellationToken)).ToConnectorResponse()) : null;
    }

    /// <summary>
    /// Initializes a new empty instance of the <see cref="HttpConnectorOptions"/> class.
    /// </summary>
    public HttpConnectorOptions() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpConnectorOptions"/> class.
    /// <para>
    /// Allows specifying delegates to handle incoming and outgoing HTTP requests.
    /// </para>
    /// </summary>
    /// <param name="incomingHttpRequestHandler">The delegate for handling incoming HTTP requests. Leave <see langword="null"/> to use the default implementation.</param>
    /// <param name="outgoingHttpRequestHandler">The delegate for handling outgoing HTTP requests. Leave <see langword="null"/> to use the default implementation.</param>
    public HttpConnectorOptions(ExecuteIncomingHttpRequestDelegate? incomingHttpRequestHandler = null, ExecuteOutgoingHttpRequestDelegate? outgoingHttpRequestHandler = null)
    {
        IncomingHttpRequestHandler = incomingHttpRequestHandler;
        OutgoingHttpRequestHandler = outgoingHttpRequestHandler;
    }

    /// <summary>
    /// Legacy constructor for <see cref="HttpConnectorOptions"/>. Initializes a new instance of the <see cref="HttpConnectorOptions"/> class.
    /// <para>
    /// Allows specifying delegates to handle incoming and outgoing HTTP requests.
    /// </para>
    /// </summary>
    /// <param name="handleIncomingEndpointDelegate">The delegate for handling incoming HTTP requests. Leave <see langword="null"/> to use the default implementation.</param>
    /// <param name="handleOutgoingEndpointDelegate">The delegate for handling outgoing HTTP requests. Leave <see langword="null"/> to use the default implementation.</param>
    [Obsolete("Use the constructor with ExecuteIncomingHttpRequestDelegate and ExecuteOutgoingHttpRequestDelegate instead.")]
    public HttpConnectorOptions(HandleIncomingEndpointDelegate? handleIncomingEndpointDelegate = null, HandleOutgoingEndpointDelegate? handleOutgoingEndpointDelegate = null)
    {
        HandleIncomingEndpoint = handleIncomingEndpointDelegate;
        HandleOutgoingEndpoint = handleOutgoingEndpointDelegate;
    }

    /// <summary>
    /// Configures a custom handler for executing incoming HTTP requests directed to this connector.
    /// </summary>
    /// <param name="incomingHttpRequestHandler">
    /// A delegate that defines how to handle incoming HTTP requests. The delegate receives the request data and endpoint configuration as parameters.
    /// </param>
    /// <remarks>
    /// Use this method to assign a custom implementation for handling incoming HTTP requests. 
    /// If this method is not called, the default eHub implementation will be used. The default implementation takes the incoming HTTP request's content, headers, 
    /// and path (including query and route parameters) and forwards them via <c>eMessenger.Ask</c> to another connector.
    /// The response from the other connector is then used to generate the HTTP response.
    /// </remarks>
    /// <returns>The current <see cref="HttpConnectorOptions"/> instance.</returns>
    public HttpConnectorOptions SetIncomingHttpRequestHandler(ExecuteIncomingHttpRequestDelegate incomingHttpRequestHandler)
    {
        IncomingHttpRequestHandler = incomingHttpRequestHandler;
        return this;
    }

    /// <summary>
    /// Configures a custom handler for executing outgoing HTTP requests from this connector.
    /// </summary>
    /// <param name="outgoingHttpRequestHandler">
    /// A delegate that defines how to handle outgoing HTTP requests. The delegate receives the request data and endpoint configuration as parameters.
    /// </param>
    /// <remarks>
    /// Use this method to assign a custom implementation for handling outgoing HTTP requests. 
    /// If this method is not called, the default eHub implementation will be used. The default implementation constructs 
    /// an HTTP request using the content, headers, and path (including query and route parameters) provided by the <c>eMessenger.Ask</c> request, 
    /// along with details from the endpoint configuration. It executes the HTTP request and converts the response into a <see cref="ConnectorResponse"/>, 
    /// which is returned to the initiating connector.
    /// </remarks>
    /// <returns>The current <see cref="HttpConnectorOptions"/> instance.</returns>
    public HttpConnectorOptions SetOutgoingHttpRequestHandler(ExecuteOutgoingHttpRequestDelegate outgoingHttpRequestHandler)
    {
        OutgoingHttpRequestHandler = outgoingHttpRequestHandler;
        return this;
    }

    /// <summary>
    /// Executes the incoming HTTP request using the configured <see cref="IncomingHttpRequestHandler"/> delegate.
    /// </summary>
    /// <param name="request">The connector request containing HTTP information, such as content, url and headers, from the incoming HTTP request.</param>
    /// <param name="endpointConfig">Configuration settings specific to the endpoint that matched this incoming HTTP request.</param>
    /// <param name="cancellationToken">A token to monitor for request cancellation.</param>
    /// <returns>A <see cref="ConnectorResponse"/> representing the result of the incoming HTTP request execution.</returns>
    /// <remarks>
    /// This method invokes the <see cref="IncomingHttpRequestHandler"/> to handle the incoming HTTP request. 
    /// Ensure <see cref="SetIncomingHttpRequestHandler"/> is called to assign the handler, or the default eHub implementation will be used.
    /// <para>
    /// <b>Default Implementation</b>: If not set, the default eHub implementation will take the content, path (including query and route parameters), and headers from the incoming HTTP request
    /// and forward them via <c>eMessenger.Ask</c> to another connector. The response from the other connector is then used to respond to the original HTTP request.
    /// </para>
    /// </remarks>
    public ValueTask<ConnectorResponse> ExecuteIncomingHttpRequest(ConnectorRequest request, HttpConnectorEndpointConfig endpointConfig, CancellationToken cancellationToken)
        => IncomingHttpRequestHandler is null
            ? throw new InvalidOperationException("The IncomingHttpRequestHandler delegate is not set.")
            : IncomingHttpRequestHandler(request, endpointConfig, cancellationToken);

    /// <summary>
    /// Executes the outgoing HTTP request using the configured <see cref="OutgoingHttpRequestHandler"/> delegate.
    /// </summary>
    /// <param name="request">The connector request containing the request content for constructing the outgoing HTTP request.</param>
    /// <param name="endpointConfig">Configuration settings specific to the endpoint that matched this outgoing HTTP request and used to construct the HTTP request. Includes the path, method and api, but not the content</param>
    /// <param name="cancellationToken">A token to monitor for request cancellation.</param>
    /// <returns>A <see cref="ConnectorResponse"/> representing the result of executing the outgoing HTTP request.</returns>
    /// <remarks>
    /// This method invokes the <see cref="OutgoingHttpRequestHandler"/> to handle the outgoing HTTP request. 
    /// Ensure <see cref="SetOutgoingHttpRequestHandler"/> is called to assign the handler, or the default eHub implementation will be used.
    /// <para>
    /// <b>Default Implementation</b>: If not set, the default eHub implementation will use the content, path (including query and route parameters), and headers from an <c>eMessenger.Ask</c> request,
    /// along with the endpoint configuration details, to construct and execute an HTTP request. The response is converted into a <see cref="ConnectorResponse"/> and returned to the initiating connector through the <c>eMessenger.AnswerAsync</c>.
    /// </para>
    /// </remarks>
    public ValueTask<ConnectorResponse> ExecuteOutgoingHttpRequest(ConnectorRequest request, HttpConnectorEndpointConfig endpointConfig, CancellationToken cancellationToken)
        => OutgoingHttpRequestHandler is null
            ? throw new InvalidOperationException("The OutgoingHttpRequestHandler delegate is not set.")
            : OutgoingHttpRequestHandler(request, endpointConfig, cancellationToken);
}

/// <summary>
/// Represents a delegate for executing an incoming HTTP request directed to a connector.
/// </summary>
/// <param name="request">The incoming connector request containing HTTP information, such as the content.</param>
/// <param name="endpointConfig">The configurations specific to the endpoint for this HTTP request.</param>
/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
/// <returns>A <see cref="ConnectorResponse"/> representing the response from executing the incoming HTTP request.</returns>
public delegate ValueTask<ConnectorResponse> ExecuteIncomingHttpRequestDelegate(ConnectorRequest request, HttpConnectorEndpointConfig endpointConfig, CancellationToken cancellationToken);

/// <summary>
/// Represents a delegate for executing an outgoing HTTP request, based on content from the connector request and configuration from the endpoint.
/// </summary>
/// <param name="request">The connector request containing the content for the outgoing HTTP call.</param>
/// <param name="endpointConfig">The endpoint configuration used to construct and execute the HTTP request.</param>
/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
/// <returns>A <see cref="ConnectorResponse"/> representing the response received from the HTTP request execution.</returns>
public delegate ValueTask<ConnectorResponse> ExecuteOutgoingHttpRequestDelegate(ConnectorRequest request, HttpConnectorEndpointConfig endpointConfig, CancellationToken cancellationToken);

/// <summary>
/// Represents the legacy delegate for handling incoming HTTP requests.
/// </summary>
/// <param name="data">The incoming data as a read-only memory block.</param>
/// <param name="endpointConfig">The configuration for the endpoint.</param>
/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
[Obsolete("Use ExecuteIncomingHttpRequestDelegate instead.")]
public delegate ValueTask<IResult> HandleIncomingEndpointDelegate(ReadOnlyMemory<byte> data, HttpConnectorEndpointConfig endpointConfig, CancellationToken cancellationToken);

/// <summary>
/// Represents the legacy delegate for handling outgoing HTTP requests.
/// </summary>
/// <param name="request">The request data for the outgoing HTTP request.</param>
/// <param name="endpointConfig">The configuration for the endpoint.</param>
/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
[Obsolete("Use ExecuteOutgoingHttpRequestDelegate instead.")]
public delegate ValueTask<HttpConnectorResponseDto> HandleOutgoingEndpointDelegate(HttpConnectorRequestDto request, HttpConnectorEndpointConfig endpointConfig, CancellationToken cancellationToken);
