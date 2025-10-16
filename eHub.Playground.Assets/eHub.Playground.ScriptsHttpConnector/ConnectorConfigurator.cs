using eHub.PlugIn;
using eHub.PlugIn.Configuration;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace eHub.Playground.ScriptsHttpConnector;
public class ConnectorConfigurator : IHttpPacketTransfer, IConnectorConfigurator
{
    public HttpPacketTransferConverter HttpPacketConverter { get; set; } = new();

    public HttpPacketTransferOptions HttpPacketTransferOptions { get; set; } = new();

    public IPacketRepository PacketRepository { get; set; } = default!;

    public event UpdateStatusDelegate? UpdateStatus;

    public void HttpSetup(IEndpointRouteBuilder routeBuilder) { }

    public Task Run(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    class Incoming
    {
        public string Url { get; set; }
    }

    public static void ConfigureDefaults(IConnectorConfigBuilder builder)
    {
        builder.ConfigurePacketTransfer(o => o
            .AddChannelGroup("Incoming", group => group.AddChannel("Incoming"))
            .AddChannelGroup("Outgoing", group => group.AddChannel("Outgoing"))
            .AddChannelGroup("Errors", group => group
                .AddChannel("Incoming:Error")
                .AddChannel("Outgoing:Error")));

        var incomingUrl = builder.Configuration["UrlIncoming"];
        var outgoingUrl = builder.Configuration["UrlOutgoing"];
        var incoming = builder.Configuration.GetSection("Incoming").Get<Incoming>();

        builder.ConfigureHttpIncoming(o => o
            .ConfigureKestrel(k => k
                .Listen(incomingUrl)
                .ConfigureServerLimits(l => l.MaxRequestBodySize = 300_000_000))
            .AddEndpoint("TestEndpoint", new HttpConnectorEndpointConfig
            {
                Path = "/api/test",
                HttpMethod = "POST",
                Topic = "TestTopic",
                ContentTypes = ["application/json", "plain/text"]
            }));

        builder.ConfigureHttpOutgoing(o => o
            .AddApi("Host", a => a
                .SetBaseAddress(outgoingUrl)
                .UseBasicAuthorization("api", "api"))
            .AddEndpoint("TestEndpoint", e => e
                .UseApi("Host")
                .SetTopic("TestTopic")
                .SetPath("/test")
                .SetHttpMethod("POST")));
    }
}
