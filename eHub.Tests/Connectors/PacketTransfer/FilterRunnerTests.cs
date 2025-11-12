using System.Diagnostics;
using eHub.Config;
using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eHub.Database;
using eHub.Database.Models;
using eHub.PlugIn;
using eHub.Scripting.Connectors;
using eHub.Scripting.Connectors.Services;
using eHub.Tests.Helper;
using eMessenger;
using eMessenger.Tests;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace eHub.Tests.Connectors.PacketTransfer;

public class FilterRunnerTests : IAsyncLifetime
{
    private const int RunnerStopTimeout = 500;
    private FilterRunner _filterRunner = null!;
    private string _filterRunnerId = null!;
    private ConnectorMetadata _metadata = null!;
    private ConnectorTemplate _connectorTemplate = null!;
    private PacketDtoService _packetDtoService = null!;
    private ILoggerFactory _loggerFactory = null!;
    private IPacketTransfer _packetTransfer = null!;
    private IPacketConverter _packetConverter = null!;
    private IScopedMessenger _messenger = null!;
    private SqliteConnection _connection = null!;
    private IDbContextFactory<HubDbContext> _dbContextFactory = null!;
    private CancellationTokenSource _cancellationTokenSource = null!;
    private IRegistrationToken _registrationToken = null!;


    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection(DbHelper.InMemoryConnectionString);
        await _connection.OpenAsync();

        _filterRunnerId = Guid.NewGuid().ToString();
        _metadata = new ConnectorMetadata(new(), "MyTestConnector", "TestConnector");
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

        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _packetTransfer = Substitute.For<IPacketTransfer>();
        _packetConverter = new StringConverter();
        _packetTransfer.Converter.Returns(_packetConverter);
        _messenger = TestingMessenger.CreateScoped();
        _dbContextFactory = DbHelper.CreateInMemoryFactory<HubDbContext>(_connection);

        _packetDtoService = new PacketDtoService(_loggerFactory.CreateLogger<PacketDtoService>(), _packetTransfer, _metadata, _connectorTemplate);
        _filterRunner = new FilterRunner(_filterRunnerId, _metadata, _packetDtoService, _loggerFactory.CreateLogger<FilterRunner>(), _messenger, _dbContextFactory);
        _cancellationTokenSource = new();
        _registrationToken = NullRegistrationToken.Instance;

        // Make sure the database is created
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
    }

    [Fact]
    public async Task StartAsync_WithValidFilter_StartsRunner()
    {
        // Arrange
        _registrationToken = await _messenger.AnswerAsync<PacketWrapperDto, bool>(ConnectorContract.PushFilteredPacketsTopic(_filterRunnerId), (_) => true);

        var startDate = DateTime.Now.AddMinutes(-1);
        var endDate = DateTime.Now;

        await AddTestPacketsAsync(15, startDate, endDate);

        var filter = new PacketRequestDto()
        {
            DateTimeStart = startDate,
            DateTimeEnd = endDate,
            Limit = 5
        };
        
        // Act
        await _filterRunner.StartAsync(filter, _cancellationTokenSource.Token);
        
        // Assert
        _filterRunner.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_RestartsRunner()
    {
        // Arrange
        var stopped = false;
        _registrationToken += await _messenger.AnswerAsync<PacketWrapperDto, bool>(ConnectorContract.PushFilteredPacketsTopic(_filterRunnerId), (_) => true);
        _registrationToken += await _messenger.ListenAsync<ConnectorIdentifier>(ConnectorContract.FilterRunnerStoppedNotification(_filterRunnerId), (_) => stopped = true);

        var startDate = DateTime.Now.AddMinutes(-1);
        var endDate = DateTime.Now;

        await AddTestPacketsAsync(15, startDate, endDate);

        var filter = new PacketRequestDto()
        {
            DateTimeStart = startDate,
            DateTimeEnd = endDate,
            Limit = 5
        };

        // Act
        await _filterRunner.StartAsync(filter, _cancellationTokenSource.Token);
        await _filterRunner.StartAsync(filter, _cancellationTokenSource.Token);
        
        // Assert
        stopped.Should().BeTrue();
        _filterRunner.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_WhenCancelled_StopsRunnerAndNotifies()
    {
        // Arrange
        var stopped = false;
        _registrationToken += await _messenger.AnswerAsync<PacketWrapperDto, bool>(ConnectorContract.PushFilteredPacketsTopic(_filterRunnerId), (_) => true);
        _registrationToken += await _messenger.ListenAsync<ConnectorIdentifier>(ConnectorContract.FilterRunnerStoppedNotification(_filterRunnerId), (_) => stopped = true);

        var startDate = DateTime.Now.AddMinutes(-1);
        var endDate = DateTime.Now;

        await AddTestPacketsAsync(15, startDate, endDate);

        var filter = new PacketRequestDto()
        {
            DateTimeStart = startDate,
            DateTimeEnd = endDate,
            Limit = 5
        };

        // Act
        await _filterRunner.StartAsync(filter, _cancellationTokenSource.Token);
        await _cancellationTokenSource.CancelAsync();
        
        // Make sure the runner has stopped.
        await WaitForRunnerToStopAsync(RunnerStopTimeout);
        
        // Assert
        _filterRunner.IsRunning.Should().BeFalse();
        stopped.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_WhenRunning_StopsRunnerAndNotifies()
    {
        // Arrange
        var stopped = false;
        _registrationToken += await _messenger.AnswerAsync<PacketWrapperDto, bool>(ConnectorContract.PushFilteredPacketsTopic(_filterRunnerId), (_) => true);
        _registrationToken += await _messenger.ListenAsync<ConnectorIdentifier>(ConnectorContract.FilterRunnerStoppedNotification(_filterRunnerId), (_) => stopped = true);

        var startDate = DateTime.Now.AddMinutes(-1);
        var endDate = DateTime.Now;

        await AddTestPacketsAsync(15, startDate, endDate);

        var filter = new PacketRequestDto()
        {
            DateTimeStart = startDate,
            DateTimeEnd = endDate,
            Limit = 5
        };

        // Act
        await _filterRunner.StartAsync(filter, _cancellationTokenSource.Token);
        await _filterRunner.StopAsync(_cancellationTokenSource.Token);
        
        // Make sure the runner has stopped.
        await WaitForRunnerToStopAsync(RunnerStopTimeout);
        
        // Assert
        _filterRunner.IsRunning.Should().BeFalse();
        stopped.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_WhenNotRunning_DoesNotNotify()
    {
        // Arrange
        var stopped = false;
        _registrationToken += await _messenger.ListenAsync<ConnectorIdentifier>(ConnectorContract.FilterRunnerStoppedNotification(_filterRunnerId), (_) => stopped = true);

        // Act
        await _filterRunner.StopAsync(_cancellationTokenSource.Token);
        
        // Make sure the runner has stopped.
        await WaitForRunnerToStopAsync(RunnerStopTimeout);
        
        // Assert
        stopped.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_WhenAllPacketsFiltered_ExitsLoopAndNotifies()
    {
        // Arrange
        var receivedPacketIds = new HashSet<long>();
        _registrationToken += await _messenger.AnswerAsync<PacketWrapperDto, bool>(ConnectorContract.PushFilteredPacketsTopic(_filterRunnerId), (x) =>
        {
            receivedPacketIds.UnionWith(x.Packets.Select(p => p.Id));
            return true;
        });

        var stopped = false;
        _registrationToken += await _messenger.ListenAsync<ConnectorIdentifier>(ConnectorContract.FilterRunnerStoppedNotification(_filterRunnerId), (_) => stopped = true);

        var startDate = DateTime.Now.AddMinutes(-10);
        var endDate = DateTime.Now.AddMinutes(10);

        var filterStartDate = startDate.AddMinutes(-51);
        var filterEndDate = endDate.AddMinutes(5);

        await AddTestPacketsAsync(20, startDate, endDate);

        var filter = new PacketRequestDto()
        {
            DateTimeStart = filterStartDate,
            DateTimeEnd = filterEndDate,
            Limit = 5
        };

        await using var context = await _dbContextFactory.CreateDbContextAsync(_cancellationTokenSource.Token);
        var filteredPackets = await context.Packet.Where(p => p.DateCreated >= filterStartDate && p.DateCreated <= filterEndDate).ToArrayAsync(_cancellationTokenSource.Token);
        var expectedPacketsIds = filteredPackets.Select(p => p.Id).ToHashSet();
        
        // Act
        await _filterRunner.RunAsync(filter, _cancellationTokenSource.Token);
        
        // Assert
        _filterRunner.IsRunning.Should().BeFalse();
        stopped.Should().BeTrue();
        receivedPacketIds.Should().BeEquivalentTo(expectedPacketsIds);
    }

    [Fact]
    public async Task RunAsync_WhenPacketsLeftAfterLoopEnds_SendsRemainingPackets()
    {
        // Arrange
        var receivedPacketIds = new HashSet<long>();
        var batchesSent = 0;

        _registrationToken += await _messenger.AnswerAsync<PacketWrapperDto, bool>(ConnectorContract.PushFilteredPacketsTopic(_filterRunnerId), (x) =>
        {
            receivedPacketIds.UnionWith(x.Packets.Select(p => p.Id));
            batchesSent++;
            return true;
        });

        var stopped = false;
        _registrationToken += await _messenger.ListenAsync<ConnectorIdentifier>(ConnectorContract.FilterRunnerStoppedNotification(_filterRunnerId), (_) => stopped = true);

        var startDate = DateTime.Now.AddMinutes(-10);
        var endDate = DateTime.Now.AddMinutes(10);

        var filterStartDate = startDate.AddMinutes(-51);
        var filterEndDate = endDate.AddMinutes(5);

        await AddTestPacketsAsync(16, startDate, endDate);

        var filter = new PacketRequestDto()
        {
            DateTimeStart = filterStartDate,
            DateTimeEnd = filterEndDate,
            Limit = 5
        };

        await using var context = await _dbContextFactory.CreateDbContextAsync(_cancellationTokenSource.Token);
        var filteredPackets = await context.Packet.Where(p => p.DateCreated >= filterStartDate && p.DateCreated <= filterEndDate)
            .ToArrayAsync(_cancellationTokenSource.Token);
        
        var expectedPacketsIds = filteredPackets.Select(p => p.Id).ToHashSet();

        // The filter runner will send batches of 5 packets of 16 total.
        // This means there will be 3 full batches and 1 partial batch (4 total).
        const int expectedBatchCount = 4;
        
        // Act
        await _filterRunner.RunAsync(filter, _cancellationTokenSource.Token);
        
        // Assert
        _filterRunner.IsRunning.Should().BeFalse();
        stopped.Should().BeTrue();
        receivedPacketIds.Should().BeEquivalentTo(expectedPacketsIds);
        batchesSent.Should().Be(expectedBatchCount);
    }

    [Fact]
    public async Task RunAsync_WhenCancelled_ExitsLoopAndNotifies()
    {
        // Arrange
        var stopped = false;
        _registrationToken += await _messenger.AnswerAsync<PacketWrapperDto, bool>(ConnectorContract.PushFilteredPacketsTopic(_filterRunnerId), (_) => true);
        _registrationToken += await _messenger.ListenAsync<ConnectorIdentifier>(ConnectorContract.FilterRunnerStoppedNotification(_filterRunnerId), (_) => stopped = true);

        var startDate = DateTime.Now.AddMinutes(-1);
        var endDate = DateTime.Now;

        await AddTestPacketsAsync(15, startDate, endDate);

        var filter = new PacketRequestDto()
        {
            DateTimeStart = startDate,
            DateTimeEnd = endDate,
            Limit = 5
        };

        // Act
        var runnerTask = _filterRunner.RunAsync(filter, _cancellationTokenSource.Token);
        await _cancellationTokenSource.CancelAsync();
        
        // Make sure the runner has stopped.
        await WaitForTaskToCompleteAsync(runnerTask, RunnerStopTimeout);
        
        // Assert
        runnerTask.IsCompleted.Should().BeTrue();
        stopped.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_WhenNoAcknowledge_ExitsLoopAndNotifies()
    {
        // Arrange
        var stopped = false;
        _registrationToken += await _messenger.ListenAsync<ConnectorIdentifier>(ConnectorContract.FilterRunnerStoppedNotification(_filterRunnerId), (_) => stopped = true);

        var startDate = DateTime.Now.AddMinutes(-1);
        var endDate = DateTime.Now;

        await AddTestPacketsAsync(15, startDate, endDate);

        var filter = new PacketRequestDto()
        {
            DateTimeStart = startDate,
            DateTimeEnd = endDate,
            Limit = 5
        };

        // Act
        var runnerTask = _filterRunner.RunAsync(filter, _cancellationTokenSource.Token);
        
        // Make sure the runner has stopped.
        await WaitForTaskToCompleteAsync(runnerTask, RunnerStopTimeout);
        
        // Assert
        runnerTask.IsCompleted.Should().BeTrue();
        stopped.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_WhenNoPacketsQueried_ExitsLoopAndNotifies()
    {
        // Arrange
        var stopped = false;
        _registrationToken += await _messenger.ListenAsync<ConnectorIdentifier>(ConnectorContract.FilterRunnerStoppedNotification(_filterRunnerId), (_) => stopped = true);

        var startDate = DateTime.Now.AddMinutes(-1);
        var endDate = DateTime.Now;
        var filter = new PacketRequestDto()
        {
            DateTimeStart = startDate,
            DateTimeEnd = endDate,
            Limit = 5
        };

        // Act
        var runnerTask = _filterRunner.RunAsync(filter, _cancellationTokenSource.Token);
        
        // Make sure the runner has stopped.
        await WaitForTaskToCompleteAsync(runnerTask, RunnerStopTimeout);
        
        // Assert
        runnerTask.IsCompleted.Should().BeTrue();
        stopped.Should().BeTrue();
    }

    [Fact]
    public void RunAsync_CalledWithCancelledToken_ExitsImmediately()
    {
        // Arrange
        var cancelledToken = new CancellationToken(true);
        
        // Act
        var runnerTask = _filterRunner.RunAsync(new PacketRequestDto(), cancelledToken);
        
        // Assert
        runnerTask.IsCompleted.Should().BeTrue();
    }

    private async Task AddTestPacketsAsync(int count, DateTime startDate, DateTime endDate)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var random = new Random();

        // Get all values of the PacketStatus enum.
        var statuses = Enum.GetValues<PacketStatus>();

        // Calculate the interval between packets.
        var totalDuration = endDate - startDate;
        var interval = count > 1
            ? TimeSpan.FromTicks(totalDuration.Ticks / (count - 1))
            : TimeSpan.Zero;

        for (var i = 0; i < count; i++)
        {
            var packet = new Packet
            {
                Channel = "TestChannel",
                Status = statuses[random.Next(statuses.Length)],
                DateCreated = startDate.AddTicks(interval.Ticks * i)
            };

            await context.Packet.AddAsync(packet);
        }

        await context.SaveChangesAsync();
    }

    private async Task WaitForRunnerToStopAsync(int maxMs)
    {
        var sw = Stopwatch.StartNew();
        while (_filterRunner.IsRunning && sw.ElapsedMilliseconds < maxMs)
        {
            await Task.Delay(10);
        }
    }

    private static async Task WaitForTaskToCompleteAsync(Task task, int maxMs)
    {
        var sw = Stopwatch.StartNew();
        while (!task.IsCompleted && sw.ElapsedMilliseconds < maxMs)
        {
            await Task.Delay(10);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource.Dispose();
        await _registrationToken.DisposeAsync();
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }
}
