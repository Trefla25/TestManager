using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;

namespace eHub.Tests.Connectors.FluentApi;

[TestClass]
public class PacketTransferBuilderTests
{
    private readonly PacketTransferBuilder _builder = new();

    [TestMethod]
    public void AddChannelGroup_WhenCalled_AddsChannelGroup()
    {
        var configured = false;
        _builder.AddChannelGroup("group1", cg => configured = true);

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.ChannelGroups.Should().ContainKey("group1");
        configured.Should().BeTrue();
    }

    [TestMethod]
    public void AddChannelGroup_WithMultipleGroups_AddsChannelGroup()
    {
        _builder.AddChannelGroup("group1", cg => { });
        _builder.AddChannelGroup("group2", cg => { })
            .AddChannelGroup("group3", cg => { });

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.ChannelGroups.Should().ContainKey("group1");
        config.ChannelGroups.Should().ContainKey("group2");
        config.ChannelGroups.Should().ContainKey("group3");
    }

    [TestMethod]
    public void AddChannelGroup_WhenCalledMultipleTimes_ThrowsException()
    {
        _builder.AddChannelGroup("group1", cg => { });

        var act = () => _builder.AddChannelGroup("group1", cg => { });

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void SetDbPath_WithValidPath_SetsDbPath()
    {
        _builder.SetDbPath("path/to/db");

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.DbPath.Should().Be("path/to/db");
    }

    [TestMethod]
    public void SetDbPath_WhenCalledMultipleTimes_KeepsLast()
    {
        _builder
            .SetDbPath("path/to/db")
            .SetDbPath("new/path/db");

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.DbPath.Should().Be("new/path/db");
    }

    [TestMethod]
    public void AddSqlitePragma_WhenCalled_AddsPragma()
    {
        _builder.AddSqlitePragma("journal_mode", "WAL");
        _builder.AddSqlitePragma("busy_timeout", "5000");

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.SqlitePragmas.Should().ContainKey("journal_mode");
        config.SqlitePragmas.Should().ContainKey("busy_timeout");
        config.SqlitePragmas["journal_mode"].Should().Be("WAL");
        config.SqlitePragmas["busy_timeout"].Should().Be("5000");
    }

    [TestMethod]
    public void AddSqlitePragma_WhenCalledOnSamePragma_KeepsLast()
    {
        _builder
            .AddSqlitePragma("journal_mode", "WAL")
            .AddSqlitePragma("journal_mode", "DELETE");

        var config = _builder.Build();
        config.Should().NotBeNull();
        config.SqlitePragmas.Should().ContainKey("journal_mode");
        config.SqlitePragmas["journal_mode"].Should().Be("DELETE");
    }

    [TestMethod]
    public void Build_WhenAllMethodsChained_ConfiguresAllProperties()
    {
        _builder
            .AddChannelGroup("group1", cg => { })
            .SetDbPath("path/to/db")
            .AddSqlitePragma("journal_mode", "WAL");

        var config = _builder.Build();

        config.Should().NotBeNull();
        config.ChannelGroups.Should().ContainKey("group1");
        config.DbPath.Should().Be("path/to/db");
        config.SqlitePragmas.Should().ContainKey("journal_mode");
        config.SqlitePragmas["journal_mode"].Should().Be("WAL");
    }
}
