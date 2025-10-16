using System.Text;
using System.Text.Json;
using eHub.Config;
using eHub.PlugIn.Configuration;

namespace eHub.Scripting.Connectors.Configuration;

internal class ConnectorConfigBuilder(IConfiguration configuration) : IConnectorConfigBuilder
{
    private readonly IConfiguration _configuration = configuration;
    private readonly Dictionary<string, string?> _rawKeyValues = [];
    private readonly ConnectorTemplate _connectorTemplate = new();

    public KestrelBuilder? KestrelBuilder { get; private set; }
    public List<AuthenticationSchemeConfig> Schemes { get; private set; } = [];
    public IConfiguration Configuration => _configuration;

    public IConnectorConfigBuilder ConfigureHttpIncoming(Action<IHttpIncomingBuilder> configure)
    {
        var httpIncomingBuilder = new HttpIncomingBuilder();
        configure(httpIncomingBuilder);
        _connectorTemplate.HttpIncoming = httpIncomingBuilder.Build();
        KestrelBuilder = _connectorTemplate.HttpIncoming.KestrelBuilder;
        Schemes = _connectorTemplate.HttpIncoming.Auth?.Schemes ?? [];

        return this;
    }

    public IConnectorConfigBuilder ConfigureHttpOutgoing(Action<IHttpOutgoingBuilder> configure)
    {
        var httpOutgoingBuilder = new HttpOutgoingBuilder();
        configure(httpOutgoingBuilder);
        _connectorTemplate.HttpOutgoing = httpOutgoingBuilder.Build();

        return this;
    }

    public IConnectorConfigBuilder ConfigurePacketTransfer(Action<IPacketTransferBuilder> configure)
    {
        var packetTransferBuilder = new PacketTransferBuilder();
        configure(packetTransferBuilder);
        _connectorTemplate.PacketTransfer = packetTransferBuilder.Build();

        return this;
    }

    public IConnectorConfigBuilder SetRawConfigValue<T>(string key, T? value)
    {
        _rawKeyValues[key] = value?.ToString();
        return this;
    }

    public IConfiguration BuildConfiguration()
    {
        var json = JsonSerializer.Serialize(_connectorTemplate);
        using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));


        var configuration = new ConfigurationBuilder()
            .AddJsonStream(memoryStream)
            .AddInMemoryCollection(_rawKeyValues)
            .Build();

        return configuration;
    }
}

