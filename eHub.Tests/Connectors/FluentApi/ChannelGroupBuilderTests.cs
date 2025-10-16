using eHub.Config;
using eHub.PlugIn;
using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;

namespace eHub.Tests.Connectors.FluentApi;

[TestClass]
public class ChannelGroupBuilderTests
{
    private readonly ChannelGroupBuilder _builder = new();

    [TestMethod]
    public void AddChannel_WithValidChannel_AddsChannel()
    {
        _builder.AddChannel("channel1");
        var config = _builder.Build();

        config.Should().NotBeNull();
        config.Channels.Should().Contain("channel1");
    }

    [TestMethod]
    public void AddChannel_WithMultipleChannels_AddsChannels()
    {
        _builder.AddChannel("ch1");
        _builder.AddChannel("ch2")
            .AddChannel("ch3");

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.Channels.Should().BeEquivalentTo(["ch1", "ch2", "ch3"]);
    }

    [TestMethod]
    public void AddPacketRetentionRule_WithValidInput_AddsRetentionRule()
    {
        _builder.AddPacketRetentionRule(PacketStatus.Error, TimeSpan.FromMinutes(5));

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.PacketRetention.Should().ContainKey(PacketStatus.Error.ToString());

        var timeSpan = TimeSpan.Parse(config.PacketRetention[PacketStatus.Error.ToString()]);
        timeSpan.Should().Be(TimeSpan.FromMinutes(5));
    }

    [TestMethod]
    public void DisableResend_WhenCalled_SetsCanResendFalse()
    {
        _builder.DisableResend();

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.CanResend.Should().BeFalse();
    }

    [TestMethod]
    public void SetChannels_WithValidChannels_SetsChannels()
    {
        _builder.SetChannels(["ch1", "ch2"]);

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.Channels.Should().BeEquivalentTo(["ch1", "ch2"]);
    }

    [TestMethod]
    public void SetChannels_WhenCalledMultipleTimes_KeepsLast()
    {
        _builder.SetChannels(["ch1", "ch2"]);
        _builder.SetChannels(["ch3", "ch4"]);
        _builder.SetChannels(["ch5", "ch6"]);

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.Channels.Should().BeEquivalentTo(["ch5", "ch6"]);
    }

    [TestMethod]
    public void SetCleanerInterval_WithValidInterval_SetsCleanerInterval()
    {
        _builder.SetCleanerInterval(TimeSpan.FromSeconds(10));

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.CleanerInterval.Should().Be(TimeSpan.FromSeconds(10));
    }

    [TestMethod]
    public void SetDbPollInterval_WithValidInterval_SetsDbPollInterval()
    {
        _builder.SetDbPollInterval(TimeSpan.FromSeconds(15));

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.DbPollInterval.Should().Be(TimeSpan.FromSeconds(15));
    }

    [TestMethod]
    public void SetDefaultPacketRetention_WithValidRetention_SetsDefaultRetention()
    {
        _builder.SetDefaultPacketRetention(TimeSpan.FromMinutes(10));

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.PacketRetention.Should().ContainKey("Default");

        var timeSpan = TimeSpan.Parse(config.PacketRetention["Default"]);
        timeSpan.Should().Be(TimeSpan.FromMinutes(10));
    }

    [TestMethod]
    public void SetPacketsPerCycle_WithValidCount_SetsPacketsPerCycle()
    {
        _builder.SetPacketsPerCycle(50);

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.PacketsPerCycle.Should().Be(50);
    }

    [TestMethod]
    public void UseConcurrentProcessing_WhenCalled_SetsModeToConcurrent()
    {
        _builder.UseConcurrentProcessing();

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.Mode.Should().Be(ChannelMode.Concurrent);
    }

    [TestMethod]
    public void UseSequentialProcessing_WhenCalled_SetsModeToSequential()
    {
        _builder.UseSequentialProcessing();


        var config = _builder.Build();

        config.Should().NotBeNull();
        config.Mode.Should().Be(ChannelMode.Sequential);
    }

    [TestMethod]
    public void Build_WithMultipleModes_KeepsLast()
    {
        _builder.UseSequentialProcessing();
        _builder.UseConcurrentProcessing();

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.Mode.Should().Be(ChannelMode.Concurrent);
    }

    [TestMethod]
    public void Build_WhenAllMethodsChained_ConfiguresAllProperties()
    {
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

        var config = _builder.Build();

        var timeSpanError = TimeSpan.Parse(config.PacketRetention[PacketStatus.Error.ToString()]);
        var timeSpanDefault = TimeSpan.Parse(config.PacketRetention["Default"]);

        config.Should().NotBeNull();
        config.Channels.Should().BeEquivalentTo(["ch1", "ch2", "ch3"]);
        config.PacketRetention.Should().ContainKey(PacketStatus.Error.ToString());
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
