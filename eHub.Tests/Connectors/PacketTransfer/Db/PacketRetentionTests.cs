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

namespace eHub.Tests.Connectors.PacketTransfer.Db;

[TestClass]
public class PacketRetentionTests
{
    private PacketTransferFeature _feature = default!;
    private IDbContextFactory<HubDbContext> _dbContextFactory = default!;
    private SqliteConnection _connection = default!;

    [TestInitialize]
    public async Task TestInitialize()
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

    [TestMethod]
    public async Task Test_DefaultPacketRetention_DeletesExpectedRows()
    {
        var channelGroup = new ChannelGroup() { Channels = ["Channel1"], PacketRetention = new() { { "Default", "10m" } } };

        await using var hubDbContext = await _dbContextFactory.CreateDbContextAsync();
        var cleanContext = new PacketTransferFeature.DbCleanContext("A", channelGroup)
        {
            LastClean = DateTime.MinValue
        };

        // Seed data
        await hubDbContext.Packet.AddRangeAsync(
            new Packet { Channel = "Channel1", Status = PacketStatus.Enqueued, DateCreated = DateTime.Now.AddMinutes(-15) },
            new Packet { Channel = "Channel1", Status = PacketStatus.Enqueued, DateCreated = DateTime.Now.AddMinutes(-5) }
        );
        await hubDbContext.SaveChangesAsync();

        var cancellationToken = CancellationToken.None;
        var deletedRows = await _feature.DeleteExpiredPackets(cleanContext, hubDbContext, cancellationToken);

        Assert.AreEqual(1, deletedRows, "Only one row should be deleted");
        Assert.AreEqual(1, await hubDbContext.Packet.CountAsync(), "Only one row should remain in the database");
    }

    [TestMethod]
    public async Task Test_SpecificStatusPacketRetention_DeletesExpectedRows()
    {
        var channelGroup = new ChannelGroup() { Channels = ["Channel1"], PacketRetention = new() { { "Enqueued", "10m" }, { "Default", "30m" } } };

        await using var hubDbContext = await _dbContextFactory.CreateDbContextAsync();
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
        await hubDbContext.SaveChangesAsync();

        var cancellationToken = CancellationToken.None;
        var deletedRows = await _feature.DeleteExpiredPackets(context, hubDbContext, cancellationToken);

        Assert.AreEqual(1, deletedRows, "One row matching specific retention should be deleted");
        Assert.AreEqual(2, await hubDbContext.Packet.CountAsync(), "Two rows should remain in the database");
    }

    [TestMethod]
    public async Task Test_NoMatchingRetention_NoRowsDeleted()
    {
        var channelGroup = new ChannelGroup() { Channels = ["Channel1"], PacketRetention = new() { { "Processed", "5m" }, { "Default", "30d" } } };

        await using var hubDbContext = await _dbContextFactory.CreateDbContextAsync();
        var context = new PacketTransferFeature.DbCleanContext("A", channelGroup)
        {
            LastClean = DateTime.MinValue
        };

        // Seed data
        await hubDbContext.Packet.AddRangeAsync(
            new Packet { Channel = "Channel1", Status = PacketStatus.Enqueued, DateCreated = DateTime.Now.AddMinutes(-15) },
            new Packet { Channel = "Channel1", Status = PacketStatus.Enqueued, DateCreated = DateTime.Now.AddMinutes(-10) }
        );
        await hubDbContext.SaveChangesAsync();

        var cancellationToken = CancellationToken.None;
        var deletedRows = await _feature.DeleteExpiredPackets(context, hubDbContext, cancellationToken);

        Assert.AreEqual(0, deletedRows, "No rows should be deleted");
        Assert.AreEqual(2, await hubDbContext.Packet.CountAsync(), "All rows should remain in the database");
    }

    [TestCleanup]
    public async Task TestCleanup()
    {
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }
}
