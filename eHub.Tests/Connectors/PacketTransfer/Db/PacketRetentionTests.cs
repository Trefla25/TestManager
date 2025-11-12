using eHub.Database.Models;
using eHub.Database;
using eHub.PlugIn;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using eHub.Config;
using Microsoft.Extensions.Logging;
using eHub.Scripting.Connectors.Features;
using eMessenger;
using eHub.Scripting.Connectors;
using eHub.Scripting.Connectors.Metrics;
using eMessenger.Tests;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using eHub.Scripting.Connectors.Services;
using eHub.Tests.Helper;
using FluentAssertions;

namespace eHub.Tests.Connectors.PacketTransfer.Db;

public class PacketRetentionTests : IAsyncLifetime
{
    private PacketTransferFeature _feature = null!;
    private IDbContextFactory<HubDbContext> _dbContextFactory = null!;
    private SqliteConnection _connection = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection(DbHelper.InMemoryConnectionString);
        await _connection.OpenAsync();

        _dbContextFactory = DbHelper.CreateInMemoryFactory<HubDbContext>(_connection);

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var messenger = TestingMessenger.CreateScoped();
        var packetTransfer = Substitute.For<IPacketTransfer>();
        var filterRunnerProvider = Substitute.For<IFilterRunnerProvider>();
        var metadata = new ConnectorMetadata(new MessagingContext(), "TestConnector", "TestConnector");
        var template = new ConnectorTemplate() { PacketTransfer = new() { ChannelGroups = [] } };
        var packetDtoService = new PacketDtoService(NullLogger<PacketDtoService>.Instance, packetTransfer, metadata, template);
        var metrics = new ConnectorMetrics(Mocks.MeterFactory, metadata);

        // Create a new instance with mocked dependencies
        _feature = new PacketTransferFeature(
            loggerFactory.CreateLogger<PacketTransferFeature>(),
            _dbContextFactory,
            messenger,
            packetTransfer,
            filterRunnerProvider,
            null!,
            packetDtoService,
            metadata,
            template,
            null!,
            metrics
        );
    }

    [Fact]
    public async Task Test_DefaultPacketRetention_DeletesExpectedRows()
    {
        // Arrange
        var channelGroup = new ChannelGroup() { Channels = ["Channel1"], PacketRetention = new() { { "Default", "10m" } } };

        await using var hubDbContext = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        var cleanContext = new PacketTransferFeature.DbCleanContext("A", channelGroup)
        {
            LastClean = DateTime.MinValue
        };

        // Seed data
        await hubDbContext.Packet.AddRangeAsync(
            new Packet { Channel = "Channel1", Status = PacketStatus.Enqueued, DateCreated = DateTime.Now.AddMinutes(-15) },
            new Packet { Channel = "Channel1", Status = PacketStatus.Enqueued, DateCreated = DateTime.Now.AddMinutes(-5) }
        );
        await hubDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var deletedRows = await _feature.DeleteExpiredPackets(cleanContext, hubDbContext, TestContext.Current.CancellationToken);
        
        // Assert
        deletedRows.Should().Be(1, "Only one row should be deleted");
        (await hubDbContext.Packet.CountAsync(cancellationToken: TestContext.Current.CancellationToken)).Should().Be(1, "Only one row should remain in the database");
    }

    [Fact]
    public async Task Test_SpecificStatusPacketRetention_DeletesExpectedRows()
    {
        // Arrange
        var channelGroup = new ChannelGroup() { Channels = ["Channel1"], PacketRetention = new() { { "Enqueued", "10m" }, { "Default", "30m" } } };

        await using var hubDbContext = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        var context = new PacketTransferFeature.DbCleanContext("A", channelGroup)
        {
            LastClean = DateTime.MinValue
        };

        // Seed data
        await hubDbContext.Packet.AddRangeAsync(
            new Packet { Channel = "Channel1", Status = PacketStatus.Enqueued, DateCreated = DateTime.Now.AddMinutes(-15) },
            new Packet { Channel = "Channel1", Status = PacketStatus.Enqueued, DateCreated = DateTime.Now.AddMinutes(-5) },
            new Packet { Channel = "Channel1", Status = PacketStatus.Processed, DateCreated = DateTime.Now.AddMinutes(-25) }
        );
        await hubDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var deletedRows = await _feature.DeleteExpiredPackets(context, hubDbContext, TestContext.Current.CancellationToken);
        
        // Assert
        deletedRows.Should().Be(1, "One row matching specific retention should be deleted");
        (await hubDbContext.Packet.CountAsync(cancellationToken: TestContext.Current.CancellationToken)).Should().Be(2, "Two rows should remain in the database");
    }

    [Fact]
    public async Task Test_NoMatchingRetention_NoRowsDeleted()
    {
        // Arrange
        var channelGroup = new ChannelGroup() { Channels = ["Channel1"], PacketRetention = new() { { "Processed", "5m" }, { "Default", "30d" } } };

        await using var hubDbContext = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        var context = new PacketTransferFeature.DbCleanContext("A", channelGroup)
        {
            LastClean = DateTime.MinValue
        };

        // Seed data
        await hubDbContext.Packet.AddRangeAsync(
            new Packet { Channel = "Channel1", Status = PacketStatus.Enqueued, DateCreated = DateTime.Now.AddMinutes(-15) },
            new Packet { Channel = "Channel1", Status = PacketStatus.Enqueued, DateCreated = DateTime.Now.AddMinutes(-10) }
        );
        await hubDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var deletedRows = await _feature.DeleteExpiredPackets(context, hubDbContext, TestContext.Current.CancellationToken);
        
        // Assert
        deletedRows.Should().Be(0, "No rows should be deleted");
        (await hubDbContext.Packet.CountAsync(cancellationToken: TestContext.Current.CancellationToken)).Should().Be(2, "All rows should remain in the database");
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }
}
