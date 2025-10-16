using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using eHub.PlugIn.Communication;

namespace eHub.PlugIn;

/// <summary>
/// Implement this interface in a public class to expose an HTTP connector with packet transfer tools.<br/>
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
public interface IHttpPacketTransfer : IHttpConnector, IPacketTransfer
{
    /// <summary>
    /// Gets or sets the converter used for transforming HTTP packets.
    /// </summary>
    public HttpPacketTransferConverter HttpPacketConverter { get; set; }

    /// <summary>
    /// Gets or sets the HTTP Packet Transfer connector options.
    /// <para>
    /// Contains options for overriding the default implementations of the HTTP Packet Transfer connector.
    /// </para>
    /// </summary>
    HttpPacketTransferOptions HttpPacketTransferOptions { get; }

    /// <summary>
    /// Do not implement or override this property. 
    /// This provides a default implementation using <see cref="HttpPacketTransferOptions"/>.
    /// </summary>
    HttpConnectorOptions IHttpConnector.HttpConnectorOptions => HttpPacketTransferOptions;

    /// <summary>
    /// Do not implement or override this property. 
    /// This provides a default implementation using <see cref="HttpPacketConverter"/>.
    /// </summary>
    IPacketConverter IPacketTransfer.Converter => HttpPacketConverter ?? new HttpPacketTransferConverter();

    /// <summary>
    /// Do not implement or override this method.
    /// This provides a default implementation that processes the packet using <see cref="HttpPacketTransferOptions.ProcessPacket"/>.
    /// </summary>
    /// <param name="packet">The packet data to be processed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask<ProcessPacketState> IPacketTransfer.ProcessPacket(PacketData packet, CancellationToken cancellationToken)
        => HttpPacketTransferOptions.ProcessPacket(packet, cancellationToken);
}

/// <summary>
/// Options for overriding the default implementations of the HTTP Packet Transfer connector.
/// </summary>
public class HttpPacketTransferOptions : HttpConnectorOptions
{
    /// <summary>
    /// Gets or sets the channel name for incoming packets.
    /// </summary>
    public string? IncomingChannel { get; set; }

    /// <summary>
    /// Gets or sets the channel name for outgoing packets.
    /// </summary>
    public string? OutgoingChannel { get; set; }

    /// <summary>
    /// Gets the delegate responsible for processing a packet within this connector.
    /// </summary>
    /// <remarks>
    /// Set this delegate using <see cref="SetPacketProcessingHandler"/> in <see cref="IConnector.Run"/> to customize the processing of packets within this connector.
    /// If this property is not set, the default eHub implementation will be used.
    /// </remarks>
    public ProcessPacketDelegate? PacketProcessingHandler { get; private set; }

    /// <summary>
    /// Gets the delegate responsible for processing incoming packets directed to this connector.
    /// </summary>
    /// <remarks>
    /// Set this delegate using <see cref="SetIncomingPacketProcessingHandler"/> in <see cref="IConnector.Run"/> to customize the processing of incoming packets.
    /// If this property is not set, the default eHub implementation will be used.
    /// <para>
    /// <b>Default Implementation</b>: If not set, the default eHub implementation converts the incoming packet’s binary data and metadata into a <see cref="ConnectorRequest"/> and an <see cref="HttpConnectorEndpointConfig"/>,
    /// and calls <see cref="HttpConnectorOptions.ExecuteIncomingHttpRequest"/> to execute the request and return the response.
    /// </para>
    /// </remarks>
    public ProcessIncomingPacketDelegate? IncomingPacketProcessingHandler { get; private set; }

    /// <summary>
    /// Gets the delegate responsible for processing outgoing packets from this connector.
    /// </summary>
    /// <remarks>
    /// Set this delegate using <see cref="SetOutgoingPacketProcessingHandler"/> in <see cref="IConnector.Run"/> to customize the processing of outgoing packets.
    /// If this property is not set, the default eHub implementation will be used.
    /// <para>
    /// <b>Default Implementation</b>: If not set, the default eHub implementation converts the packet’s binary data and metadata into a <see cref="ConnectorRequest"/> and <see cref="HttpConnectorEndpointConfig"/>,
    /// then calls <see cref="HttpConnectorOptions.ExecuteOutgoingHttpRequest"/> to execute the request and return the response.
    /// </para>
    /// </remarks>
    public ProcessOutgoingPacketDelegate? OutgoingPacketProcessingHandler { get; private set; }

    /// <summary>
    /// Legacy delegate for processing incoming HTTP packets.
    /// <para>
    /// This delegate will be invoked by <see cref="HandleProcessPacket"/> when <see cref="PacketData.Channel"/> equals <see cref="IncomingChannel"/>, 
    /// as well as directly by any incoming HTTP requests.
    /// </para>
    /// <para>
    /// Set this delegate in <see cref="IConnector.Run(CancellationToken)"/> to customize the handling of incoming HTTP packets.
    /// If this property is not set, the default eHub implementation will be used.
    /// </para>
    /// </summary>
    [Obsolete("Use IncomingPacketProcessingHandler to get the delegate, SetIncomingPacketProcessingHandler to override the default handler and ProcessIncomingPacket to call it.")]
    public HandleProcessIncomingPacketDelegate? HandleProcessIncomingPacket
    {
        get => async (packet, cancellationToken) => (await ProcessIncomingPacket(packet, cancellationToken)).ToIResult();
        set => IncomingPacketProcessingHandler = value is { } ? new ProcessIncomingPacketDelegate(async (packet, cancellationToken) => await HttpUtil.GetConnectorResponseFromIResult(await value(packet, cancellationToken))) : null;
    }

    /// <summary>
    /// Legacy delegate for processing outgoing HTTP packets.
    /// <para>
    /// This delegate will be invoked by <see cref="HandleProcessPacket"/> when <see cref="PacketData.Channel"/> equals <see cref="OutgoingChannel"/>, 
    /// as well as directly by any outgoing HTTP requests.
    /// </para>
    /// <para>
    /// Set this delegate in <see cref="IConnector.Run(CancellationToken)"/> to customize the handling of outgoing HTTP packets.
    /// If this property is not set, the default eHub implementation will be used.
    /// </para>
    /// </summary>
    [Obsolete("Use OutgoingPacketProcessingHandler to get the delegate, SetOutgoingPacketProcessingHandler to override the default handler and ProcessOutgoingPacket to call it.")]
    public HandleProcessOutgoingPacketDelegate? HandleProcessOutgoingPacket
    {
        get => async (packet, cancellationToken) => (await ProcessOutgoingPacket(packet, cancellationToken)).ToHttpConnectorResponseDto();
        set => OutgoingPacketProcessingHandler = value is { } ? new ProcessOutgoingPacketDelegate(async (packet, cancellationToken) => (await value(packet, cancellationToken)).ToConnectorResponse()) : null;
    }

    /// <summary>
    /// Legacy delegate for processing packets.
    /// <para>
    /// This delegate will be invoked by <see cref="IPacketTransfer.ProcessPacket(PacketData, CancellationToken)"/>.
    /// </para>
    /// <para>
    /// Set this delegate in <see cref="IConnector.Run(CancellationToken)"/> to customize the handling of packets.
    /// If this property is not set, the default eHub implementation will be used.
    /// </para>
    /// </summary>
    [Obsolete("Use PacketProcessingHandler to get the delegate, SetPacketProcessingHandler to override the default handler and ProcessPacket to call it.")]
    public HandleProcessPacketDelegate? HandleProcessPacket
    {
        get => ProcessPacket;
        set => PacketProcessingHandler = value is { } ? new ProcessPacketDelegate((packet, cancellationToken) => value(packet, cancellationToken)) : null;
    }

    /// <summary>
    /// Initializes a new empty instance of the <see cref="HttpPacketTransferOptions"/> class.
    /// </summary>
    public HttpPacketTransferOptions() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpPacketTransferOptions"/> class.
    /// </summary>
    /// <param name="incomingChannel"></param>
    /// <param name="outgoingChanel"></param>
    public HttpPacketTransferOptions(string? incomingChannel, string? outgoingChanel)
    {
        IncomingChannel = incomingChannel;
        OutgoingChannel = outgoingChanel;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpPacketTransferOptions"/> class.
    /// <para>
    /// Allows specifying delegates to handle incoming and outgoing HTTP requests.
    /// </para>
    /// </summary>
    /// <param name="incomingChannel">The channel name used for storing incoming HTTP requests. Leave <see langword="null"/> to use the default value.</param>
    /// <param name="outgoingChanel">The channel name used for storing outgoing HTTP requests. Leave <see langword="null"/> to use the default value.</param>
    /// <param name="incomingHttpRequestHandler">The delegate for handling incoming HTTP requests. Leave <see langword="null"/> to use the default implementation.</param>
    /// <param name="outgoingHttpRequestHandler">The delegate for handling outgoing HTTP requests. Leave <see langword="null"/> to use the default implementation.</param>
    /// <param name="processIncomingPacketDelegate">The delegate for handling incoming packets. Leave <see langword="null"/> to use the default implementation.</param>
    /// <param name="processOutgoingPacketDelegate">The delegate for handling outgoing packets. Leave <see langword="null"/> to use the default implementation.</param>
    /// <param name="processPacketDelegate">The delegate for handling the processing of packets. Leave <see langword="null"/> to use the default implementation.</param>
    public HttpPacketTransferOptions(
        string? incomingChannel = null,
        string? outgoingChanel = null,
        ExecuteIncomingHttpRequestDelegate? incomingHttpRequestHandler = null,
        ExecuteOutgoingHttpRequestDelegate? outgoingHttpRequestHandler = null,
        ProcessIncomingPacketDelegate? processIncomingPacketDelegate = null,
        ProcessOutgoingPacketDelegate? processOutgoingPacketDelegate = null,
        ProcessPacketDelegate? processPacketDelegate = null)
        : base(incomingHttpRequestHandler, outgoingHttpRequestHandler)
    {
        IncomingChannel = incomingChannel;
        OutgoingChannel = outgoingChanel;
        IncomingPacketProcessingHandler = processIncomingPacketDelegate;
        OutgoingPacketProcessingHandler = processOutgoingPacketDelegate;
        PacketProcessingHandler = processPacketDelegate;
    }

    /// <summary>
    /// Legacy constructor for <see cref="HttpPacketTransferOptions"/>. Initializes a new instance of the <see cref="HttpPacketTransferOptions"/> class.
    /// <para>
    /// Allows specifying options to handle incoming and outgoing HTTP requests.
    /// </para>
    /// </summary>
    /// <param name="incomingChannel">The channel name used for storing incoming HTTP requests. Leave <see langword="null"/> to use the default value.</param>
    /// <param name="outgoingChanel">The channel name used for storing outgoing HTTP requests. Leave <see langword="null"/> to use the default value.</param>
    /// <param name="handleIncomingEndpointDelegate">The delegate for handling incoming HTTP requests. Leave <see langword="null"/> to use the default implementation.</param>
    /// <param name="handleOutgoingEndpointDelegate">The delegate for handling outgoing HTTP requests. Leave <see langword="null"/> to use the default implementation.</param>
    /// <param name="handleProcessIncomingPacketDelegate">The delegate for handling incoming HTTP Packets. Leave <see langword="null"/> to use the default implementation.</param>
    /// <param name="handleProcessOutgoingPacketDelegate">The delegate for handling outgoing HTTP Packets. Leave <see langword="null"/> to use the default implementation.</param>
    /// <param name="handleProcessPacketDelegate">The delegate for handling the processing of HTTP Packets. Leave <see langword="null"/> to use the default implementation.</param>
    [Obsolete("Use the constructor with ProcessPacketDelegate, ProcessIncomingPacketDelegate and ProcessOutgoingPacketDelegate instead.")]
    public HttpPacketTransferOptions(
        string? incomingChannel = null,
        string? outgoingChanel = null,
        HandleIncomingEndpointDelegate? handleIncomingEndpointDelegate = null,
        HandleOutgoingEndpointDelegate? handleOutgoingEndpointDelegate = null,
        HandleProcessIncomingPacketDelegate? handleProcessIncomingPacketDelegate = null,
        HandleProcessOutgoingPacketDelegate? handleProcessOutgoingPacketDelegate = null,
        HandleProcessPacketDelegate? handleProcessPacketDelegate = null
        ) : base(handleIncomingEndpointDelegate, handleOutgoingEndpointDelegate)
    {
        IncomingChannel = incomingChannel;
        OutgoingChannel = outgoingChanel;
        HandleProcessIncomingPacket = handleProcessIncomingPacketDelegate;
        HandleProcessOutgoingPacket = handleProcessOutgoingPacketDelegate;
        HandleProcessPacket = handleProcessPacketDelegate;
    }

    /// <summary>
    /// Configures a custom handler for processing packets within this connector.
    /// </summary>
    /// <param name="packetProcessingHandler">
    /// A delegate that defines how packets should be processed. The delegate receives the packet data and a cancellation token as parameters.
    /// </param>
    /// <remarks>
    /// Use this method to assign a custom implementation for packet processing. 
    /// If this method is not called, the default eHub implementation will be used. The default behavior checks the <see cref="PacketData.Channel"/> 
    /// of the incoming packet. If the channel matches <see cref="IncomingChannel"/>, the packet is processed as an incoming packet; 
    /// if it matches <see cref="OutgoingChannel"/>, it is processed as an outgoing packet.
    /// </remarks>
    /// <returns>The current <see cref="HttpPacketTransferOptions"/> instance.</returns>
    public HttpPacketTransferOptions SetPacketProcessingHandler(ProcessPacketDelegate packetProcessingHandler)
    {
        PacketProcessingHandler = packetProcessingHandler;
        return this;
    }

    /// <summary>
    /// Configures a custom handler for processing incoming packets directed to this connector.
    /// </summary>
    /// <param name="incomingPacketProcessingHandler">
    /// A delegate that defines how to process incoming packets. The delegate receives the packet data and a cancellation token as parameters.
    /// </param>
    /// <remarks>
    /// Use this method to assign a custom implementation for handling incoming packets. 
    /// If this method is not called, the default eHub implementation will be used. The default behavior converts the incoming packet’s binary data and metadata 
    /// into a <see cref="ConnectorRequest"/> and an <see cref="HttpConnectorEndpointConfig"/>, then calls <see cref="HttpConnectorOptions.ExecuteIncomingHttpRequest"/> 
    /// to handle the request and return the response.
    /// </remarks>
    /// <returns>The current <see cref="HttpPacketTransferOptions"/> instance.</returns>
    public HttpPacketTransferOptions SetIncomingPacketProcessingHandler(ProcessIncomingPacketDelegate incomingPacketProcessingHandler)
    {
        IncomingPacketProcessingHandler = incomingPacketProcessingHandler;
        return this;
    }

    /// <summary>
    /// Configures a custom handler for processing outgoing packets from this connector.
    /// </summary>
    /// <param name="outgoingPacketProcessingHandler">
    /// A delegate that defines how to process outgoing packets. The delegate receives the packet data and a cancellation token as parameters.
    /// </param>
    /// <remarks>
    /// Use this method to assign a custom implementation for handling outgoing packets. 
    /// If this method is not called, the default eHub implementation will be used. The default behavior converts the packet’s binary data and metadata 
    /// into a <see cref="ConnectorRequest"/> and <see cref="HttpConnectorEndpointConfig"/>, then calls <see cref="HttpConnectorOptions.ExecuteOutgoingHttpRequest"/> 
    /// to handle the request and return the response.
    /// </remarks>
    /// <returns>The current <see cref="HttpPacketTransferOptions"/> instance.</returns>
    public HttpPacketTransferOptions SetOutgoingPacketProcessingHandler(ProcessOutgoingPacketDelegate outgoingPacketProcessingHandler)
    {
        OutgoingPacketProcessingHandler = outgoingPacketProcessingHandler;
        return this;
    }

    /// <summary>
    /// Processes a packet by invoking the configured <see cref="PacketProcessingHandler"/> delegate.
    /// </summary>
    /// <param name="packet">The packet to be processed.</param>
    /// <param name="cancellationToken">A token to monitor for request cancellation.</param>
    /// <returns>A <see cref="ProcessPacketState"/> representing the result of the packet processing.</returns>
    /// <remarks>
    /// This method invokes the <see cref="PacketProcessingHandler"/> to process the packet. 
    /// Ensure <see cref="SetPacketProcessingHandler"/> is called to assign the handler, or the default eHub implementation will be used.
    /// <para>
    /// <b>Default Implementation</b>: If the <see cref="PacketProcessingHandler"/> is not set, the default eHub implementation checks the channel of <paramref name="packet"/>.
    /// If the <see cref="PacketData.Channel"/> is equal to <see cref="IncomingChannel"/>, it calls <see cref="ProcessIncomingPacket"/>.
    /// If the <see cref="PacketData.Channel"/> is equal to <see cref="OutgoingChannel"/>, it calls <see cref="ProcessOutgoingPacket"/>.
    /// </para>
    /// </remarks>
    public ValueTask<ProcessPacketState> ProcessPacket(PacketData packet, CancellationToken cancellationToken)
        => PacketProcessingHandler is null
            ? throw new InvalidOperationException("The PacketProcessingHandler delegate is not set.")
            : PacketProcessingHandler(packet, cancellationToken);

    /// <summary>
    /// Processes an incoming packet by invoking the configured <see cref="IncomingPacketProcessingHandler"/> delegate.
    /// </summary>
    /// <param name="packet">The incoming packet to be processed.</param>
    /// <param name="cancellationToken">A token to monitor for request cancellation.</param>
    /// <returns>A <see cref="ConnectorResponse"/> representing the result of processing the incoming packet.</returns>
    /// <remarks>
    /// This method invokes the <see cref="IncomingPacketProcessingHandler"/> to process the incoming packet. 
    /// Ensure <see cref="SetIncomingPacketProcessingHandler"/> is called to assign the handler, or the default eHub implementation will be used.
    /// <para>
    /// <b>Default Implementation</b>: If not set, the default eHub implementation converts the incoming packet’s binary data and metadata into a <see cref="ConnectorRequest"/> and an <see cref="HttpConnectorEndpointConfig"/>,
    /// and calls <see cref="HttpConnectorOptions.ExecuteIncomingHttpRequest"/> to execute the request and return the response.
    /// </para>
    /// </remarks>
    public ValueTask<ConnectorResponse> ProcessIncomingPacket(PacketData packet, CancellationToken cancellationToken)
        => IncomingPacketProcessingHandler is null
            ? throw new InvalidOperationException("The IncomingPacketProcessingHandler delegate is not set.")
            : IncomingPacketProcessingHandler(packet, cancellationToken);

    /// <summary>
    /// Processes an outgoing packet by invoking the configured <see cref="OutgoingPacketProcessingHandler"/> delegate.
    /// </summary>
    /// <param name="packet">The outgoing packet to be processed.</param>
    /// <param name="cancellationToken">A token to monitor for request cancellation.</param>
    /// <returns>A <see cref="ConnectorResponse"/> representing the result of processing the outgoing packet.</returns>
    /// <remarks>
    /// This method invokes the <see cref="OutgoingPacketProcessingHandler"/> to process the outgoing packet. 
    /// Ensure <see cref="SetOutgoingPacketProcessingHandler"/> is called to assign the handler, or the default eHub implementation will be used.
    /// <para>
    /// <b>Default Implementation</b>: If not set, the default eHub implementation converts the packet’s binary data and metadata into a <see cref="ConnectorRequest"/> and <see cref="HttpConnectorEndpointConfig"/>,
    /// then calls <see cref="HttpConnectorOptions.ExecuteOutgoingHttpRequest"/> to execute the request and return the response.
    /// </para>
    /// </remarks>
    public ValueTask<ConnectorResponse> ProcessOutgoingPacket(PacketData packet, CancellationToken cancellationToken)
        => OutgoingPacketProcessingHandler is null
            ? throw new InvalidOperationException("The OutgoingPacketProcessingHandler delegate is not set.")
            : OutgoingPacketProcessingHandler(packet, cancellationToken);
}

/// <summary>
/// Represents a delegate for processing a packet within the connector.
/// </summary>
/// <param name="packet">The packet to be processed.</param>
/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
/// <returns>A <see cref="ProcessPacketState"/> representing the result of the packet processing.</returns>
public delegate ValueTask<ProcessPacketState> ProcessPacketDelegate(PacketData packet, CancellationToken cancellationToken);

/// <summary>
/// Represents a delegate for processing an incoming packet within the connector.
/// </summary>
/// <param name="packet">The incoming packet to be processed.</param>
/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
/// <returns>A <see cref="ConnectorResponse"/> representing the result of processing the incoming packet.</returns>
public delegate ValueTask<ConnectorResponse> ProcessIncomingPacketDelegate(PacketData packet, CancellationToken cancellationToken);

/// <summary>
/// Represents a delegate for processing an outgoing packet from the connector.
/// </summary>
/// <param name="packet">The outgoing packet to be processed.</param>
/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
/// <returns>A <see cref="ConnectorResponse"/> representing the result of processing the outgoing packet.</returns>
public delegate ValueTask<ConnectorResponse> ProcessOutgoingPacketDelegate(PacketData packet, CancellationToken cancellationToken);

/// <summary>
/// Represents the legacy delegate for handling incoming HTTP packets.
/// </summary>
/// <param name="packet">The packet data to be processed.</param>
/// <param name="cancellationToken">The cancellation token.</param>
[Obsolete("Use ProcessIncomingPacketDelegate instead.")]
public delegate ValueTask<IResult> HandleProcessIncomingPacketDelegate(PacketData packet, CancellationToken cancellationToken);

/// <summary>
/// Represents the legacy delegate for handling outgoing HTTP packets.
/// </summary>
/// <param name="packet">The packet data to be processed.</param>
/// <param name="cancellationToken">The cancellation token.</param>
[Obsolete("Use ProcessOutgoingPacketDelegate instead.")]
public delegate ValueTask<HttpConnectorResponseDto> HandleProcessOutgoingPacketDelegate(PacketData packet, CancellationToken cancellationToken);

/// <summary>
/// Represents the legacy delegate for handling the processing of packets.
/// </summary>
/// <param name="packet">The packet data to be processed.</param>
/// <param name="cancellationToken">The cancellation token.</param>
[Obsolete("Use ProcessPacketDelegate instead.")]
public delegate ValueTask<ProcessPacketState> HandleProcessPacketDelegate(PacketData packet, CancellationToken cancellationToken);
