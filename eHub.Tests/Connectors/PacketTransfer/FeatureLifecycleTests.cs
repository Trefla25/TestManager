using System.Collections.Immutable;
using eHub.Config;
using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eHub.Database;
using eHub.PlugIn;
using eHub.Scripting.Connectors;
using eHub.Scripting.Connectors.Features;
using eHub.Scripting.Connectors.Metrics;
using eHub.Scripting.Connectors.Services;
using eHub.Tests.Helper;
using ElementLogic.Configuration.Client;
using eMessenger;
using eMessenger.Tests;
using ePlugin.Engine.Client;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace eHub.Tests.Connectors.PacketTransfer;

public class FeatureLifecycleTests : IAsyncLifetime
{
    private PacketTransferFeature _packetTransferFeature = null!;
    private SqliteConnection _connection = null!;
    private ConnectorMetadata _metadata = null!;
    private ConnectorTemplate _connectorTemplate = null!;
    private PacketDtoService _packetDtoService = null!;
    private PluginData _pluginData = null!;
    private CancellationTokenSource _cancellationTokenSource = null!;
    private IDbContextFactory<HubDbContext> _dbContextFactory = null!;
    private ILoggerFactory _loggerFactory = null!;
    private ILogger<PacketTransferFeature> _logger = null!;
    private IScopedMessenger _messenger = null!;
    private IPacketTransfer _packetTransfer = null!;
    private IFilterRunnerProvider _filterRunnerProvider = null!;
    private IEffortlessConfigurationRegistry _registry = null!;
    private ConnectorMetrics _metrics = null!;

    public async ValueTask InitializeAsync()
    {
        _cancellationTokenSource = new();
        _connection = new SqliteConnection(DbHelper.InMemoryConnectionString);
        await _connection.OpenAsync();

        _metadata = new ConnectorMetadata(new(), "MyTestConnector", "TestConnector");
        _connectorTemplate = new ConnectorTemplate() { PacketTransfer = new() };

        _pluginData = new PluginData("TestPlugin", [],
            new PluginFiles(new NullFileProvider(), new NullFileProvider(), new NullFileProvider(), new NullFileProvider(), new NullFileProvider()),
            NullLoggerFactory.Instance,
            System.Runtime.Loader.AssemblyLoadContext.Default,
            NullPluginScopeDependencyResolver.Instance);

        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = _loggerFactory.CreateLogger<PacketTransferFeature>();
        _packetTransfer = Substitute.For<IPacketTransfer>();
        _messenger = TestingMessenger.CreateScoped();
        _dbContextFactory = DbHelper.CreateInMemoryFactory<HubDbContext>(_connection);
        _filterRunnerProvider = Substitute.For<IFilterRunnerProvider>();
        _registry = Substitute.For<IEffortlessConfigurationRegistry>();
        _registry.AppConfigFolder.Returns(new DirectoryInfo("./"));
        _metrics = new ConnectorMetrics(Mocks.MeterFactory, _metadata);

        _packetDtoService = new PacketDtoService(NullLogger<PacketDtoService>.Instance, _packetTransfer, _metadata, _connectorTemplate);
    }

    [Fact]
    public async Task StartAsync_WhenConnectorStarted_RegistersTopics()
    {
        // Arrange
        _messenger = Substitute.For<IScopedMessenger>();
        _packetTransferFeature = new PacketTransferFeature(_logger, _dbContextFactory, _messenger, _packetTransfer, _filterRunnerProvider, _registry, _packetDtoService, _metadata, _connectorTemplate, _pluginData, _metrics);
        
        // Act
        await _packetTransferFeature.StartAsync(_cancellationTokenSource.Token);
        
        // Assert
        await _messenger.Received(1).AnswerAsync(
            Arg.Is(ConnectorContract.UIGetTopic(_metadata.ConnectorIdentifier)),
            Arg.Any<Func<ConnectorUiData>>());

        await _messenger.Received(1).AnswerAsync(
            Arg.Is(ConnectorContract.PollAliveConnectorsTopic()),
            Arg.Any<Func<ConnectorKeepAliveDto>>());

        await _messenger.Received(1).AnswerAsync(
            Arg.Is(ConnectorContract.UIPacketsGetTopic(_metadata.ConnectorIdentifier)),
            Arg.Any<Func<PacketRequestDto, ValueTask<PacketWrapperDto>>>());

        await _messenger.Received(1).ListenAsync(
            Arg.Is(ConnectorContract.PacketResendTopic(_metadata.ConnectorIdentifier)),
            Arg.Any<Func<ImmutableArray<PacketResendDto>, ValueTask>>());

        await _messenger.Received(1).ListenAsync(
            Arg.Is(ConnectorContract.PacketManualStopSequenceTopic(_metadata.ConnectorIdentifier)),
            Arg.Any<Func<ImmutableArray<long>, ValueTask>>());

        await _messenger.Received(1).ListenAsync(
            Arg.Is(ConnectorContract.PacketDeleteSequenceTopic(_metadata.ConnectorIdentifier)),
            Arg.Any<Func<ImmutableArray<long>, ValueTask>>());

        await _messenger.Received(1).AnswerAsync(
            Arg.Is(ConnectorContract.PacketExportTopic(_metadata.ConnectorIdentifier)),
            Arg.Any<Func<PacketRequestDto, ValueTask<ConnectorPacketsExportDto>>>());

        await _messenger.Received(1).AnswerAsync(
            Arg.Is(ConnectorContract.PacketTryImportTopic(_metadata.ConnectorIdentifier)),
            Arg.Any<Func<ConnectorPacketsTryImportDto, ValueTask<bool>>>());

        await _messenger.Received(1).ListenAsync(
            Arg.Is(ConnectorContract.StartFilterRunnerTopic(_metadata.ConnectorIdentifier)),
            Arg.Any<Func<FilterRunnerRequest, ValueTask>>());

        await _messenger.Received(1).AnswerAsync(
            Arg.Is(ConnectorContract.GetCustomFiltersTopic(_metadata.ConnectorIdentifier)),
            Arg.Any<Func<IReadOnlyDictionary<string, string>>>());

        await _messenger.Received(1).ListenAsync(
            Arg.Is(ConnectorContract.StopFilterRunnerTopic(_metadata.ConnectorIdentifier)),
            Arg.Any<Func<string, ValueTask>>());
    }

    [Fact]
    public async Task StartAsync_WhenConnectorStarted_SendsStartedNotification()
    {
        // Arrange
        ConnectorStartedEventDto? startedDto = null;

        await _messenger.ListenAsync<ConnectorStartedEventDto>(ConnectorContract.ConnectorStartedTopic(), (result) =>
        {
            startedDto = result;
        });

        _packetTransferFeature = new PacketTransferFeature(_logger, _dbContextFactory, _messenger, _packetTransfer, _filterRunnerProvider, _registry, _packetDtoService, _metadata, _connectorTemplate, _pluginData, _metrics);
        
        // Act
        await _packetTransferFeature.StartAsync(_cancellationTokenSource.Token);
        
        // Assert
        startedDto.Should().NotBeNull();
        startedDto.Identifier.Should().Be(_metadata.ConnectorIdentifier);
        startedDto.Ui.ConnectorName.Should().Be(_metadata.TemplateName);
        startedDto.Ui.ConnectorType.Should().Be(_metadata.ConnectorType);
        startedDto.Ui.UIViewConfig.Should().NotBeNull();
    }

    [Fact]
    public async Task StartAsync_WhenCalledTwice_FailsSecond()
    {
        // Arrange
        _packetTransferFeature = new PacketTransferFeature(_logger, _dbContextFactory, _messenger, _packetTransfer, _filterRunnerProvider, _registry, _packetDtoService, _metadata, _connectorTemplate, _pluginData, _metrics);

        // Act
        await _packetTransferFeature.StartAsync(_cancellationTokenSource.Token);
        var act = async () => await _packetTransferFeature.StartAsync(_cancellationTokenSource.Token);
        
        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Packet Transfer Feature already started*");
    }

    [Fact]
    public async Task StartAsync_WhenFails_DoesNotRegisterUITopics()
    {
        // Arrange
        _messenger = Substitute.For<IScopedMessenger>();

        // Intentionally fail the connector initialization
        _dbContextFactory = Substitute.For<IDbContextFactory<HubDbContext>>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new Exception("Expected Exception"));

        _packetTransferFeature = new PacketTransferFeature(_logger, _dbContextFactory, _messenger, _packetTransfer, _filterRunnerProvider, _registry, _packetDtoService, _metadata, _connectorTemplate, _pluginData, _metrics);

        // Act
        try
        {
            await _packetTransferFeature.StartAsync(_cancellationTokenSource.Token);
        }
        catch { }

        await _messenger.DidNotReceive().AnswerAsync(
            Arg.Is(ConnectorContract.UIGetTopic(_metadata.ConnectorIdentifier)),
            Arg.Any<Func<ValueTask<ConnectorUiData>>>());

        await _messenger.DidNotReceive().AnswerAsync(
            Arg.Is(ConnectorContract.PollAliveConnectorsTopic()),
            Arg.Any<Func<ValueTask<ConnectorKeepAliveDto>>>());

        await _messenger.DidNotReceive().AnswerAsync(
            Arg.Is(ConnectorContract.UIPacketsGetTopic(_metadata.ConnectorIdentifier)),
            Arg.Any<Func<PacketRequestDto, ValueTask<PacketWrapperDto>>>());

        await _messenger.DidNotReceive().ListenAsync(
            Arg.Is(ConnectorContract.PacketResendTopic(_metadata.ConnectorIdentifier)),
            Arg.Any<Func<ImmutableArray<PacketResendDto>, ValueTask>>());

        await _messenger.DidNotReceive().ListenAsync(
            Arg.Is(ConnectorContract.PacketManualStopSequenceTopic(_metadata.ConnectorIdentifier)),
            Arg.Any<Func<ImmutableArray<long>, ValueTask>>());

        await _messenger.DidNotReceive().ListenAsync(
            Arg.Is(ConnectorContract.PacketDeleteSequenceTopic(_metadata.ConnectorIdentifier)),
            Arg.Any<Func<ImmutableArray<long>, ValueTask>>());

        await _messenger.DidNotReceive().AnswerAsync(
            Arg.Is(ConnectorContract.PacketExportTopic(_metadata.ConnectorIdentifier)),
            Arg.Any<Func<PacketRequestDto, ValueTask<ConnectorPacketsExportDto>>>());

        await _messenger.DidNotReceive().AnswerAsync(
            Arg.Is(ConnectorContract.PacketTryImportTopic(_metadata.ConnectorIdentifier)),
            Arg.Any<Func<ConnectorPacketsTryImportDto, ValueTask<bool>>>());
        await _messenger.DidNotReceive().ListenAsync(
            Arg.Is(ConnectorContract.StartFilterRunnerTopic(_metadata.ConnectorIdentifier)),
            Arg.Any<Func<FilterRunnerRequest, ValueTask>>());
        // Assert
        await _messenger.DidNotReceive().ListenAsync(
            Arg.Is(ConnectorContract.StopFilterRunnerTopic(_metadata.ConnectorIdentifier)),
            Arg.Any<Func<string, ValueTask>>());
    }

    [Fact]
    public async Task DisposeAsync_WhenCalled_SendsStoppedNotification()
    {
        // Arrange
        ConnectorStoppedEventDto? stoppedDto = null;

        await _messenger.ListenAsync<ConnectorStoppedEventDto>(ConnectorContract.ConnectorStoppedTopic(), (result) =>
        {
            stoppedDto = result;
        });

        _packetTransferFeature = new PacketTransferFeature(_logger, _dbContextFactory, _messenger, _packetTransfer, _filterRunnerProvider, _registry, _packetDtoService, _metadata, _connectorTemplate, _pluginData, _metrics);
        
        // Act
        await _packetTransferFeature.StartAsync(_cancellationTokenSource.Token);
        await _packetTransferFeature.DisposeAsync();
        
        // Assert
        stoppedDto.Should().NotBeNull();
        stoppedDto.Identifier.Should().Be(_metadata.ConnectorIdentifier);
    }

    public async ValueTask DisposeAsync()
    {
        await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource.Dispose();

        await _packetTransferFeature.DisposeAsync();

        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }
}
