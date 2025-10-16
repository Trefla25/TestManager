using eHub.Database;
using eHub.PlugIn;
using eHub.Scripting.Connectors;
using eHub.Scripting.Connectors.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Microsoft.EntityFrameworkCore;
using eMessenger;
using FluentAssertions;
using eHub.Config;
using Microsoft.Data.Sqlite;
using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eMessenger.Tests;
using eHub.Database.Models;
using eHub.Tests.Helper;

namespace eHub.Tests.Connectors.PacketTransfer;

[TestClass]
public class FilterRunnerManagerTests
{
    private FilterRunnerManager _filterRunnerManager = default!;
    private ConnectorMetadata _metadata = default!;
    private ConnectorTemplate _connectorTemplate = default!;
    private ILoggerFactory _loggerFactory = default!;
    private IPacketTransfer _packetTransfer = default!;
    private IScopedMessenger _messenger = default!;
    private IDbContextFactory<HubDbContext> _dbContextFactory = default!;

    [TestInitialize]
    public void TestInitialize()
    {
        _metadata = new ConnectorMetadata(new(), "MyTestConnector", "TestConnector");
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _packetTransfer = Substitute.For<IPacketTransfer>();
        _messenger = TestingMessenger.CreateScoped();
        _dbContextFactory = Substitute.For<IDbContextFactory<HubDbContext>>();
        _connectorTemplate = new ConnectorTemplate()
        {
            PacketTransfer = new()
            {
                ChannelGroups = new Dictionary<string, ChannelGroup>()
                {
                    ["TestGroup"] = new ChannelGroup()
                    {
                        Channels = ["TestChannel"]
                    }
                }
            }
        };
    }

    [TestMethod]
    public void TryGetFilterRunner_WhenNoRunnerExists_ReturnsFalseAndNull()
    {
        _filterRunnerManager = new FilterRunnerManager(_metadata, null!, _loggerFactory, _messenger, _dbContextFactory);

        var result = _filterRunnerManager.TryGetFilterRunner("nonExistingRunner", out var runner);

        result.Should().BeFalse();
        runner.Should().BeNull();
    }

    [TestMethod]
    public void TryGetFilterRunner_WhenRunnerExists_ReturnsTrueAndSameInstance()
    {
        _filterRunnerManager = new FilterRunnerManager(_metadata, null!, _loggerFactory, _messenger, _dbContextFactory);
        var filterRunner = _filterRunnerManager.GetOrAddFilterRunner("existingRunner");

        var result = _filterRunnerManager.TryGetFilterRunner("existingRunner", out var existingRunner);

        result.Should().BeTrue();
        existingRunner.Should().NotBeNull();
        existingRunner.Should().BeSameAs(filterRunner);
    }

    [TestMethod]
    public void GetOrAddFilterRunner_WhenCalled_CreatesNewInstance()
    {
        _filterRunnerManager = new FilterRunnerManager(_metadata, null!, _loggerFactory, _messenger, _dbContextFactory);

        var filterRunner = _filterRunnerManager.GetOrAddFilterRunner("myRunner");

        filterRunner.Should().NotBeNull();
    }

    [TestMethod]
    public void GetOrAddFilterRunner_WhenCalledTwiceWithSameId_ReturnsSameInstance()
    {
        _filterRunnerManager = new FilterRunnerManager(_metadata, null!, _loggerFactory, _messenger, _dbContextFactory);

        var runner1 = _filterRunnerManager.GetOrAddFilterRunner("myRunner");
        var runner2 = _filterRunnerManager.GetOrAddFilterRunner("myRunner");

        runner1.Should().BeSameAs(runner2);
    }

    [TestMethod]
    public void GetOrAddFilterRunner_WhenCalledWithDifferentIds_ReturnsDifferentInstances()
    {
        _filterRunnerManager = new FilterRunnerManager(_metadata, null!, _loggerFactory, _messenger, _dbContextFactory);

        var runner1 = _filterRunnerManager.GetOrAddFilterRunner("runnerA");
        var runner2 = _filterRunnerManager.GetOrAddFilterRunner("runnerB");

        runner1.Should().NotBeNull();
        runner2.Should().NotBeNull();
        runner1.Should().NotBeSameAs(runner2);
    }

    [TestMethod]
    public async Task FilterRunnersCleaner_WhenCalled_RemovesStaleRunners()
    {
        var connection = new SqliteConnection(DbHelper.InMemoryConnectionString);
        await connection.OpenAsync();

        _dbContextFactory = DbHelper.CreateInMemoryFactory<HubDbContext>(connection);
        _packetTransfer.Converter.Returns(new StringConverter());

        var runner1Id = Guid.NewGuid().ToString();
        var runner2Id = Guid.NewGuid().ToString();
        var runner3Id = Guid.NewGuid().ToString();

        var regToken = NullRegistrationToken.Instance;
        regToken += await _messenger.AnswerAsync<PacketWrapperDto, bool>(ConnectorContract.PushFilteredPacketsTopic(runner1Id), (x) => true);
        regToken += await _messenger.AnswerAsync<PacketWrapperDto, bool>(ConnectorContract.PushFilteredPacketsTopic(runner2Id), (x) => true);
        regToken += await _messenger.AnswerAsync<PacketWrapperDto, bool>(ConnectorContract.PushFilteredPacketsTopic(runner3Id), (x) => true);

        var packetDtoService = new PacketDtoService(_loggerFactory.CreateLogger<PacketDtoService>(), _packetTransfer, _metadata, _connectorTemplate);

        _filterRunnerManager = new FilterRunnerManager(_metadata, packetDtoService, _loggerFactory, _messenger, _dbContextFactory);

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();

        var dateTimeNow = DateTime.Now;

        // Add multiple packets so the runners will be busy
        await context.Packet.AddRangeAsync([
            new Packet() { Channel = "TestChannel", Status = PacketStatus.Processed, DateCreated = dateTimeNow.AddSeconds(-10) },
            new Packet() { Channel = "TestChannel", Status = PacketStatus.Processed, DateCreated = dateTimeNow.AddSeconds(-5) },
            new Packet() { Channel = "TestChannel", Status = PacketStatus.Processed, DateCreated = dateTimeNow.AddSeconds(-1) }]);
        await context.SaveChangesAsync();

        var runner1 = _filterRunnerManager.GetOrAddFilterRunner(runner1Id);
        var runner2 = _filterRunnerManager.GetOrAddFilterRunner(runner2Id);
        var runner3 = _filterRunnerManager.GetOrAddFilterRunner(runner3Id);

        var filter = new PacketRequestDto()
        {
            DateTimeStart = DateTime.Now.AddSeconds(-15),
            DateTimeEnd = DateTime.Now,
            Limit = 1
        };

        var cancellationTokenSource = new CancellationTokenSource();

        // Start all runners
        await Task.WhenAll(
            runner1.StartAsync(filter, cancellationTokenSource.Token),
            runner2.StartAsync(filter, cancellationTokenSource.Token),
            runner3.StartAsync(filter, cancellationTokenSource.Token));

        // Stop only runner1 and runner2
        await Task.WhenAll(
            runner1.StopAsync(),
            runner2.StopAsync());

        _ = _filterRunnerManager.FilterRunnersCleaner(cancellationTokenSource.Token);

        // Check if the runners have been removed
        var isRunner1Alive = _filterRunnerManager.TryGetFilterRunner(runner1Id, out _);
        var isRunner2Alive = _filterRunnerManager.TryGetFilterRunner(runner2Id, out _);
        var isRunner3Alive = _filterRunnerManager.TryGetFilterRunner(runner3Id, out _);

        // Only runner3 should be alive
        isRunner1Alive.Should().BeFalse();
        isRunner2Alive.Should().BeFalse();
        isRunner3Alive.Should().BeTrue();

        await cancellationTokenSource.CancelAsync();
        await regToken.DisposeAsync();
        await connection.CloseAsync();
    }

    [TestMethod]
    public async Task DisposeAsync_WhenCalled_StopsAllActiveRunners()
    {
        var connection = new SqliteConnection(DbHelper.InMemoryConnectionString);
        await connection.OpenAsync();

        _dbContextFactory = DbHelper.CreateInMemoryFactory<HubDbContext>(connection);
        _packetTransfer.Converter.Returns(new StringConverter());

        var runner1Id = Guid.NewGuid().ToString();
        var runner2Id = Guid.NewGuid().ToString();
        var runner3Id = Guid.NewGuid().ToString();

        var regToken = NullRegistrationToken.Instance;
        regToken += await _messenger.AnswerAsync<PacketWrapperDto, bool>(ConnectorContract.PushFilteredPacketsTopic(runner1Id), (x) => true);
        regToken += await _messenger.AnswerAsync<PacketWrapperDto, bool>(ConnectorContract.PushFilteredPacketsTopic(runner2Id), (x) => true);
        regToken += await _messenger.AnswerAsync<PacketWrapperDto, bool>(ConnectorContract.PushFilteredPacketsTopic(runner3Id), (x) => true);

        var runner1Stopped = false;
        var runner2Stopped = false;
        var runner3Stopped = false;
        regToken += await _messenger.ListenAsync<ConnectorIdentifier>(ConnectorContract.FilterRunnerStoppedNotification(runner1Id), (x) => runner1Stopped = true);
        regToken += await _messenger.ListenAsync<ConnectorIdentifier>(ConnectorContract.FilterRunnerStoppedNotification(runner2Id), (x) => runner2Stopped = true);
        regToken += await _messenger.ListenAsync<ConnectorIdentifier>(ConnectorContract.FilterRunnerStoppedNotification(runner3Id), (x) => runner3Stopped = true);

        var packetDtoService = new PacketDtoService(_loggerFactory.CreateLogger<PacketDtoService>(), _packetTransfer, _metadata, _connectorTemplate);

        _filterRunnerManager = new FilterRunnerManager(_metadata, packetDtoService, _loggerFactory, _messenger, _dbContextFactory);

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();

        var dateTimeNow = DateTime.Now;
        await context.Packet.AddAsync(new Packet() { Channel = "TestChannel", Status = PacketStatus.Processed, DateCreated = dateTimeNow.AddSeconds(-1) });
        await context.SaveChangesAsync();

        var runner1 = _filterRunnerManager.GetOrAddFilterRunner(runner1Id);
        var runner2 = _filterRunnerManager.GetOrAddFilterRunner(runner2Id);
        var runner3 = _filterRunnerManager.GetOrAddFilterRunner(runner3Id);

        var filter = new PacketRequestDto()
        {
            DateTimeStart = DateTime.Now.AddSeconds(-2),
            DateTimeEnd = DateTime.Now,
            Limit = 1
        };

        var cancellationTokenSource = new CancellationTokenSource();

        // Start all runners
        await Task.WhenAll(
            runner1.StartAsync(filter, cancellationTokenSource.Token),
            runner2.StartAsync(filter, cancellationTokenSource.Token),
            runner3.StartAsync(filter, cancellationTokenSource.Token));

        await _filterRunnerManager.DisposeAsync();

        // Check that all runners have been stopped
        runner1Stopped.Should().BeTrue();
        runner2Stopped.Should().BeTrue();
        runner3Stopped.Should().BeTrue();

        await cancellationTokenSource.CancelAsync();
        await regToken.DisposeAsync();
        await connection.CloseAsync();
    }

    [TestCleanup]
    public async Task TestCleanup()
    {
        await _filterRunnerManager.DisposeAsync();
    }
}
