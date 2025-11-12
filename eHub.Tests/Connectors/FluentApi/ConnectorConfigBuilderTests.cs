using eHub.Config;
using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace eHub.Tests.Connectors.FluentApi;

public class ConnectorConfigBuilderTests
{
    private ConnectorConfigBuilder _builder = new(new ConfigurationBuilder().Build());

    [Fact]
    public void Configuration_OnGet_ReturnsSameConfigurations()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>()
            {
                { "SomeKey", "Value" },
                { "Nested:Config:Section", "SomeValue" },
                { "SomeOtherKey:Value", "OtherValue" },
            })
            .Build();
        
        // Act
        _builder = new ConnectorConfigBuilder(configuration);
        
        // Assert
        _builder.Configuration["SomeKey"].Should().Be(configuration["SomeKey"]);
        _builder.Configuration["Nested:Config:Section"].Should().Be(configuration["Nested:Config:Section"]);
        _builder.Configuration.GetSection("SomeOtherKey")["Value"].Should().Be(configuration.GetSection("SomeOtherKey")["Value"]);
    }

    [Fact]
    public void ConfigureHttpIncoming_WhenCalled_ConfiguresHttpIncoming()
    {
        // Arrange
        var configured = false;
        _builder.ConfigureHttpIncoming(_ => configured = true);
        
        // Act
        var config = _builder.BuildConfiguration();
        
        // Assert
        config.Should().NotBeNull();

        var httpIncoming = config.GetSection("HttpIncoming").Get<HttpIncoming>();
        httpIncoming.Should().NotBeNull();
        configured.Should().BeTrue();
    }

    [Fact]
    public void ConfigureHttpOutgoing_WhenCalled_ConfiguresHttpOutgoing()
    {
        // Arrange
        var configured = false;
        _builder.ConfigureHttpOutgoing(_ => configured = true);
        
        // Act
        var config = _builder.BuildConfiguration();
        
        // Assert
        config.Should().NotBeNull();

        var httpOutgoing = config.GetSection("HttpOutgoing").Get<HttpOutgoing>();
        httpOutgoing.Should().NotBeNull();
        configured.Should().BeTrue();
    }

    [Fact]
    public void ConfigurePacketTransfer_WhenCalled_ConfiguresPacketTransfer()
    {
        // Arrange
        var configured = false;
        _builder.ConfigurePacketTransfer(_ => configured = true);
        
        // Act
        var config = _builder.BuildConfiguration();
        
        // Assert
        config.Should().NotBeNull();

        var packetTransfer = config.GetSection("PacketTransfer").Get<Config.PacketTransfer>();
        packetTransfer.Should().NotBeNull();
        configured.Should().BeTrue();
    }

    [Fact]
    public void SetConfigValueRaw_OnSimpleProperty_SetsProperty()
    {
        // Arrange
        _builder.SetRawConfigValue("Enabled", true);

        // Act
        var config = _builder.BuildConfiguration();
        var template = config.Get<ConnectorTemplate>();
        
        // Assert
        template.Should().NotBeNull();
        template.Enabled.Should().BeTrue();
    }

    [Fact]
    public void SetConfigValueRaw_OnNestedProperty_SetsProperty()
    {
        // Arrange
        _builder.SetRawConfigValue("PacketTransfer:DbPath", "C:\\Test\\db.sqlite");

        // Act
        var config = _builder.BuildConfiguration();
        var packetTransfer = config.GetSection("PacketTransfer").Get<Config.PacketTransfer>();
        
        // Assert
        packetTransfer.Should().NotBeNull();
        packetTransfer.DbPath.Should().Be("C:\\Test\\db.sqlite");
    }

    [Fact]
    public void SetConfigValueRaw_OnDictionaryProperty_SetsProperty()
    {
       // Arrange
       _builder.SetRawConfigValue("HttpIncoming:Endpoints:Endpoint1:Path", "/api/test");

        // Act
        var config = _builder.BuildConfiguration();
        var httpIncoming = config.GetSection("HttpIncoming").Get<HttpIncoming>();
        
        // Assert
        httpIncoming.Should().NotBeNull();
        httpIncoming.Endpoints.Should().ContainKey("Endpoint1");
        httpIncoming.Endpoints["Endpoint1"].Path.Should().Be("/api/test");
    }

    [Fact]
    public void SetConfigValueRaw_OnArrayItems_SetsArrayItems()
    {
        // Arrange
        _builder.SetRawConfigValue("PacketTransfer:ChannelGroups:Incoming:Channels:0", "A");
        _builder.SetRawConfigValue("PacketTransfer:ChannelGroups:Incoming:Channels:1", "B");

        // Act
        var config = _builder.BuildConfiguration();
        var packetTransfer = config.GetSection("PacketTransfer").Get<Config.PacketTransfer>();
        
        // Assert
        packetTransfer.Should().NotBeNull();
        packetTransfer.ChannelGroups.Should().ContainKey("Incoming");
        packetTransfer.ChannelGroups["Incoming"].Channels.Should().BeEquivalentTo(["A", "B"]);  
    }

    [Fact]
    public void SetConfigValueRaw_WhenCalledMultipleTimes_KeepsLast()
    {
        // Arrange
        _builder.SetRawConfigValue("Enabled", "true");
        _builder.SetRawConfigValue("Enabled", "false");
        
        // Act
        var result = _builder.BuildConfiguration();
        
        // Assert
        result.Should().NotBeNull();

        result["Enabled"].Should().Be("false");
    }

    [Fact]
    public void Build_WhenAllMethodsChained_ConfiguresAllProperties()
    {
        // Arrange
        _builder
            .ConfigureHttpIncoming(_ => { })
            .ConfigureHttpOutgoing(_ => { })
            .ConfigurePacketTransfer(options =>
            {
                options.SetDbPath("C:\\Test\\db.sqlite");
            })
            .SetRawConfigValue("PacketTransfer:DbPath", "C:\\Test\\new_db.sqlite");
        
        // Act
        var config = _builder.BuildConfiguration();
        
        // Assert
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
