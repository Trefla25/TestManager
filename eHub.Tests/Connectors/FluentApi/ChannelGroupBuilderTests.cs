using eHub.Config;
using eHub.PlugIn;
using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;

namespace eHub.Tests.Connectors.FluentApi;

public class ChannelGroupBuilderTests
{
    private readonly ChannelGroupBuilder _builder = new();

    [Fact]
    public void AddChannel_WithValidChannel_AddsChannel()
    {
        // Arrange
        _builder.AddChannel("channel1");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.Channels.Should().Contain("channel1");
    }

    [Fact]
    public void AddChannel_WithMultipleChannels_AddsChannels()
    {
        // Arrange
        _builder.AddChannel("ch1");
        _builder.AddChannel("ch2")
            .AddChannel("ch3");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.Channels.Should().BeEquivalentTo(["ch1", "ch2", "ch3"]);
    }

    [Fact]
    public void AddPacketRetentionRule_WithValidInput_AddsRetentionRule()
    {
        // Arrange
        _builder.AddPacketRetentionRule(PacketStatus.Error, TimeSpan.FromMinutes(5));
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.PacketRetention.Should().ContainKey(nameof(PacketStatus.Error));

        var timeSpan = TimeSpan.Parse(config.PacketRetention[nameof(PacketStatus.Error)]);
        timeSpan.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void DisableResend_WhenCalled_SetsCanResendFalse()
    {
        // Arrange
        _builder.DisableResend();
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.CanResend.Should().BeFalse();
    }

    [Fact]
    public void SetChannels_WithValidChannels_SetsChannels()
    {
        // Arrange
        _builder.SetChannels(["ch1", "ch2"]);
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.Channels.Should().BeEquivalentTo(["ch1", "ch2"]);
    }

    [Fact]
    public void SetChannels_WhenCalledMultipleTimes_KeepsLast()
    {
        // Arrange
        _builder.SetChannels(["ch1", "ch2"]);
        _builder.SetChannels(["ch3", "ch4"]);
        _builder.SetChannels(["ch5", "ch6"]);
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.Channels.Should().BeEquivalentTo(["ch5", "ch6"]);
    }

    [Fact]
    public void SetCleanerInterval_WithValidInterval_SetsCleanerInterval()
    {
        // Arrange
        _builder.SetCleanerInterval(TimeSpan.FromSeconds(10));
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.CleanerInterval.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void SetDbPollInterval_WithValidInterval_SetsDbPollInterval()
    {
        // Arrange
        _builder.SetDbPollInterval(TimeSpan.FromSeconds(15));
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.DbPollInterval.Should().Be(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void SetDefaultPacketRetention_WithValidRetention_SetsDefaultRetention()
    {
        // Arrange
        _builder.SetDefaultPacketRetention(TimeSpan.FromMinutes(10));
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.PacketRetention.Should().ContainKey("Default");

        var timeSpan = TimeSpan.Parse(config.PacketRetention["Default"]);
        timeSpan.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void SetPacketsPerCycle_WithValidCount_SetsPacketsPerCycle()
    {
        // Arrange
        _builder.SetPacketsPerCycle(50);
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.PacketsPerCycle.Should().Be(50);
    }

    [Fact]
    public void UseConcurrentProcessing_WhenCalled_SetsModeToConcurrent()
    {
        // Arrange
        _builder.UseConcurrentProcessing();
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.Mode.Should().Be(ChannelMode.Concurrent);
    }

    [Fact]
    public void UseSequentialProcessing_WhenCalled_SetsModeToSequential()
    {
        // Arrange
        _builder.UseSequentialProcessing();
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.Mode.Should().Be(ChannelMode.Sequential);
    }

    [Fact]
    public void Build_WithMultipleModes_KeepsLast()
    {
        // Arrange
        _builder.UseSequentialProcessing();
        _builder.UseConcurrentProcessing();
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.Mode.Should().Be(ChannelMode.Concurrent);
    }

    [Fact]
    public void Build_WhenAllMethodsChained_ConfiguresAllProperties()
    {
        // Arrange
        _builder
            .SetChannels(["ch1", "ch2"])
            .AddChannel("ch3")
            .AddPacketRetentionRule(PacketStatus.Error, TimeSpan.FromMinutes(5))
            .DisableResend()
            .SetCleanerInterval(TimeSpan.FromSeconds(10))
            .SetDbPollInterval(TimeSpan.FromSeconds(15))
            .SetDefaultPacketRetention(TimeSpan.FromMinutes(10))
            .SetPacketsPerCycle(50)
            .UseConcurrentProcessing();

        // Act
        var config = _builder.Build();

        var timeSpanError = TimeSpan.Parse(config.PacketRetention[nameof(PacketStatus.Error)]);
        var timeSpanDefault = TimeSpan.Parse(config.PacketRetention["Default"]);
        
        // Assert
        config.Should().NotBeNull();
        config.Channels.Should().BeEquivalentTo(["ch1", "ch2", "ch3"]);
        config.PacketRetention.Should().ContainKey(nameof(PacketStatus.Error));
        config.CanResend.Should().BeFalse();
        config.CleanerInterval.Should().Be(TimeSpan.FromSeconds(10));
        config.DbPollInterval.Should().Be(TimeSpan.FromSeconds(15));
        config.PacketRetention.Should().ContainKey("Default");
        config.PacketsPerCycle.Should().Be(50);
        config.Mode.Should().Be(ChannelMode.Concurrent);
        timeSpanError.Should().Be(TimeSpan.FromMinutes(5));
        timeSpanDefault.Should().Be(TimeSpan.FromMinutes(10));
    }
}
