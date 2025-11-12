using eHub.Config;
using eHub.Database;
using eHub.PlugIn;
using eHub.Scripting.Connectors.Features;
using eHub.Scripting.Connectors.Services;
using eHub.Scripting.Connectors;
using ElementLogic.Configuration.Client;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ePlugin.Engine.Client;
using eMessenger;
using eMessenger.Tests;
using eHub.Contracts;
using FluentAssertions;
using eHub.Contracts.UIConfig;
using eHub.Database.Models;
using System.Collections.Immutable;
using System.Text;
using eHub.PlugIn.UI;
using eHub.Contracts.Helper;
using eHub.Scripting.Connectors.Metrics;
using eHub.Tests.Helper;

namespace eHub.Tests.Connectors.PacketTransfer;

public class FeatureActionTests : IAsyncLifetime
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
    private IPacketFilterConfigurator _packetTransfer = null!;
    private IPacketConverter _packetConverter = null!;
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

        // Set big polling interval to avoid packet processing
        _connectorTemplate.PacketTransfer.ChannelGroups.Add("ResendGroup", new ChannelGroup() { Channels = ["TestChannel"], DbPollInterval = TimeSpan.FromHours(1) });
        _connectorTemplate.PacketTransfer.ChannelGroups.Add("NoResendGroup", new ChannelGroup() { Channels = ["NoResendChannel"], CanResend = false, DbPollInterval = TimeSpan.FromHours(1) });

        _pluginData = new PluginData("TestPlugin", [],
            new PluginFiles(new NullFileProvider(), new NullFileProvider(), new NullFileProvider(), new NullFileProvider(), new NullFileProvider()),
            NullLoggerFactory.Instance,
            System.Runtime.Loader.AssemblyLoadContext.Default,
            NullPluginScopeDependencyResolver.Instance);

        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = _loggerFactory.CreateLogger<PacketTransferFeature>();
        _packetTransfer = Substitute.For<IPacketFilterConfigurator>();
        _packetConverter = Substitute.For<IPacketConverter>();
        _packetTransfer.Converter.Returns(_packetConverter);
        _messenger = TestingMessenger.CreateScoped();
        _dbContextFactory = DbHelper.CreateInMemoryFactory<HubDbContext>(_connection);
        _filterRunnerProvider = Substitute.For<IFilterRunnerProvider>();
        _registry = Substitute.For<IEffortlessConfigurationRegistry>();
        _registry.AppConfigFolder.Returns(new DirectoryInfo("./"));
        _metrics = new ConnectorMetrics(Mocks.MeterFactory, _metadata);

        _packetTransfer.ConfigureCustomFilters(Arg.Do<ICustomFilterBuilder>(builder =>
        {
            builder.AddCustomFilter<string>("MyCustomFilter", "SUBSTRING(BinaryData, 1, 4)");
        }));

        // Make sure the packets are not changed during processing
        _packetTransfer.ProcessPacket(Arg.Any<PacketData>(), Arg.Any<CancellationToken>()).Returns(ProcessPacketState.RetryUnchanged);

        _packetDtoService = new PacketDtoService(NullLogger<PacketDtoService>.Instance, _packetTransfer, _metadata, _connectorTemplate);
        _packetTransferFeature = new PacketTransferFeature(_logger, _dbContextFactory, _messenger, _packetTransfer, _filterRunnerProvider, _registry, _packetDtoService, _metadata, _connectorTemplate, _pluginData, _metrics);

        await _packetTransferFeature.StartAsync(_cancellationTokenSource.Token);
    }

    [Fact]
    public async Task PollAliveConnectors_WhenCalled_ReturnsKeepAliveDto()
    {
        // Arrange
        _packetTransferFeature = new PacketTransferFeature(_logger, _dbContextFactory, _messenger, _packetTransfer, _filterRunnerProvider, _registry, _packetDtoService, _metadata, _connectorTemplate, _pluginData, _metrics);
        await _packetTransferFeature.StartAsync(_cancellationTokenSource.Token);
        
        // Act
        var response = await _messenger
            .AskAsync<ConnectorKeepAliveDto>(ConnectorContract.PollAliveConnectorsTopic())
            .FirstOrDefaultResponse();
        
        // Assert
        response.Should().NotBeNull();
        response.Identifier.Should().Be(_metadata.ConnectorIdentifier);
    }

    [Fact]
    public async Task UIGet_WhenCalled_ReturnsConnectorUIData()
    {
        // Act
        var response = await _messenger
            .AskAsync<ConnectorUiData>(ConnectorContract.UIGetTopic(_metadata.ConnectorIdentifier))
            .FirstOrDefaultResponse();
        
        // Assert
        response.Should().NotBeNull();
        response.ConnectorName.Should().Be(_metadata.TemplateName);
        response.ConnectorType.Should().Be(_metadata.ConnectorType);
        response.UIViewConfig.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCustomFilters_WhenCalled_ReturnsCustomFilters()
    {
        // Act
        var response = await _messenger
            .AskAsync<IReadOnlyDictionary<string, string>>(ConnectorContract.GetCustomFiltersTopic(_metadata.ConnectorIdentifier))
            .FirstOrDefaultResponse();
        
        // Assert
        response.Should().NotBeNull();
        response.Should().ContainKey("MyCustomFilter");
        response["MyCustomFilter"].Should().Be(typeof(string).ToString());
    }

    [Fact]
    public async Task GetPackets_WhenDatabaseIsEmpty_ReturnsEmpty()
    {
        // Arrange
        var filter = new PacketRequestDto(
            DateTimeStart: DateTime.MinValue,
            DateTimeEnd: DateTime.MaxValue);

        var packetsGetTopic = ConnectorContract.UIPacketsGetTopic(_metadata.ConnectorIdentifier);

        // Act
        var packetWrapper = await _messenger
            .AskAsync<PacketRequestDto, PacketWrapperDto>(packetsGetTopic, filter)
            .FirstOrDefaultResponse();
        
        // Assert
        packetWrapper.Should().NotBeNull();
        packetWrapper.Packets.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPackets_WithValidDateRange_ReturnsPackets()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync(_cancellationTokenSource.Token);
        await context.AddTestPacketsAsync(10, dateTimeNow.AddHours(-2), dateTimeNow);

        _packetConverter.PacketToUIDataConverter(Arg.Any<PacketData>(), Arg.Any<CancellationToken>()).Returns(x =>
        {
            var packet = x.ArgAt<PacketData>(0);
            var data = Encoding.UTF8.GetString(packet.BinaryData.Span).Remove(0, 6);
            return new UIConversionInfo(data, $"{data}Preview", UIDataTypes.Plaintext);
        });

        var filter = new PacketRequestDto(
            DateTimeStart: dateTimeNow.AddHours(-2),
            DateTimeEnd: dateTimeNow.AddHours(-1));

        var filteredPackets = await context.Packet.AsNoTracking()
            .Where(p => p.DateCreated >= filter.DateTimeStart && p.DateCreated <= filter.DateTimeEnd)
            .ToArrayAsync(_cancellationTokenSource.Token);

        var expectedPackets = await Task.WhenAll(filteredPackets.Select(async p => await _packetDtoService.BuildDtoAsync(p)));

        var packetsGetTopic = ConnectorContract.UIPacketsGetTopic(_metadata.ConnectorIdentifier);
        
        // Act
        var packetWrapper = await _messenger
            .AskAsync<PacketRequestDto, PacketWrapperDto>(packetsGetTopic, filter)
            .FirstOrDefaultResponse();
        
        // Assert
        packetWrapper.Should().NotBeNull();
        packetWrapper.Packets.Should().BeEquivalentTo(expectedPackets);
    }

    [Fact]
    public async Task GetPackets_WithColumnFilters_ReturnsPackets()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync(_cancellationTokenSource.Token);
        await context.AddTestPacketsAsync(20, dateTimeNow.AddHours(-2), dateTimeNow.AddHours(-1), ["TestChannel1", "TestChannel2"]);
        await context.Packet.AddAsync(new Packet
        {
            BinaryData = Encoding.UTF8.GetBytes("NotFiltered"),
            Channel = "TestChannel1",
            DateCreated = dateTimeNow,
            Status = PacketStatus.Enqueued
        }, _cancellationTokenSource.Token);

        await context.SaveChangesAsync(_cancellationTokenSource.Token);

        _packetConverter.PacketToUIDataConverter(Arg.Any<PacketData>(), Arg.Any<CancellationToken>()).Returns(x =>
        {
            var packet = x.ArgAt<PacketData>(0);
            var data = Encoding.UTF8.GetString(packet.BinaryData.Span).Remove(0, 6);

            return packet.Id switch
            {
                < 5 => new UIConversionInfo(data, $"Preview{data}", UIDataTypes.Plaintext),
                < 10 => new UIConversionInfo("InvalidData", $"{data}Preview", UIDataTypes.Plaintext),
                _ => new UIConversionInfo(data, $"{data}Preview", UIDataTypes.Plaintext)
            };
        });

        var columnFilters = new ColumnFilterDto[]
        {
            new(nameof(PacketDto.Data), ColumnFilterOperator.StartsWith, "Data"),
            new(nameof(PacketDto.PreviewData), ColumnFilterOperator.EndsWith, "Preview"),
            new(nameof(PacketDto.Channel), ColumnFilterOperator.Contains, "Channel1"),
            new(nameof(PacketDto.Status), ColumnFilterOperator.Equal, nameof(PacketStatus.Enqueued))
        };

        var filter = new PacketRequestDto(ColumnFilters: [.. columnFilters]);

        var filteredPackets = await context.Packet.AsNoTracking()
            .Where(p => p.Channel.Contains("Channel1") && p.Status == PacketStatus.Enqueued)
            .ToArrayAsync(_cancellationTokenSource.Token);

        var packetsDto = await Task.WhenAll(filteredPackets.Select(async p => await _packetDtoService.BuildDtoAsync(p)));
        var expectedPackets = packetsDto
            .Where(p => columnFilters.Where(f => f.ColumnName == nameof(PacketDto.Data)).ToArray().Matches(p.Data))
            .Where(p => columnFilters.Where(f => f.ColumnName == nameof(PacketDto.PreviewData)).ToArray().Matches(p.PreviewData))
            .ToArray();

        var packetsGetTopic = ConnectorContract.UIPacketsGetTopic(_metadata.ConnectorIdentifier);
        
        // Act
        var packetWrapper = await _messenger
            .AskAsync<PacketRequestDto, PacketWrapperDto>(packetsGetTopic, filter)
            .FirstOrDefaultResponse();
        
        // Assert
        packetWrapper.Should().NotBeNull();
        packetWrapper.Packets.Should().BeEquivalentTo(expectedPackets);
    }

    [Fact]
    public async Task GetPackets_WhenNoMatch_ReturnsEmpty()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;
        await using var context = await _dbContextFactory.CreateDbContextAsync(_cancellationTokenSource.Token);
        await context.AddTestPacketsAsync(10, dateTimeNow.AddHours(-3), dateTimeNow.AddHours(-2));

        var filter = new PacketRequestDto(
            DateTimeStart: dateTimeNow.AddHours(-1),
            DateTimeEnd: dateTimeNow);

        var packetsGetTopic = ConnectorContract.UIPacketsGetTopic(_metadata.ConnectorIdentifier);
        
        // Act
        var packetWrapper = await _messenger
            .AskAsync<PacketRequestDto, PacketWrapperDto>(packetsGetTopic, filter)
            .FirstOrDefaultResponse();
        
        // Assert
        packetWrapper.Should().NotBeNull();
        packetWrapper.Packets.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPackets_WithInvalidDateRange_ReturnsEmpty()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;
        await using var context = await _dbContextFactory.CreateDbContextAsync(_cancellationTokenSource.Token);
        await context.AddTestPacketsAsync(10, dateTimeNow.AddHours(-3), dateTimeNow.AddHours(-2));

        var filter = new PacketRequestDto(
            DateTimeStart: dateTimeNow,
            DateTimeEnd: dateTimeNow.AddHours(-1));

        var packetsGetTopic = ConnectorContract.UIPacketsGetTopic(_metadata.ConnectorIdentifier);
        
        // Act
        var packetWrapper = await _messenger
            .AskAsync<PacketRequestDto, PacketWrapperDto>(packetsGetTopic, filter)
            .FirstOrDefaultResponse();
        
        // Assert
        packetWrapper.Should().NotBeNull();
        packetWrapper.Packets.Should().BeEmpty();
    }

    [Fact]
    public async Task Resend_WhenDbEmpty_DoesntResend()
    {
        // Arrange
        var nonExistent = ImmutableArray.Create(new PacketResendDto(42));

        // Act
        await _messenger.SendAsync(ConnectorContract.PacketResendTopic(_metadata.ConnectorIdentifier), nonExistent);

        await using var context = await _dbContextFactory.CreateDbContextAsync(_cancellationTokenSource.Token);
        var packets = await context.Packet.ToArrayAsync(_cancellationTokenSource.Token);
        
        // Assert
        packets.Should().BeEmpty();
    }

    [Fact]
    public async Task Resend_WithSameData_InsertsIdenticalPacket()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(_cancellationTokenSource.Token);

        await context.Packet.AddAsync(new Packet
        {
            BinaryData = Encoding.UTF8.GetBytes("SameData"),
            Channel = "TestChannel",
            DateCreated = DateTime.Now.AddHours(-2),
            DateChanged = DateTime.Now.AddHours(-1),
            Status = PacketStatus.Processed,
            ParentId = 100,
            RetryCount = 1,
            Metadata = "Metadata",
            DynamicField = "DynamicField"
        }, _cancellationTokenSource.Token);

        await context.SaveChangesAsync(_cancellationTokenSource.Token);

        var request = ImmutableArray.Create(new PacketResendDto(1));
        var requestDateTime = DateTime.Now;
        
        // Act
        await _messenger.SendAsync(ConnectorContract.PacketResendTopic(_metadata.ConnectorIdentifier), request);

        var packets = await context.Packet.AsNoTracking().ToArrayAsync(_cancellationTokenSource.Token);
        var oldPacket = packets.Single(p => p.Id == 1);
        var newPacket = packets.Single(p => p.Id == 2);
        
        // Assert
        packets.Should().HaveCount(2);

        newPacket.BinaryData.Should().BeEquivalentTo(oldPacket.BinaryData);
        newPacket.Channel.Should().Be(oldPacket.Channel);
        newPacket.DateCreated.Should().BeCloseTo(requestDateTime, TimeSpan.FromMilliseconds(500));
        newPacket.DateChanged.Should().BeNull();
        newPacket.Status.Should().Be(PacketStatus.Enqueued);
        newPacket.ParentId.Should().Be(oldPacket.ParentId);
        newPacket.RetryCount.Should().Be(0);
        newPacket.Metadata.Should().Be(oldPacket.Metadata);
        newPacket.DynamicField.Should().Be(oldPacket.DynamicField);
    }

    [Fact]
    public async Task Resend_WithNewData_InsertsPacketWithNewData()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(_cancellationTokenSource.Token);

        await context.Packet.AddAsync(new Packet
        {
            BinaryData = Encoding.UTF8.GetBytes("OldData"),
            Channel = "TestChannel",
            DateCreated = DateTime.Now.AddHours(-2),
            DateChanged = DateTime.Now.AddHours(-1),
            Status = PacketStatus.Processed,
            ParentId = 100,
            RetryCount = 1,
            Metadata = "Metadata",
            DynamicField = "DynamicField"
        }, _cancellationTokenSource.Token);

        await context.SaveChangesAsync(_cancellationTokenSource.Token);

        var uiConversion = new UIConversionInfo("OldData", "DataPreview", UIDataTypes.Plaintext);
        _packetConverter.PacketToUIDataConverter(Arg.Any<PacketData>(), Arg.Any<CancellationToken>())
            .Returns(uiConversion);

        uiConversion.Data = "NewData";
        _packetConverter.UIToBinaryDataConverter(Arg.Is(uiConversion), Arg.Is("Metadata"), Arg.Any<CancellationToken>())
            .Returns(Encoding.UTF8.GetBytes("NewBinaryData"));

        var request = ImmutableArray.Create(new PacketResendDto(1, "NewData"));
        var requestDateTime = DateTime.Now;
        
        // Act
        await _messenger.SendAsync(ConnectorContract.PacketResendTopic(_metadata.ConnectorIdentifier), request);

        var packets = await context.Packet.AsNoTracking().ToArrayAsync(_cancellationTokenSource.Token);
        var oldPacket = packets.Single(p => p.Id == 1);
        var newPacket = packets.Single(p => p.Id == 2);
        
        // Assert
        packets.Should().HaveCount(2);

        newPacket.BinaryData.Should().BeEquivalentTo(Encoding.UTF8.GetBytes("NewBinaryData"));
        newPacket.Channel.Should().Be(oldPacket.Channel);
        newPacket.DateCreated.Should().BeCloseTo(requestDateTime, TimeSpan.FromSeconds(1));
        newPacket.DateChanged.Should().BeNull();
        newPacket.Status.Should().Be(PacketStatus.Enqueued);
        newPacket.ParentId.Should().Be(oldPacket.ParentId);
        newPacket.RetryCount.Should().Be(0);
        newPacket.Metadata.Should().Be(oldPacket.Metadata);
        newPacket.DynamicField.Should().Be(oldPacket.DynamicField);
    }

    [Fact]
    public async Task Resend_WithMultiplePackets_InsertsPackets()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(_cancellationTokenSource.Token);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddHours(-2), DateTime.Now.AddHours(-1));

        var request = ImmutableArray.Create(
            new PacketResendDto(2),
            new PacketResendDto(5, "NewData5"),
            new PacketResendDto(8)
        );

        var uiConversion = new UIConversionInfo("BinaryData5", "DataPreview", UIDataTypes.Plaintext);
        _packetConverter.PacketToUIDataConverter(Arg.Is<PacketData>(p => p.Id == 5), Arg.Any<CancellationToken>())
            .Returns(uiConversion);

        uiConversion.Data = "NewData5";
        _packetConverter.UIToBinaryDataConverter(Arg.Is(uiConversion), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Encoding.UTF8.GetBytes("NewBinaryData5"));

        var requestDateTime = DateTime.Now;
        
        // Act
        await _messenger.SendAsync(ConnectorContract.PacketResendTopic(_metadata.ConnectorIdentifier), request);

        var packets = await context.Packet.AsNoTracking().OrderBy(p => p.Id).ToArrayAsync(_cancellationTokenSource.Token);
        var newPackets = packets.Where(p => p.Id > 10).ToArray();
        
        // Assert
        newPackets.Should().HaveCount(3);
        newPackets.Should().OnlyContain(p => p.Status == PacketStatus.Enqueued);
        newPackets.ForEach(p => p.DateCreated.Should().BeCloseTo(requestDateTime, TimeSpan.FromSeconds(1)));

        newPackets[0].BinaryData.Should().BeEquivalentTo(packets[1].BinaryData);
        newPackets[1].BinaryData.Should().BeEquivalentTo(Encoding.UTF8.GetBytes("NewBinaryData5"));
        newPackets[2].BinaryData.Should().BeEquivalentTo(packets[7].BinaryData);
    }

    [Fact]
    public async Task Resend_WithNonExistentPacket_DoesNotInsertPacket()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(_cancellationTokenSource.Token);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddHours(-2), DateTime.Now.AddHours(-1));

        var request = ImmutableArray.Create(
            new PacketResendDto(2),
            new PacketResendDto(5),
            new PacketResendDto(100)
        );

        // Act
        await _messenger.SendAsync(ConnectorContract.PacketResendTopic(_metadata.ConnectorIdentifier), request);

        var packets = await context.Packet.AsNoTracking().OrderBy(p => p.Id).ToArrayAsync(_cancellationTokenSource.Token);
        var newPackets = packets.Where(p => p.Id > 10).ToArray();
        
        // Assert
        newPackets.Should().HaveCount(2);
        newPackets.Should().OnlyContain(p => p.Status == PacketStatus.Enqueued);

        newPackets[0].BinaryData.Should().BeEquivalentTo(packets[1].BinaryData);
        newPackets[1].BinaryData.Should().BeEquivalentTo(packets[4].BinaryData);
    }

    [Fact]
    public async Task Resend_WithNonResendablePackets_InsertsOnlyResendable()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(_cancellationTokenSource.Token);
        await context.Packet.AddRangeAsync([
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.Enqueued },
            new Packet { Channel = "NoResendChannel", DateCreated = DateTime.Now, Status = PacketStatus.Processed },
            new Packet { Channel = "NoResendChannel", DateCreated = DateTime.Now, Status = PacketStatus.Error },
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.FatalError },
            new Packet { Channel = "BadChannel", DateCreated = DateTime.Now, Status = PacketStatus.InProgress },
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.ManualStop }]);

        await context.SaveChangesAsync(_cancellationTokenSource.Token);

        var request = ImmutableArray.Create(
            new PacketResendDto(1),
            new PacketResendDto(2),
            new PacketResendDto(3),
            new PacketResendDto(4),
            new PacketResendDto(5));

        // Act
        await _messenger.SendAsync(ConnectorContract.PacketResendTopic(_metadata.ConnectorIdentifier), request);

        var packets = await context.Packet.AsNoTracking().OrderBy(p => p.Id).ToArrayAsync(_cancellationTokenSource.Token);
        var newPackets = packets.Where(p => p.Id > 6).ToArray();
        
        // Assert
        newPackets.Should().HaveCount(2);
        newPackets.Should().OnlyContain(p => p.Status == PacketStatus.Enqueued);

        newPackets[0].BinaryData.Should().BeEquivalentTo(packets[1].BinaryData);
        newPackets[1].BinaryData.Should().BeEquivalentTo(packets[3].BinaryData);
    }

    [Fact]
    public async Task Delete_WithSinglePacket_DeletesPacket()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(_cancellationTokenSource.Token);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddHours(-1), DateTime.Now);

        var request = ImmutableArray.Create<long>(5);

        // Act
        await _messenger.SendAsync(ConnectorContract.PacketDeleteSequenceTopic(_metadata.ConnectorIdentifier), request);
        var packets = await context.Packet.AsNoTracking().ToArrayAsync(_cancellationTokenSource.Token);
        
        // Assert
        packets.Should().HaveCount(9);
        packets.Should().OnlyContain(p => p.Id != 5);
    }

    [Fact]
    public async Task Delete_WithNonExistentPacket_DoesNothing()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(_cancellationTokenSource.Token);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddHours(-1), DateTime.Now);

        var request = ImmutableArray.Create<long>(100);

        // Act
        await _messenger.SendAsync(ConnectorContract.PacketDeleteSequenceTopic(_metadata.ConnectorIdentifier), request);
        var packets = await context.Packet.AsNoTracking().ToArrayAsync(_cancellationTokenSource.Token);
        
        // Assert
        packets.Should().HaveCount(10);
    }

    [Fact]
    public async Task Delete_WithMultiplePackets_DeletesAllPackets()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(_cancellationTokenSource.Token);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddHours(-1), DateTime.Now);

        var request = ImmutableArray.Create<long>(2, 5, 9);

        // Act
        await _messenger.SendAsync(ConnectorContract.PacketDeleteSequenceTopic(_metadata.ConnectorIdentifier), request);
        var packets = await context.Packet.AsNoTracking().ToArrayAsync(_cancellationTokenSource.Token);
        
        // Assert
        packets.Should().HaveCount(7);
        packets.Should().OnlyContain(p => p.Id != 2 && p.Id != 5 && p.Id != 9);
    }

    [Fact]
    public async Task ManualStop_WithSinglePacket_UpdatesPacket()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(_cancellationTokenSource.Token);
        await context.Packet.AddRangeAsync([
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.Enqueued },
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.Processed },
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.Error },
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.FatalError },
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.InProgress },
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.ManualStop }]);

        await context.SaveChangesAsync(_cancellationTokenSource.Token);

        var request = ImmutableArray.Create<long>(3);

        // Act
        await _messenger.SendAsync(ConnectorContract.PacketManualStopSequenceTopic(_metadata.ConnectorIdentifier), request);
        var packets = await context.Packet.AsNoTracking().OrderBy(p => p.Id).ToArrayAsync(_cancellationTokenSource.Token);
        
        // Assert
        packets.Should().HaveCount(6);
        packets[2].Status.Should().Be(PacketStatus.ManualStop);
    }

    [Fact]
    public async Task ManualStop_WithNonExistentPacket_DoesNothing()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(_cancellationTokenSource.Token);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddHours(-1), DateTime.Now);

        var request = ImmutableArray.Create<long>(100);

        var oldPackets = await context.Packet.AsNoTracking().OrderBy(p => p.Id).ToArrayAsync(_cancellationTokenSource.Token);

        // Act
        await _messenger.SendAsync(ConnectorContract.PacketManualStopSequenceTopic(_metadata.ConnectorIdentifier), request);
        var packets = await context.Packet.AsNoTracking().OrderBy(p => p.Id).ToArrayAsync(_cancellationTokenSource.Token);
        
        // Assert
        packets.Should().BeEquivalentTo(oldPackets);
    }

    [Fact]
    public async Task ManualStop_WithAnyStatus_UpdatesAll()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(_cancellationTokenSource.Token);
        await context.Packet.AddRangeAsync([
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.Enqueued },
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.Processed },
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.Error },
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.FatalError },
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.InProgress },
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.ManualStop },
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.Enqueued }]);

        await context.SaveChangesAsync(_cancellationTokenSource.Token);

        var request = ImmutableArray.Create<long>(1, 2, 3, 4, 5, 6);

        // Act
        await _messenger.SendAsync(ConnectorContract.PacketManualStopSequenceTopic(_metadata.ConnectorIdentifier), request);
        var packets = await context.Packet.AsNoTracking().OrderBy(p => p.Id).ToArrayAsync(_cancellationTokenSource.Token);
        
        // Assert
        packets.Should().HaveCount(7);

        packets.SkipLast(1).ForEach(p => p.Status.Should().Be(PacketStatus.ManualStop));
        packets[^1].Status.Should().Be(PacketStatus.Enqueued);
    }

    [Fact]
    public async Task Export_WhenDatabaseIsEmpty_ReturnsEmpty()
    {
        // Arrange
        var filter = new PacketRequestDto(
            DateTimeStart: DateTime.MinValue,
            DateTimeEnd: DateTime.MaxValue);

        var exportTopic = ConnectorContract.PacketExportTopic(_metadata.ConnectorIdentifier);
        
        // Act
        var exportResult = await _messenger
            .AskAsync<PacketRequestDto, ConnectorPacketsExportDto>(exportTopic, filter)
            .FirstOrDefaultResponse();
        
        // Assert
        exportResult.Should().NotBeNull();
        exportResult.Packets.Should().BeEmpty();
    }

    [Fact]
    public async Task Export_WithValidDateRange_ReturnsPackets()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync(_cancellationTokenSource.Token);
        await context.AddTestPacketsAsync(10, dateTimeNow.AddHours(-2), dateTimeNow);

        _packetConverter.PacketToUIDataConverter(Arg.Any<PacketData>(), Arg.Any<CancellationToken>()).Returns(x =>
        {
            var packet = x.ArgAt<PacketData>(0);
            var data = Encoding.UTF8.GetString(packet.BinaryData.Span).Remove(0, 6);
            return new UIConversionInfo(data, $"{data}Preview", UIDataTypes.Plaintext);
        });

        var filter = new PacketRequestDto(
            DateTimeStart: dateTimeNow.AddHours(-2),
            DateTimeEnd: dateTimeNow.AddHours(-1));

        var filteredPackets = await context.Packet.AsNoTracking()
            .Where(p => p.DateCreated >= filter.DateTimeStart && p.DateCreated <= filter.DateTimeEnd)
            .ToArrayAsync(_cancellationTokenSource.Token);

        var expectedPackets = await Task.WhenAll(filteredPackets.Select(async p => await _packetDtoService.BuildDtoAsync(p)));

        var exportTopic = ConnectorContract.PacketExportTopic(_metadata.ConnectorIdentifier);
        
        // Act
        var exportResult = await _messenger
            .AskAsync<PacketRequestDto, ConnectorPacketsExportDto>(exportTopic, filter)
            .FirstOrDefaultResponse();
        
        // Assert
        exportResult.Should().NotBeNull();
        exportResult.Packets.Should().BeEquivalentTo(expectedPackets);
    }

    [Fact]
    public async Task Export_WhenNoMatch_ReturnsEmpty()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;
        await using var context = await _dbContextFactory.CreateDbContextAsync(_cancellationTokenSource.Token);
        await context.AddTestPacketsAsync(10, dateTimeNow.AddHours(-3), dateTimeNow.AddHours(-2));

        var filter = new PacketRequestDto(
            DateTimeStart: dateTimeNow.AddHours(-1),
            DateTimeEnd: dateTimeNow);

        var exportTopic = ConnectorContract.PacketExportTopic(_metadata.ConnectorIdentifier);
        
        // Act
        var exportResult = await _messenger
            .AskAsync<PacketRequestDto, ConnectorPacketsExportDto>(exportTopic, filter)
            .FirstOrDefaultResponse();
        
        // Assert
        exportResult.Should().NotBeNull();
        exportResult.Packets.Should().BeEmpty();
    }

    [Fact]
    public async Task Export_WithInvalidDateRange_ReturnsEmpty()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;
        await using var context = await _dbContextFactory.CreateDbContextAsync(_cancellationTokenSource.Token);
        await context.AddTestPacketsAsync(10, dateTimeNow.AddHours(-3), dateTimeNow.AddHours(-2));

        var filter = new PacketRequestDto(
            DateTimeStart: dateTimeNow,
            DateTimeEnd: dateTimeNow.AddHours(-1));

        var exportTopic = ConnectorContract.PacketExportTopic(_metadata.ConnectorIdentifier);
        
        // Act
        var exportResult = await _messenger
            .AskAsync<PacketRequestDto, ConnectorPacketsExportDto>(exportTopic, filter)
            .FirstOrDefaultResponse();
        
        // Assert
        exportResult.Should().NotBeNull();
        exportResult.Packets.Should().BeEmpty();
    }

    [Fact]
    public async Task Import_WhenNoConflict_ImportsPackets()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(_cancellationTokenSource.Token);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddHours(-2), DateTime.Now.AddHours(-1));

        var packetDtos = new PacketDto[]
        {
            new() {
                Id = 1,
                Data = "Packet11",
                DateCreated = DateTime.Now.AddMinutes(-20),
                Channel = "TestChannel",
                ParentId = null,
                Status = PacketStatus.Processed,
                DynamicField = "DynamicField"
            },
            new() {
                Id = 2,
                Data = "Parent12",
                DateCreated = DateTime.Now.AddMinutes(-15),
                Channel = "TestChannel",
                ParentId = null,
                Status = PacketStatus.FatalError,
                Metadata = "Metadata",
                RetryCount = 10
            },
            new() {
                Id = 3,
                Data = "Packet13",
                DateCreated = DateTime.Now.AddMinutes(-10),
                DateChanged = DateTime.Now.AddMinutes(-5),
                Channel = "TestChannel",
                ParentId = 3,
                Status = PacketStatus.Enqueued
            }
        };

        _packetConverter.UIToBinaryDataConverter(Arg.Any<UIConversionInfo>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(x =>
        {
            var uiConversion = x.ArgAt<UIConversionInfo>(0);
            return Encoding.UTF8.GetBytes($"Binary{uiConversion.Data}");
        });

        var exportDto = new ConnectorPacketsExportDto([.. packetDtos]);
        var importDto = new ConnectorPacketsTryImportDto(exportDto, false);
        
        // Act
        var result = await _messenger
            .AskAsync<ConnectorPacketsTryImportDto, bool>(ConnectorContract.PacketTryImportTopic(_metadata.ConnectorIdentifier), importDto)
            .FirstOrDefaultResponse();

        var packets = await context.Packet.AsNoTracking().OrderBy(p => p.Id).ToArrayAsync(_cancellationTokenSource.Token);
        var importedPackets = packets.Where(p => p.Id > 10).ToArray();
        
        // Assert
        result.Should().BeTrue();
        importedPackets.Should().HaveCount(3);

        for (var i = 0; i < 3; i++)
        {
            importedPackets[i].BinaryData.Should().BeEquivalentTo(Encoding.UTF8.GetBytes($"Binary{packetDtos[i].Data}"));
            importedPackets[i].DateCreated.Should().Be(packetDtos[i].DateCreated);
            importedPackets[i].DateChanged.Should().Be(packetDtos[i].DateChanged);
            importedPackets[i].Channel.Should().Be(packetDtos[i].Channel);
            importedPackets[i].ParentId.Should().Be(packetDtos[i].ParentId);
            importedPackets[i].Status.Should().Be(packetDtos[i].Status);
            importedPackets[i].Metadata.Should().Be(packetDtos[i].Metadata);
            importedPackets[i].DynamicField.Should().Be(packetDtos[i].DynamicField);
            importedPackets[i].RetryCount.Should().Be(packetDtos[i].RetryCount);
        }
    }

    [Fact]
    public async Task Import_WhenConflictWithoutForceImport_DoesNotImport()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(_cancellationTokenSource.Token);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddHours(-1), DateTime.Now.AddMinutes(-10));

        var packetDtos = new PacketDto[]
        {
            new() {
                Id = 1,
                Data = "Packet11",
                DateCreated = DateTime.Now.AddMinutes(-20),
                Channel = "TestChannel",
                ParentId = null,
                Status = PacketStatus.Processed,
                DynamicField = "DynamicField"
            },
            new() {
                Id = 2,
                Data = "Parent12",
                DateCreated = DateTime.Now.AddMinutes(-15),
                Channel = "TestChannel",
                ParentId = null,
                Status = PacketStatus.FatalError,
                Metadata = "Metadata",
                RetryCount = 10
            },
            new() {
                Id = 3,
                Data = "Packet13",
                DateCreated = DateTime.Now.AddMinutes(-5),
                Channel = "TestChannel",
                ParentId = 3,
                Status = PacketStatus.Enqueued
            }
        };

        var exportDto = new ConnectorPacketsExportDto([.. packetDtos]);
        var importDto = new ConnectorPacketsTryImportDto(exportDto, false);
        
        // Act
        var result = await _messenger
            .AskAsync<ConnectorPacketsTryImportDto, bool>(ConnectorContract.PacketTryImportTopic(_metadata.ConnectorIdentifier), importDto)
            .FirstOrDefaultResponse();
        var packets = await context.Packet.AsNoTracking().OrderBy(p => p.Id).ToArrayAsync(_cancellationTokenSource.Token);
        
        // Assert
        result.Should().BeFalse();
        packets.Should().HaveCount(10);
    }

    [Fact]
    public async Task Import_WhenConflictWithForceImport_OverwritesConflictingPackets()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(_cancellationTokenSource.Token);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddHours(-1), DateTime.Now.AddMinutes(-10));

        var packetDtos = new PacketDto[]
        {
            new() {
                Id = 1,
                Data = "Packet11",
                DateCreated = DateTime.Now.AddMinutes(-20),
                Channel = "TestChannel",
                ParentId = null,
                Status = PacketStatus.Processed,
                DynamicField = "DynamicField"
            },
            new() {
                Id = 2,
                Data = "Parent12",
                DateCreated = DateTime.Now.AddMinutes(-15),
                Channel = "TestChannel",
                ParentId = null,
                Status = PacketStatus.FatalError,
                Metadata = "Metadata",
                RetryCount = 10
            },
            new() {
                Id = 3,
                Data = "Packet13",
                DateCreated = DateTime.Now.AddMinutes(-5),
                DateChanged = DateTime.Now,
                Channel = "TestChannel",
                ParentId = 3,
                Status = PacketStatus.Enqueued
            }
        };

        _packetConverter.UIToBinaryDataConverter(Arg.Any<UIConversionInfo>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(x =>
        {
            var uiConversion = x.ArgAt<UIConversionInfo>(0);
            return Encoding.UTF8.GetBytes($"Binary{uiConversion.Data}");
        });

        var exportDto = new ConnectorPacketsExportDto([.. packetDtos]);
        var importDto = new ConnectorPacketsTryImportDto(exportDto, true);
        
        // Act
        var result = await _messenger
            .AskAsync<ConnectorPacketsTryImportDto, bool>(ConnectorContract.PacketTryImportTopic(_metadata.ConnectorIdentifier), importDto)
            .FirstOrDefaultResponse();

        var packets = await context.Packet.AsNoTracking().OrderBy(p => p.Id).ToArrayAsync(_cancellationTokenSource.Token);
        var importedPackets = packets.Where(p => p.Id > 10).ToArray();
        
        // Assert
        result.Should().BeTrue();
        packets.Should().HaveCount(11);
        importedPackets.Should().HaveCount(3);

        for (var i = 0; i < 3; i++)
        {
            importedPackets[i].BinaryData.Should().BeEquivalentTo(Encoding.UTF8.GetBytes($"Binary{packetDtos[i].Data}"));
            importedPackets[i].DateCreated.Should().Be(packetDtos[i].DateCreated);
            importedPackets[i].DateChanged.Should().Be(packetDtos[i].DateChanged);
            importedPackets[i].Channel.Should().Be(packetDtos[i].Channel);
            importedPackets[i].ParentId.Should().Be(packetDtos[i].ParentId);
            importedPackets[i].Status.Should().Be(packetDtos[i].Status);
            importedPackets[i].Metadata.Should().Be(packetDtos[i].Metadata);
            importedPackets[i].DynamicField.Should().Be(packetDtos[i].DynamicField);
            importedPackets[i].RetryCount.Should().Be(packetDtos[i].RetryCount);
        }
    }

    [Fact]
    public async Task Import_WhenConflictOnExactDateTimeWithoutForceImport_DoesNotImport()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync(_cancellationTokenSource.Token);

        await context.Packet.AddAsync(new()
        {
            BinaryData = Encoding.UTF8.GetBytes("BinaryData"),
            Channel = "TestChannel",
            DateCreated = dateTimeNow,
            Status = PacketStatus.Processed,
        }, _cancellationTokenSource.Token);

        await context.SaveChangesAsync(_cancellationTokenSource.Token);

        var packetDto = new PacketDto
        {
            Id = 2,
            Data = "Packet2",
            DateCreated = dateTimeNow,
            Channel = "TestChannel",
            ParentId = null,
            Status = PacketStatus.Processed,
        };

        var exportDto = new ConnectorPacketsExportDto([packetDto]);
        var importDto = new ConnectorPacketsTryImportDto(exportDto, false);
        
        // Act
        var result = await _messenger
            .AskAsync<ConnectorPacketsTryImportDto, bool>(ConnectorContract.PacketTryImportTopic(_metadata.ConnectorIdentifier), importDto)
            .FirstOrDefaultResponse();
        var packets = await context.Packet.AsNoTracking().OrderBy(p => p.Id).ToArrayAsync(_cancellationTokenSource.Token);
        
        // Assert
        result.Should().BeFalse();
        packets.Should().ContainSingle();
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
