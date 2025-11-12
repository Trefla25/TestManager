using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Loader;
using System.Text;
using eHub.Config;
using eHub.Database;
using eHub.PlugIn;
using eHub.Scripting.Connectors;
using eHub.Scripting.Connectors.Features;
using eHub.Scripting.Connectors.Metrics;
using eHub.Scripting.Connectors.Services;
using eController.Util;
using eHub.Tests.Helper;
using ElementLogic.Configuration.Client;
using eMessenger.Tests;
using ePlugin.Engine.Client;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit.Sdk;

namespace eHub.Tests.Connectors.PacketTransfer;

public class PacketDispatcherTests
{
    private const int TestTimeout = 100_000;
    private const int WaitForElementsTimeout = TestTimeout / 2;

    private static readonly PluginFiles EmptyPluginFiles = new(new NullFileProvider(), new NullFileProvider(), new NullFileProvider(), new NullFileProvider(), new NullFileProvider());
    private readonly PluginData _pluginData = new("DefaultTest", [], EmptyPluginFiles, NullLoggerFactory.Instance, AssemblyLoadContext.Default, NullPluginScopeDependencyResolver.Instance);

    private readonly CancellationTokenSource _cts;
    private AwaitablePacketsConnector? _testConnector;
    private PacketTransferFeature? _packetTransferCore;
    private Task? _ptcWork;
    private readonly string _connectionString;

    public PacketDispatcherTests()
    {
        _cts = new CancellationTokenSource();

        var runId = Guid.NewGuid().ToString();
        _connectionString = $"Data Source={runId};Mode=Memory;Cache=Shared";
    }

    [MemberNotNull(nameof(_testConnector), nameof(_packetTransferCore), nameof(_ptcWork))]
    private void SetupTransfer(ChannelMode mode)
    {
        // We need at least one connection to the database to keep it alive
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        _cts.Token.Register(connection.Dispose);

        var registry = Substitute.For<IEffortlessConfigurationRegistry>();
        registry.AppConfigFolder.Returns(new DirectoryInfo("."));

        var testServices = new ServiceCollection()
            .AddLogging(o => o
                .ClearProviders()
                .AddSimpleConsole(opt => opt.TimestampFormat = "mm:ss.fff ")
                .SetMinimumLevel(LogLevel.Debug)
                .AddFilter((ns, lvl) => !ns!.StartsWith("Microsoft.EntityFrameworkCore") || lvl > LogLevel.Information)
                )
            .AddDbContextFactory<HubDbContext>(o => o.UseSqlite(_connectionString))
            .BuildServiceProvider();
        var messenger = TestingMessenger.CreateScoped(NullLoggerFactory.Instance);
        _testConnector = new AwaitablePacketsConnector();
        var dbContextFactory = testServices.GetRequiredService<IDbContextFactory<HubDbContext>>();
        var metadata = new ConnectorMetadata(new(), "TestConnector", "TestConnector");
        var connectorTemplate = new ConnectorTemplate()
        {
            Enabled = true,
            Type = "TestConnector",
            PacketTransfer = new()
            {
                DbPath = connection.DataSource,
                ChannelGroups = {
                            {
                                "Test", new ChannelGroup()
                                {
                                    Mode = mode,
                                    Channels = [ "A" ],
                                    DbPollInterval = TimeSpan.FromSeconds(100),
                                    PacketsPerCycle = mode == ChannelMode.Concurrent ? 10 : 1
                                }
                            }
                        }
            }
        };

        var filterRunnerProvider = Substitute.For<IFilterRunnerProvider>();
        var packetDtoService = new PacketDtoService(NullLogger<PacketDtoService>.Instance, _testConnector, metadata, connectorTemplate);
        var metrics = new ConnectorMetrics(Mocks.MeterFactory, metadata);

        _packetTransferCore = new PacketTransferFeature(
                testServices.GetRequiredService<ILogger<PacketTransferFeature>>(),
                dbContextFactory,
                messenger,
                _testConnector,
                filterRunnerProvider,
                registry,
                packetDtoService,
                metadata,
                connectorTemplate,
                _pluginData,
                metrics
            );

        _ptcWork = _packetTransferCore.StartAsync(_cts.Token);
    }

    private async Task WaitForElementsAsync(int count)
    {
        var sw = Stopwatch.StartNew();
        while (_testConnector!.ProcessRun.Count < count)
        {
            if (sw.Elapsed > TimeSpan.FromMilliseconds(WaitForElementsTimeout))
            {
                throw new XunitException($"Timeout waiting for {count} elements");
            }
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }
    }

    [Fact(Timeout = TestTimeout)]
    public async Task TestSequentialPacketMode()
    {
        // Arrange
        SetupTransfer(ChannelMode.Sequential);

        // Act
        Console.WriteLine("Adding packets");
        await _testConnector.SimulatePacket("1", "A");
        await _testConnector.SimulatePacket("2", "A");
        await _testConnector.SimulatePacket("3", "A");

        Console.WriteLine("Process pkg 1");
        await WaitForElementsAsync(1);
        
        // Assert
        _testConnector.ProcessRun.ToArray().Should().BeEquivalentTo(new[] { "1" });

        Console.WriteLine("Process pkg 2");
        _testConnector.ProcessedPackets["1"].SetResult();
        await WaitForElementsAsync(2);
        _testConnector.ProcessRun.ToArray().Should().BeEquivalentTo(new[] { "1", "2" });

        Console.WriteLine("Process pkg 3");
        _testConnector.ProcessedPackets["2"].SetResult();
        await WaitForElementsAsync(3);
        _testConnector.ProcessRun.ToArray().Should().BeEquivalentTo(new[] { "1", "2", "3" });

        _testConnector.ProcessedPackets["3"].SetResult();

        await Task.Delay(TimeSpan.FromSeconds(0.1), _cts.Token);

        await _cts.CancelAsync();
        await _ptcWork;
    }


    [Fact(Timeout = TestTimeout)]
    public async Task TestSequentialPacketModeReverse()
    {
        // Arrange
        SetupTransfer(ChannelMode.Sequential);

        // Act
        Console.WriteLine("Adding packets");
        await _testConnector.SimulatePacket("1", "A");
        await _testConnector.SimulatePacket("2", "A");
        await _testConnector.SimulatePacket("3", "A");
        // Complete in reverse order to test if the order is maintained

        await WaitForElementsAsync(1);
        
        // Assert
        _testConnector.ProcessRun.ToArray().Should().BeEquivalentTo(new[] { "1" });

        Console.WriteLine("Trigger process 2 & 3");
        _testConnector.ProcessedPackets["3"].SetResult();
        _testConnector.ProcessedPackets["2"].SetResult();
        await Task.Delay(TimeSpan.FromSeconds(0.1));
        _testConnector.ProcessRun.ToArray().Should().BeEquivalentTo(new[] { "1" });

        Console.WriteLine("Trigger process 1");
        _testConnector.ProcessedPackets["1"].SetResult();
        await WaitForElementsAsync(3);
        _testConnector.ProcessRun.ToArray().Should().BeEquivalentTo(new[] { "1", "2", "3" });

        await _cts.CancelAsync();
        await _ptcWork;
    }


    [Fact(Timeout = TestTimeout)]
    public async Task TestConcurrentPacketMode()
    {
        // Arrange
        SetupTransfer(ChannelMode.Concurrent);

        // Act
        Console.WriteLine("Adding packets");
        await _testConnector.SimulatePacket("1", "A");
        await _testConnector.SimulatePacket("2", "A");
        await _testConnector.SimulatePacket("3", "A");
        await _testConnector.SimulatePacket("4", "A");
        // All 4 packets should be processed at the same time

        await WaitForElementsAsync(4);
        
        // Assert
        _testConnector.ProcessRun.ToArray().Should().BeEquivalentTo(new[] { "1", "2", "3", "4" });

        _testConnector.ProcessedPackets["1"].SetResult();
        _testConnector.ProcessedPackets["2"].SetResult();
        _testConnector.ProcessedPackets["3"].SetResult();
        _testConnector.ProcessedPackets["4"].SetResult();

        await WaitForElementsAsync(4);
        await Task.Delay(TimeSpan.FromSeconds(0.1), _cts.Token);
        _testConnector.ProcessRun.ToArray().Should().BeEquivalentTo(new[] { "1", "2", "3", "4" });

        // TODO we should also test if all packets have been properly written to the database

        await _cts.CancelAsync();
        await _ptcWork;
    }


    [Fact(Timeout = TestTimeout)]
    public async Task TestConcurrentPacketModeMaxParallelism()
    {
        // Arrange
        SetupTransfer(ChannelMode.Concurrent);

        // Act
        Console.WriteLine("Adding packets");
        for (var i = 0; i < 20; i++)
        {
            await _testConnector.SimulatePacket(i.ToString(), "A");
        }

        // We should have 10 packets processed at the same time
        await WaitForElementsAsync(10);
        await Task.Delay(TimeSpan.FromSeconds(0.1), _cts.Token);
        
        // Assert
        _testConnector.ProcessRun.Count.Should().Be(10);

        // Complete first 5 packets

        for (var i = 0; i < 5; i++)
        {
            _testConnector.ProcessedPackets[i.ToString()].SetResult();
        }

        // We should have 15 packets processed at the same time
        await WaitForElementsAsync(15);
        await Task.Delay(TimeSpan.FromSeconds(0.1), _cts.Token);
        _testConnector.ProcessRun.Count.Should().Be(15);

        // Complete the rest of the packets

        for (var i = 5; i < 20; i++)
        {
            _testConnector.ProcessedPackets[i.ToString()].SetResult();
        }

        // We should have all 20 packets processed
        await WaitForElementsAsync(20);
        await Task.Delay(TimeSpan.FromSeconds(0.1), _cts.Token);
        _testConnector.ProcessRun.Count.Should().Be(20);

        await _cts.CancelAsync();
        await _ptcWork;
    }


    [Fact(Timeout = TestTimeout)]
    public async Task TestWithInnerDbAccess()
    {
        // Arrange
        SetupTransfer(ChannelMode.Sequential);

        // Act
        _testConnector.SimulateProcess += async (packet, cancellationToken) =>
        {
            var name = Encoding.UTF8.GetString(packet.BinaryData.Span);
            _testConnector.ProcessedPackets[name].SetResult();

            if (!name.EndsWith('b'))
            {
                await _testConnector.SimulatePacket(name + "b", "A");
            }
        };

        Console.WriteLine("Adding packets");
        await _testConnector.SimulatePacket("1", "A");
        await WaitForElementsAsync(2);
        
        // Assert
        _testConnector.ProcessRun.ToArray().Should().BeEquivalentTo(new[] { "1", "1b" });
    }
}

internal class AwaitablePacketsConnector : IPacketTransfer
{
    public AwaitablePacketsConnector()
    {
    }

    public IPacketConverter Converter { get; } = new StringConverter();
    public IPacketRepository PacketRepository { get; set; } = default!;

    public event UpdateStatusDelegate? UpdateStatus;

    public Dictionary<string, TaskCompletionSource> ProcessedPackets { get; } = [];

    public ConcurrentBag<string> ProcessRun { get; } = [];

    public async Task SimulatePacket(string name, string channel)
    {
        var data = Encoding.UTF8.GetBytes(name);
        ProcessedPackets.Add(name, new TaskCompletionSource());
        await PacketRepository.Create().Add(new PacketData(data, channel)).CreateAsync();
    }

    public Func<PacketData, CancellationToken, Task> SimulateProcess { get; set; } = (p, ct) => Task.CompletedTask;

    public async ValueTask<ProcessPacketState> ProcessPacket(PacketData packet, CancellationToken cancellationToken)
    {
        var name = Encoding.UTF8.GetString(packet.BinaryData.Span);
        ProcessRun.Add(name);
        await SimulateProcess.Invoke(packet, cancellationToken);
        await ProcessedPackets[name].Task;
        return ProcessPacketState.Success;
    }

    public Task Run(CancellationToken cancellationToken) => TaskUtil.DelayNoexept(Timeout.InfiniteTimeSpan, cancellationToken);
}
