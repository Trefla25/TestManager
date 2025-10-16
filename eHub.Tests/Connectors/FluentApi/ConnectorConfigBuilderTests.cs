using eHub.Config;
using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace eHub.Tests.Connectors.FluentApi;

[TestClass]
public class ConnectorConfigBuilderTests
{
    private ConnectorConfigBuilder _builder = new(new ConfigurationBuilder().Build());

    [TestMethod]
    public void Configuration_OnGet_ReturnsSameConfigurations()
    {
        var cconfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>()
            {
                { "SomeKey", "Value" },
                { "Nested:Config:Section", "SomeValue" },
                { "SomeOtherKey:Value", "OtherValue" },
            })
            .Build();

        _builder = new ConnectorConfigBuilder(cconfiguration);

        _builder.Configuration["SomeKey"].Should().Be(cconfiguration["SomeKey"]);
        _builder.Configuration["Nested:Config:Section"].Should().Be(cconfiguration["Nested:Config:Section"]);
        _builder.Configuration.GetSection("SomeOtherKey")["Value"].Should().Be(cconfiguration.GetSection("SomeOtherKey")["Value"]);
    }

    [TestMethod]
    public void ConfigureHttpIncoming_WhenCalled_ConfiguresHttpIncoming()
    {
        var configured = false;
        _builder.ConfigureHttpIncoming(options => configured = true);

        var config = _builder.BuildConfiguration();
        config.Should().NotBeNull();

        var httpIncoming = config.GetSection("HttpIncoming").Get<HttpIncoming>();
        httpIncoming.Should().NotBeNull();
        configured.Should().BeTrue();
    }

    [TestMethod]
    public void ConfigureHttpOutgoing_WhenCalled_ConfiguresHttpOutgoing()
    {
        var configured = false;
        _builder.ConfigureHttpOutgoing(options => configured = true);

        var config = _builder.BuildConfiguration();
        config.Should().NotBeNull();

        var httpOutgoing = config.GetSection("HttpOutgoing").Get<HttpOutgoing>();
        httpOutgoing.Should().NotBeNull();
        configured.Should().BeTrue();
    }

    [TestMethod]
    public void ConfigurePacketTransfer_WhenCalled_ConfiguresPacketTransfer()
    {
        var configured = false;
        _builder.ConfigurePacketTransfer(options => configured = true);

        var config = _builder.BuildConfiguration();
        config.Should().NotBeNull();

        var packetTransfer = config.GetSection("PacketTransfer").Get<Config.PacketTransfer>();
        packetTransfer.Should().NotBeNull();
        configured.Should().BeTrue();
    }

    [TestMethod]
    public void SetConfigValueRaw_OnSimpleProperty_SetsProperty()
    {
        _builder.SetRawConfigValue("Enabled", true);

        var config = _builder.BuildConfiguration();

        var template = config.Get<ConnectorTemplate>();
        template.Should().NotBeNull();
        template.Enabled.Should().BeTrue();
    }

    [TestMethod]
    public void SetConfigValueRaw_OnNestedProperty_SetsProperty()
    {
        _builder.SetRawConfigValue("PacketTransfer:DbPath", "C:\\Test\\db.sqlite");

        var config = _builder.BuildConfiguration();

        var packetTransfer = config.GetSection("PacketTransfer").Get<Config.PacketTransfer>();
        packetTransfer.Should().NotBeNull();
        packetTransfer.DbPath.Should().Be("C:\\Test\\db.sqlite");
    }

    [TestMethod]
    public void SetConfigValueRaw_OnDictionaryProperty_SetsProperty()
    {
       _builder.SetRawConfigValue("HttpIncoming:Endpoints:Endpoint1:Path", "/api/test");

        var config = _builder.BuildConfiguration();

        var httpIncoming = config.GetSection("HttpIncoming").Get<HttpIncoming>();
        httpIncoming.Should().NotBeNull();
        httpIncoming.Endpoints.Should().ContainKey("Endpoint1");
        httpIncoming.Endpoints["Endpoint1"].Path.Should().Be("/api/test");
    }

    [TestMethod]
    public void SetConfigValueRaw_OnArrayItems_SetsArrayItems()
    {
        _builder.SetRawConfigValue("PacketTransfer:ChannelGroups:Incoming:Channels:0", "A");
        _builder.SetRawConfigValue("PacketTransfer:ChannelGroups:Incoming:Channels:1", "B");

        var config = _builder.BuildConfiguration();

        var packetTransfer = config.GetSection("PacketTransfer").Get<Config.PacketTransfer>();
        packetTransfer.Should().NotBeNull();
        packetTransfer.ChannelGroups.Should().ContainKey("Incoming");
        packetTransfer.ChannelGroups["Incoming"].Channels.Should().BeEquivalentTo(["A", "B"]);  
    }

    [TestMethod]
    public void SetConfigValueRaw_WhenCalledMultipleTimes_KeepsLast()
    {
        _builder.SetRawConfigValue("Enabled", "true");
        _builder.SetRawConfigValue("Enabled", "false");

        var result = _builder.BuildConfiguration();
        result.Should().NotBeNull();

        result["Enabled"].Should().Be("false");
    }

    [TestMethod]
    public void Build_WhenAllMethodsChained_ConfiguresAllProperties()
    {
        _builder
            .ConfigureHttpIncoming(options => { })
            .ConfigureHttpOutgoing(options => { })
            .ConfigurePacketTransfer(options =>
            {
                options.SetDbPath("C:\\Test\\db.sqlite");
            })
            .SetRawConfigValue("PacketTransfer:DbPath", "C:\\Test\\new_db.sqlite");

        var config = _builder.BuildConfiguration();
        config.Should().NotBeNull();

        var template = config.Get<ConnectorTemplate>();
        template.Should().NotBeNull();

        var packetTransfer = config.GetSection("PacketTransfer").Get<Config.PacketTransfer>();
        packetTransfer.Should().NotBeNull();
        packetTransfer.DbPath.Should().Be("C:\\Test\\new_db.sqlite");

        var httpIncoming = config.GetSection("HttpIncoming").Get<HttpIncoming>();
        httpIncoming.Should().NotBeNull();

        var httpOutgoing = config.GetSection("HttpOutgoing").Get<HttpOutgoing>();
        httpOutgoing.Should().NotBeNull();
    }
}
