using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;

namespace eHub.Tests.Connectors.FluentApi;

public class PacketTransferBuilderTests
{
    private readonly PacketTransferBuilder _builder = new();

    [Fact]
    public void AddChannelGroup_WhenCalled_AddsChannelGroup()
    {
        // Arrange
        var configured = false;
        _builder.AddChannelGroup("group1", _ => configured = true);
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.ChannelGroups.Should().ContainKey("group1");
        configured.Should().BeTrue();
    }

    [Fact]
    public void AddChannelGroup_WithMultipleGroups_AddsChannelGroup()
    {
        // Arrange
        _builder.AddChannelGroup("group1", _ => { });
        _builder.AddChannelGroup("group2", _ => { })
            .AddChannelGroup("group3", _ => { });
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.ChannelGroups.Should().ContainKey("group1");
        config.ChannelGroups.Should().ContainKey("group2");
        config.ChannelGroups.Should().ContainKey("group3");
    }

    [Fact]
    public void AddChannelGroup_WhenCalledMultipleTimes_ThrowsException()
    {
        // Arrange
        _builder.AddChannelGroup("group1", _ => { });
        
        // Act
        var act = () => _builder.AddChannelGroup("group1", _ => { });
        
        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetDbPath_WithValidPath_SetsDbPath()
    {
        // Arrange
        _builder.SetDbPath("path/to/db");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.DbPath.Should().Be("path/to/db");
    }

    [Fact]
    public void SetDbPath_WhenCalledMultipleTimes_KeepsLast()
    {
        // Arrange
        _builder
            .SetDbPath("path/to/db")
            .SetDbPath("new/path/db");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.DbPath.Should().Be("new/path/db");
    }

    [Fact]
    public void AddSqlitePragma_WhenCalled_AddsPragma()
    {
        // Arrange
        _builder.AddSqlitePragma("journal_mode", "WAL");
        _builder.AddSqlitePragma("busy_timeout", "5000");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.SqlitePragmas.Should().ContainKey("journal_mode");
        config.SqlitePragmas.Should().ContainKey("busy_timeout");
        config.SqlitePragmas["journal_mode"].Should().Be("WAL");
        config.SqlitePragmas["busy_timeout"].Should().Be("5000");
    }

    [Fact]
    public void AddSqlitePragma_WhenCalledOnSamePragma_KeepsLast()
    {
        // Arrange
        _builder
            .AddSqlitePragma("journal_mode", "WAL")
            .AddSqlitePragma("journal_mode", "DELETE");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.SqlitePragmas.Should().ContainKey("journal_mode");
        config.SqlitePragmas["journal_mode"].Should().Be("DELETE");
    }

    [Fact]
    public void Build_WhenAllMethodsChained_ConfiguresAllProperties()
    {
        // Arrange
        _builder
            .AddChannelGroup("group1", _ => { })
            .SetDbPath("path/to/db")
            .AddSqlitePragma("journal_mode", "WAL");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.Should().NotBeNull();
        config.ChannelGroups.Should().ContainKey("group1");
        config.DbPath.Should().Be("path/to/db");
        config.SqlitePragmas.Should().ContainKey("journal_mode");
        config.SqlitePragmas["journal_mode"].Should().Be("WAL");
    }
}
