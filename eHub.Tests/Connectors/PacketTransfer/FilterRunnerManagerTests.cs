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

public class FilterRunnerManagerTests : IAsyncLifetime
{
    private FilterRunnerManager _filterRunnerManager = null!;
    private ConnectorMetadata _metadata = null!;
    private ConnectorTemplate _connectorTemplate = null!;
    private ILoggerFactory _loggerFactory = null!;
    private IPacketTransfer _packetTransfer = null!;
    private IScopedMessenger _messenger = null!;
    private IDbContextFactory<HubDbContext> _dbContextFactory = null!;

    public ValueTask InitializeAsync()
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
        return ValueTask.CompletedTask;
    }

    [Fact]
    public void TryGetFilterRunner_WhenNoRunnerExists_ReturnsFalseAndNull()
    {
        // Arrange
        _filterRunnerManager = new FilterRunnerManager(_metadata, null!, _loggerFactory, _messenger, _dbContextFactory);

        // Act
        var result = _filterRunnerManager.TryGetFilterRunner("nonExistingRunner", out var runner);

        // Assert
        result.Should().BeFalse();
        runner.Should().BeNull();
    }

    [Fact]
    public void TryGetFilterRunner_WhenRunnerExists_ReturnsTrueAndSameInstance()
    {
        // Arrange
        _filterRunnerManager = new FilterRunnerManager(_metadata, null!, _loggerFactory, _messenger, _dbContextFactory);
        var filterRunner = _filterRunnerManager.GetOrAddFilterRunner("existingRunner");
        
        // Act
        var result = _filterRunnerManager.TryGetFilterRunner("existingRunner", out var existingRunner);
        
        // Assert
        result.Should().BeTrue();
        existingRunner.Should().NotBeNull();
        existingRunner.Should().BeSameAs(filterRunner);
    }

    [Fact]
    public void GetOrAddFilterRunner_WhenCalled_CreatesNewInstance()
    {
        // Arrange
        _filterRunnerManager = new FilterRunnerManager(_metadata, null!, _loggerFactory, _messenger, _dbContextFactory);
        
        // Act
        var filterRunner = _filterRunnerManager.GetOrAddFilterRunner("myRunner");
        
        // Assert
        filterRunner.Should().NotBeNull();
    }

    [Fact]
    public void GetOrAddFilterRunner_WhenCalledTwiceWithSameId_ReturnsSameInstance()
    {
        // Arrange
        _filterRunnerManager = new FilterRunnerManager(_metadata, null!, _loggerFactory, _messenger, _dbContextFactory);

        // Act
        var runner1 = _filterRunnerManager.GetOrAddFilterRunner("myRunner");
        var runner2 = _filterRunnerManager.GetOrAddFilterRunner("myRunner");
        
        // Assert
        runner1.Should().BeSameAs(runner2);
    }

    [Fact]
    public void GetOrAddFilterRunner_WhenCalledWithDifferentIds_ReturnsDifferentInstances()
    {
        // Arrange
        _filterRunnerManager = new FilterRunnerManager(_metadata, null!, _loggerFactory, _messenger, _dbContextFactory);

        // Act
        var runner1 = _filterRunnerManager.GetOrAddFilterRunner("runnerA");
        var runner2 = _filterRunnerManager.GetOrAddFilterRunner("runnerB");
        
        // Assert
        runner1.Should().NotBeNull();
        runner2.Should().NotBeNull();
        runner1.Should().NotBeSameAs(runner2);
    }

    [Fact]
    public async Task FilterRunnersCleaner_WhenCalled_RemovesStaleRunners()
    {
        // Arrange
        var connection = new SqliteConnection(DbHelper.InMemoryConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        _dbContextFactory = DbHelper.CreateInMemoryFactory<HubDbContext>(connection);
        _packetTransfer.Converter.Returns(new StringConverter());

        var runner1Id = Guid.NewGuid().ToString();
        var runner2Id = Guid.NewGuid().ToString();
        var runner3Id = Guid.NewGuid().ToString();

        var regToken = NullRegistrationToken.Instance;
        regToken += await _messenger.AnswerAsync<PacketWrapperDto, bool>(
            ConnectorContract.PushFilteredPacketsTopic(runner1Id), (_) => true);
        regToken += await _messenger.AnswerAsync<PacketWrapperDto, bool>(
            ConnectorContract.PushFilteredPacketsTopic(runner2Id), (_) => true);
        regToken += await _messenger.AnswerAsync<PacketWrapperDto, bool>(
            ConnectorContract.PushFilteredPacketsTopic(runner3Id), (_) => true);

        var packetDtoService = new PacketDtoService(_loggerFactory.CreateLogger<PacketDtoService>(), _packetTransfer,
            _metadata, _connectorTemplate);

        _filterRunnerManager =
            new FilterRunnerManager(_metadata, packetDtoService, _loggerFactory, _messenger, _dbContextFactory);

        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var dateTimeNow = DateTime.Now;

        // Add multiple packets so the runners will be busy
        await context.Packet.AddRangeAsync([
            new Packet()
                { Channel = "TestChannel", Status = PacketStatus.Processed, DateCreated = dateTimeNow.AddSeconds(-10) },
            new Packet()
                { Channel = "TestChannel", Status = PacketStatus.Processed, DateCreated = dateTimeNow.AddSeconds(-5) },
            new Packet()
                { Channel = "TestChannel", Status = PacketStatus.Processed, DateCreated = dateTimeNow.AddSeconds(-1) }
        ]);
        
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

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

        // Act
        // Start all runners
        await Task.WhenAll(
            runner1.StartAsync(filter, cancellationTokenSource.Token),
            runner2.StartAsync(filter, cancellationTokenSource.Token),
            runner3.StartAsync(filter, cancellationTokenSource.Token));

        // Stop only runner1 and runner2
        await Task.WhenAll(
            runner1.StopAsync(TestContext.Current.CancellationToken),
            runner2.StopAsync(TestContext.Current.CancellationToken));

        _ = _filterRunnerManager.FilterRunnersCleaner(cancellationTokenSource.Token);

        // Check if the runners have been removed
        var isRunner1Alive = _filterRunnerManager.TryGetFilterRunner(runner1Id, out _);
        var isRunner2Alive = _filterRunnerManager.TryGetFilterRunner(runner2Id, out _);
        var isRunner3Alive = _filterRunnerManager.TryGetFilterRunner(runner3Id, out _);
        
        // Assert
        // Only runner3 should be alive
        isRunner1Alive.Should().BeFalse();
        isRunner2Alive.Should().BeFalse();
        isRunner3Alive.Should().BeTrue();

        await cancellationTokenSource.CancelAsync();
        await regToken.DisposeAsync();
        await connection.CloseAsync();
    }

    [Fact]
    public async Task DisposeAsync_WhenCalled_StopsAllActiveRunners()
    {
        // Arrange
        var connection = new SqliteConnection(DbHelper.InMemoryConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        _dbContextFactory = DbHelper.CreateInMemoryFactory<HubDbContext>(connection);
        _packetTransfer.Converter.Returns(new StringConverter());

        var runner1Id = Guid.NewGuid().ToString();
        var runner2Id = Guid.NewGuid().ToString();
        var runner3Id = Guid.NewGuid().ToString();

        var regToken = NullRegistrationToken.Instance;
        regToken += await _messenger.AnswerAsync<PacketWrapperDto, bool>(
            ConnectorContract.PushFilteredPacketsTopic(runner1Id), (_) => true);
        regToken += await _messenger.AnswerAsync<PacketWrapperDto, bool>(
            ConnectorContract.PushFilteredPacketsTopic(runner2Id), (_) => true);
        regToken += await _messenger.AnswerAsync<PacketWrapperDto, bool>(
            ConnectorContract.PushFilteredPacketsTopic(runner3Id), (_) => true);

        var runner1Stopped = false;
        var runner2Stopped = false;
        var runner3Stopped = false;
        regToken += await _messenger.ListenAsync<ConnectorIdentifier>(
            ConnectorContract.FilterRunnerStoppedNotification(runner1Id), (_) => runner1Stopped = true);
        regToken += await _messenger.ListenAsync<ConnectorIdentifier>(
            ConnectorContract.FilterRunnerStoppedNotification(runner2Id), (_) => runner2Stopped = true);
        regToken += await _messenger.ListenAsync<ConnectorIdentifier>(
            ConnectorContract.FilterRunnerStoppedNotification(runner3Id), (_) => runner3Stopped = true);

        var packetDtoService = new PacketDtoService(_loggerFactory.CreateLogger<PacketDtoService>(), _packetTransfer,
            _metadata, _connectorTemplate);

        _filterRunnerManager =
            new FilterRunnerManager(_metadata, packetDtoService, _loggerFactory, _messenger, _dbContextFactory);

        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var dateTimeNow = DateTime.Now;
        await context.Packet.AddAsync(new Packet()
            { Channel = "TestChannel", Status = PacketStatus.Processed, DateCreated = dateTimeNow.AddSeconds(-1) }, 
            TestContext.Current.CancellationToken);
        
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        
        // Act
        await _filterRunnerManager.DisposeAsync();
        
        // Assert
        // Check that all runners have been stopped
        runner1Stopped.Should().BeTrue();
        runner2Stopped.Should().BeTrue();
        runner3Stopped.Should().BeTrue();

        await cancellationTokenSource.CancelAsync();
        await regToken.DisposeAsync();
        await connection.CloseAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _filterRunnerManager.DisposeAsync();
    }
}