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

[TestClass]
public class FilterRunnerTests
{
    private const int RunnerStopTimeout = 500;
    private FilterRunner _filterRunner = default!;
    private string _filterRunnerId = default!;
    private ConnectorMetadata _metadata = default!;
    private ConnectorTemplate _connectorTemplate = default!;
    private PacketDtoService _packetDtoService = default!;
    private ILoggerFactory _loggerFactory = default!;
    private IPacketTransfer _packetTransfer = default!;
    private IPacketConverter _packetConverter = default!;
    private IScopedMessenger _messenger = default!;
    private SqliteConnection _connection = default!;
    private IDbContextFactory<HubDbContext> _dbContextFactory = default!;
    private CancellationTokenSource _cancellationTokenSource = default!;
    private IRegistrationToken _registrationToken = default!;


    [TestInitialize]
    public async Task TestInitialize()
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

    [TestMethod]
    public async Task StartAsync_WithValidFilter_StartsRunner()
    {
        _registrationToken = await _messenger.AnswerAsync<PacketWrapperDto, bool>(ConnectorContract.PushFilteredPacketsTopic(_filterRunnerId), (x) => true);

        var startDate = DateTime.Now.AddMinutes(-1);
        var endDate = DateTime.Now;

        await AddTestPacketsAsync(15, startDate, endDate);

        var filter = new PacketRequestDto()
        {
            DateTimeStart = startDate,
            DateTimeEnd = endDate,
            Limit = 5
        };

        await _filterRunner.StartAsync(filter, _cancellationTokenSource.Token);

        _filterRunner.IsRunning.Should().BeTrue();
    }

    [TestMethod]
    public async Task StartAsync_WhenAlreadyRunning_RestartsRunner()
    {
        var stopped = false;
        _registrationToken += await _messenger.AnswerAsync<PacketWrapperDto, bool>(ConnectorContract.PushFilteredPacketsTopic(_filterRunnerId), (x) => true);
        _registrationToken += await _messenger.ListenAsync<ConnectorIdentifier>(ConnectorContract.FilterRunnerStoppedNotification(_filterRunnerId), (x) => stopped = true);

        var startDate = DateTime.Now.AddMinutes(-1);
        var endDate = DateTime.Now;

        await AddTestPacketsAsync(15, startDate, endDate);

        var filter = new PacketRequestDto()
        {
            DateTimeStart = startDate,
            DateTimeEnd = endDate,
            Limit = 5
        };

        await _filterRunner.StartAsync(filter, _cancellationTokenSource.Token);
        await _filterRunner.StartAsync(filter, _cancellationTokenSource.Token);

        stopped.Should().BeTrue();
        _filterRunner.IsRunning.Should().BeTrue();
    }

    [TestMethod]
    public async Task StartAsync_WhenCancelled_StopsRunnerAndNotifies()
    {
        var stopped = false;
        _registrationToken += await _messenger.AnswerAsync<PacketWrapperDto, bool>(ConnectorContract.PushFilteredPacketsTopic(_filterRunnerId), (x) => true);
        _registrationToken += await _messenger.ListenAsync<ConnectorIdentifier>(ConnectorContract.FilterRunnerStoppedNotification(_filterRunnerId), (x) => stopped = true);

        var startDate = DateTime.Now.AddMinutes(-1);
        var endDate = DateTime.Now;

        await AddTestPacketsAsync(15, startDate, endDate);

        var filter = new PacketRequestDto()
        {
            DateTimeStart = startDate,
            DateTimeEnd = endDate,
            Limit = 5
        };

        await _filterRunner.StartAsync(filter, _cancellationTokenSource.Token);
        await _cancellationTokenSource.CancelAsync();

        // Make sure the runner has stopped.
        await WaitForRunnerToStopAsync(RunnerStopTimeout);

        _filterRunner.IsRunning.Should().BeFalse();
        stopped.Should().BeTrue();
    }

    [TestMethod]
    public async Task StopAsync_WhenRunning_StopsRunnerAndNotifies()
    {
        var stopped = false;
        _registrationToken += await _messenger.AnswerAsync<PacketWrapperDto, bool>(ConnectorContract.PushFilteredPacketsTopic(_filterRunnerId), (x) => true);
        _registrationToken += await _messenger.ListenAsync<ConnectorIdentifier>(ConnectorContract.FilterRunnerStoppedNotification(_filterRunnerId), (x) => stopped = true);

        var startDate = DateTime.Now.AddMinutes(-1);
        var endDate = DateTime.Now;

        await AddTestPacketsAsync(15, startDate, endDate);

        var filter = new PacketRequestDto()
        {
            DateTimeStart = startDate,
            DateTimeEnd = endDate,
            Limit = 5
        };

        await _filterRunner.StartAsync(filter, _cancellationTokenSource.Token);
        await _filterRunner.StopAsync(_cancellationTokenSource.Token);

        // Make sure the runner has stopped.
        await WaitForRunnerToStopAsync(RunnerStopTimeout);

        _filterRunner.IsRunning.Should().BeFalse();
        stopped.Should().BeTrue();
    }

    [TestMethod]
    public async Task StopAsync_WhenNotRunning_DoesNotNotify()
    {
        var stopped = false;
        _registrationToken += await _messenger.ListenAsync<ConnectorIdentifier>(ConnectorContract.FilterRunnerStoppedNotification(_filterRunnerId), (x) => stopped = true);

        await _filterRunner.StopAsync(_cancellationTokenSource.Token);

        // Make sure the runner has stopped.
        await WaitForRunnerToStopAsync(RunnerStopTimeout);

        stopped.Should().BeFalse();
    }

    [TestMethod]
    public async Task RunAsync_WhenAllPacketsFiltered_ExitsLoopAndNotifies()
    {
        var receivedPacketIds = new HashSet<long>();
        _registrationToken += await _messenger.AnswerAsync<PacketWrapperDto, bool>(ConnectorContract.PushFilteredPacketsTopic(_filterRunnerId), (x) =>
        {
            receivedPacketIds.UnionWith(x.Packets.Select(p => p.Id));
            return true;
        });

        var stopped = false;
        _registrationToken += await _messenger.ListenAsync<ConnectorIdentifier>(ConnectorContract.FilterRunnerStoppedNotification(_filterRunnerId), (x) => stopped = true);

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

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var filteredPackets = await context.Packet.Where(p => p.DateCreated >= filterStartDate && p.DateCreated <= filterEndDate).ToArrayAsync();
        var expectedPacketsIds = filteredPackets.Select(p => p.Id).ToHashSet();

        await _filterRunner.RunAsync(filter, _cancellationTokenSource.Token);

        _filterRunner.IsRunning.Should().BeFalse();
        stopped.Should().BeTrue();
        receivedPacketIds.Should().BeEquivalentTo(expectedPacketsIds);
    }

    [TestMethod]
    public async Task RunAsync_WhenPacketsLeftAfterLoopEnds_SendsRemainingPackets()
    {
        var receivedPacketIds = new HashSet<long>();
        var batchesSent = 0;

        _registrationToken += await _messenger.AnswerAsync<PacketWrapperDto, bool>(ConnectorContract.PushFilteredPacketsTopic(_filterRunnerId), (x) =>
        {
            receivedPacketIds.UnionWith(x.Packets.Select(p => p.Id));
            batchesSent++;
            return true;
        });

        var stopped = false;
        _registrationToken += await _messenger.ListenAsync<ConnectorIdentifier>(ConnectorContract.FilterRunnerStoppedNotification(_filterRunnerId), (x) => stopped = true);

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

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var filteredPackets = await context.Packet.Where(p => p.DateCreated >= filterStartDate && p.DateCreated <= filterEndDate).ToArrayAsync();
        var expectedPacketsIds = filteredPackets.Select(p => p.Id).ToHashSet();

        // The filter runner will send batches of 5 packets of 16 total.
        // This means there will be 3 full batches and 1 partial batch (4 total).
        var expectedBatchCount = 4;

        await _filterRunner.RunAsync(filter, _cancellationTokenSource.Token);

        _filterRunner.IsRunning.Should().BeFalse();
        stopped.Should().BeTrue();
        receivedPacketIds.Should().BeEquivalentTo(expectedPacketsIds);
        batchesSent.Should().Be(expectedBatchCount);
    }

    [TestMethod]
    public async Task RunAsync_WhenCancelled_ExitsLoopAndNotifies()
    {
        var stopped = false;
        _registrationToken += await _messenger.AnswerAsync<PacketWrapperDto, bool>(ConnectorContract.PushFilteredPacketsTopic(_filterRunnerId), (x) => true);
        _registrationToken += await _messenger.ListenAsync<ConnectorIdentifier>(ConnectorContract.FilterRunnerStoppedNotification(_filterRunnerId), (x) => stopped = true);

        var startDate = DateTime.Now.AddMinutes(-1);
        var endDate = DateTime.Now;

        await AddTestPacketsAsync(15, startDate, endDate);

        var filter = new PacketRequestDto()
        {
            DateTimeStart = startDate,
            DateTimeEnd = endDate,
            Limit = 5
        };

        var runnerTask = _filterRunner.RunAsync(filter, _cancellationTokenSource.Token);
        await _cancellationTokenSource.CancelAsync();

        // Make sure the runner has stopped.
        await WaitForTaskToCompleteAsync(runnerTask, RunnerStopTimeout);

        runnerTask.IsCompleted.Should().BeTrue();
        stopped.Should().BeTrue();
    }

    [TestMethod]
    public async Task RunAsync_WhenNoAcknowledge_ExitsLoopAndNotifies()
    {
        var stopped = false;
        _registrationToken += await _messenger.ListenAsync<ConnectorIdentifier>(ConnectorContract.FilterRunnerStoppedNotification(_filterRunnerId), (x) => stopped = true);

        var startDate = DateTime.Now.AddMinutes(-1);
        var endDate = DateTime.Now;

        await AddTestPacketsAsync(15, startDate, endDate);

        var filter = new PacketRequestDto()
        {
            DateTimeStart = startDate,
            DateTimeEnd = endDate,
            Limit = 5
        };

        var runnerTask = _filterRunner.RunAsync(filter, _cancellationTokenSource.Token);

        // Make sure the runner has stopped.
        await WaitForTaskToCompleteAsync(runnerTask, RunnerStopTimeout);

        runnerTask.IsCompleted.Should().BeTrue();
        stopped.Should().BeTrue();
    }

    [TestMethod]
    public async Task RunAsync_WhenNoPacketsQueried_ExitsLoopAndNotifies()
    {
        var stopped = false;
        _registrationToken += await _messenger.ListenAsync<ConnectorIdentifier>(ConnectorContract.FilterRunnerStoppedNotification(_filterRunnerId), (x) => stopped = true);

        var startDate = DateTime.Now.AddMinutes(-1);
        var endDate = DateTime.Now;
        var filter = new PacketRequestDto()
        {
            DateTimeStart = startDate,
            DateTimeEnd = endDate,
            Limit = 5
        };

        var runnerTask = _filterRunner.RunAsync(filter, _cancellationTokenSource.Token);

        // Make sure the runner has stopped.
        await WaitForTaskToCompleteAsync(runnerTask, RunnerStopTimeout);

        runnerTask.IsCompleted.Should().BeTrue();
        stopped.Should().BeTrue();
    }

    [TestMethod]
    public void RunAsync_CalledWithCancelledToken_ExitsImmediately()
    {
        var cancelledToken = new CancellationToken(true);
        var runnerTask = _filterRunner.RunAsync(new PacketRequestDto(), cancelledToken);

        runnerTask.IsCompleted.Should().BeTrue();
    }

    private async Task AddTestPacketsAsync(int count, DateTime startDate, DateTime endDate)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var random = new Random();

        // Get all values of the PacketStatus enum.
        var statuses = Enum.GetValues<PacketStatus>().Cast<PacketStatus>().ToArray();

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

    [TestCleanup]
    public async Task TestCleanup()
    {
        await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource.Dispose();
        await _registrationToken.DisposeAsync();
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }
}
