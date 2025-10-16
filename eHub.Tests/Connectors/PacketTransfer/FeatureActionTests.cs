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

[TestClass]
public class FeatureActionTests
{
    private PacketTransferFeature _packetTransferFeature = default!;
    private SqliteConnection _connection = default!;
    private ConnectorMetadata _metadata = default!;
    private ConnectorTemplate _connectorTemplate = default!;
    private PacketDtoService _packetDtoService = default!;
    private PluginData _pluginData = default!;
    private CancellationTokenSource _cancellationTokenSource = default!;
    private IDbContextFactory<HubDbContext> _dbContextFactory = default!;
    private ILoggerFactory _loggerFactory = default!;
    private ILogger<PacketTransferFeature> _logger = default!;
    private IScopedMessenger _messenger = default!;
    private IPacketFilterConfigurator _packetTransfer = default!;
    private IPacketConverter _packetConverter = default!;
    private IFilterRunnerProvider _filterRunnerProvider = default!;
    private IEffortlessConfigurationRegistry _registry = default!;
    private ConnectorMetrics _metrics = default!;

    [TestInitialize]
    public async Task Initialize()
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

    [TestMethod]
    public async Task PollAliveConnectors_WhenCalled_ReturnsKeepAliveDto()
    {
        _packetTransferFeature = new PacketTransferFeature(_logger, _dbContextFactory, _messenger, _packetTransfer, _filterRunnerProvider, _registry, _packetDtoService, _metadata, _connectorTemplate, _pluginData, _metrics);
        await _packetTransferFeature.StartAsync(_cancellationTokenSource.Token);

        var response = await _messenger
            .AskAsync<ConnectorKeepAliveDto>(ConnectorContract.PollAliveConnectorsTopic())
            .FirstOrDefaultResponse();

        response.Should().NotBeNull();
        response.Identifier.Should().Be(_metadata.ConnectorIdentifier);
    }

    [TestMethod]
    public async Task UIGet_WhenCalled_ReturnsConnectorUIData()
    {
        var response = await _messenger
            .AskAsync<ConnectorUiData>(ConnectorContract.UIGetTopic(_metadata.ConnectorIdentifier))
            .FirstOrDefaultResponse();

        response.Should().NotBeNull();
        response.ConnectorName.Should().Be(_metadata.TemplateName);
        response.ConnectorType.Should().Be(_metadata.ConnectorType);
        response.UIViewConfig.Should().NotBeNull();
    }

    [TestMethod]
    public async Task GetCustomFilters_WhenCalled_ReturnsCustomFilters()
    {
        var response = await _messenger
            .AskAsync<IReadOnlyDictionary<string, string>>(ConnectorContract.GetCustomFiltersTopic(_metadata.ConnectorIdentifier))
            .FirstOrDefaultResponse();

        response.Should().NotBeNull();
        response.Should().ContainKey("MyCustomFilter");
        response["MyCustomFilter"].Should().Be(typeof(string).ToString());
    }

    [TestMethod]
    public async Task GetPackets_WhenDatabaseIsEmpty_ReturnsEmpty()
    {
        var filter = new PacketRequestDto(
            DateTimeStart: DateTime.MinValue,
            DateTimeEnd: DateTime.MaxValue);

        var packetsGetTopic = ConnectorContract.UIPacketsGetTopic(_metadata.ConnectorIdentifier);
        var packetWrapper = await _messenger
            .AskAsync<PacketRequestDto, PacketWrapperDto>(packetsGetTopic, filter)
            .FirstOrDefaultResponse();

        packetWrapper.Should().NotBeNull();
        packetWrapper.Packets.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GetPackets_WithValidDateRange_ReturnsPackets()
    {
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync();
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
            .ToArrayAsync();

        var expectedPackets = await Task.WhenAll(filteredPackets.Select(async p => await _packetDtoService.BuildDtoAsync(p)));

        var packetsGetTopic = ConnectorContract.UIPacketsGetTopic(_metadata.ConnectorIdentifier);
        var packetWrapper = await _messenger
            .AskAsync<PacketRequestDto, PacketWrapperDto>(packetsGetTopic, filter)
            .FirstOrDefaultResponse();

        packetWrapper.Should().NotBeNull();
        packetWrapper.Packets.Should().BeEquivalentTo(expectedPackets);
    }

    [TestMethod]
    public async Task GetPackets_WithColumnFilters_ReturnsPackets()
    {
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(20, dateTimeNow.AddHours(-2), dateTimeNow.AddHours(-1), ["TestChannel1", "TestChannel2"]);
        await context.Packet.AddAsync(new Packet
        {
            BinaryData = Encoding.UTF8.GetBytes("NotFiltered"),
            Channel = "TestChannel1",
            DateCreated = dateTimeNow,
            Status = PacketStatus.Enqueued
        });

        await context.SaveChangesAsync();

        _packetConverter.PacketToUIDataConverter(Arg.Any<PacketData>(), Arg.Any<CancellationToken>()).Returns(x =>
        {
            var packet = x.ArgAt<PacketData>(0);
            var data = Encoding.UTF8.GetString(packet.BinaryData.Span).Remove(0, 6);

            if (packet.Id < 5)
            {
                // Matching filtering data but not preview
                return new UIConversionInfo(data, $"Preview{data}", UIDataTypes.Plaintext);
            }
            else if(packet.Id < 10)
            {
                // Matching filtering preview but not data
                return new UIConversionInfo("InvalidData", $"{data}Preview", UIDataTypes.Plaintext);
            }
            else
            {
                // Matching both data and preview
                return new UIConversionInfo(data, $"{data}Preview", UIDataTypes.Plaintext);
            }
        });

        var columnFilters = new ColumnFilterDto[]
        {
            new(nameof(PacketDto.Data), ColumnFilterOperator.StartsWith, "Data"),
            new(nameof(PacketDto.PreviewData), ColumnFilterOperator.EndsWith, "Preview"),
            new(nameof(PacketDto.Channel), ColumnFilterOperator.Contains, "Channel1"),
            new(nameof(PacketDto.Status), ColumnFilterOperator.Equal, PacketStatus.Enqueued.ToString())
        };

        var filter = new PacketRequestDto(ColumnFilters: [.. columnFilters]);

        var filteredPackets = await context.Packet.AsNoTracking()
            .Where(p => p.Channel.Contains("Channel1") && p.Status == PacketStatus.Enqueued)
            .ToArrayAsync();

        var packetsDto = await Task.WhenAll(filteredPackets.Select(async p => await _packetDtoService.BuildDtoAsync(p)));
        var expectedPackets = packetsDto
            .Where(p => columnFilters.Where(f => f.ColumnName == nameof(PacketDto.Data)).ToArray().Matches(p.Data))
            .Where(p => columnFilters.Where(f => f.ColumnName == nameof(PacketDto.PreviewData)).ToArray().Matches(p.PreviewData))
            .ToArray();

        var packetsGetTopic = ConnectorContract.UIPacketsGetTopic(_metadata.ConnectorIdentifier);
        var packetWrapper = await _messenger
            .AskAsync<PacketRequestDto, PacketWrapperDto>(packetsGetTopic, filter)
            .FirstOrDefaultResponse();

        packetWrapper.Should().NotBeNull();
        packetWrapper.Packets.Should().BeEquivalentTo(expectedPackets);
    }

    [TestMethod]
    public async Task GetPackets_WhenNoMatch_ReturnsEmpty()
    {
        var dateTimeNow = DateTime.Now;
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, dateTimeNow.AddHours(-3), dateTimeNow.AddHours(-2));

        var filter = new PacketRequestDto(
            DateTimeStart: dateTimeNow.AddHours(-1),
            DateTimeEnd: dateTimeNow);

        var packetsGetTopic = ConnectorContract.UIPacketsGetTopic(_metadata.ConnectorIdentifier);
        var packetWrapper = await _messenger
            .AskAsync<PacketRequestDto, PacketWrapperDto>(packetsGetTopic, filter)
            .FirstOrDefaultResponse();

        packetWrapper.Should().NotBeNull();
        packetWrapper.Packets.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GetPackets_WithInvalidDateRange_ReturnsEmpty()
    {
        var dateTimeNow = DateTime.Now;
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, dateTimeNow.AddHours(-3), dateTimeNow.AddHours(-2));

        var filter = new PacketRequestDto(
            DateTimeStart: dateTimeNow,
            DateTimeEnd: dateTimeNow.AddHours(-1));

        var packetsGetTopic = ConnectorContract.UIPacketsGetTopic(_metadata.ConnectorIdentifier);
        var packetWrapper = await _messenger
            .AskAsync<PacketRequestDto, PacketWrapperDto>(packetsGetTopic, filter)
            .FirstOrDefaultResponse();

        packetWrapper.Should().NotBeNull();
        packetWrapper.Packets.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Resend_WhenDbEmpty_DoesntResend()
    {
        var nonExistent = ImmutableArray.Create(new PacketResendDto(42));

        await _messenger.SendAsync(ConnectorContract.PacketResendTopic(_metadata.ConnectorIdentifier), nonExistent);

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var packets = await context.Packet.ToArrayAsync();
        packets.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Resend_WithSameData_InsertsIdenticalPacket()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

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
        });

        await context.SaveChangesAsync();

        var request = ImmutableArray.Create(new PacketResendDto(1));
        var requestDateTime = DateTime.Now;
        await _messenger.SendAsync(ConnectorContract.PacketResendTopic(_metadata.ConnectorIdentifier), request);

        var packets = await context.Packet.AsNoTracking().ToArrayAsync();
        var oldPacket = packets.Single(p => p.Id == 1);
        var newPacket = packets.Single(p => p.Id == 2);

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

    [TestMethod]
    public async Task Resend_WithNewData_InsertsPacketWithNewData()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

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
        });

        await context.SaveChangesAsync();

        var uiConversion = new UIConversionInfo("OldData", "DataPreview", UIDataTypes.Plaintext);
        _packetConverter.PacketToUIDataConverter(Arg.Any<PacketData>(), Arg.Any<CancellationToken>())
            .Returns(uiConversion);

        uiConversion.Data = "NewData";
        _packetConverter.UIToBinaryDataConverter(Arg.Is(uiConversion), Arg.Is("Metadata"), Arg.Any<CancellationToken>())
            .Returns(Encoding.UTF8.GetBytes("NewBinaryData"));

        var request = ImmutableArray.Create(new PacketResendDto(1, "NewData"));
        var requestDateTime = DateTime.Now;
        await _messenger.SendAsync(ConnectorContract.PacketResendTopic(_metadata.ConnectorIdentifier), request);

        var packets = await context.Packet.AsNoTracking().ToArrayAsync();
        var oldPacket = packets.Single(p => p.Id == 1);
        var newPacket = packets.Single(p => p.Id == 2);

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

    [TestMethod]
    public async Task Resend_WithMultiplePackets_InsertsPackets()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
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
        await _messenger.SendAsync(ConnectorContract.PacketResendTopic(_metadata.ConnectorIdentifier), request);

        var packets = await context.Packet.AsNoTracking().OrderBy(p => p.Id).ToArrayAsync();
        var newPackets = packets.Where(p => p.Id > 10).ToArray();

        newPackets.Should().HaveCount(3);
        newPackets.Should().OnlyContain(p => p.Status == PacketStatus.Enqueued);
        newPackets.ForEach(p => p.DateCreated.Should().BeCloseTo(requestDateTime, TimeSpan.FromSeconds(1)));

        newPackets[0].BinaryData.Should().BeEquivalentTo(packets[1].BinaryData);
        newPackets[1].BinaryData.Should().BeEquivalentTo(Encoding.UTF8.GetBytes("NewBinaryData5"));
        newPackets[2].BinaryData.Should().BeEquivalentTo(packets[7].BinaryData);
    }

    [TestMethod]
    public async Task Resend_WithNonExistentPacket_DoesNotInsertPacket()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddHours(-2), DateTime.Now.AddHours(-1));

        var request = ImmutableArray.Create(
            new PacketResendDto(2),
            new PacketResendDto(5),
            new PacketResendDto(100)
        );

        await _messenger.SendAsync(ConnectorContract.PacketResendTopic(_metadata.ConnectorIdentifier), request);

        var packets = await context.Packet.AsNoTracking().OrderBy(p => p.Id).ToArrayAsync();
        var newPackets = packets.Where(p => p.Id > 10).ToArray();

        newPackets.Should().HaveCount(2);
        newPackets.Should().OnlyContain(p => p.Status == PacketStatus.Enqueued);

        newPackets[0].BinaryData.Should().BeEquivalentTo(packets[1].BinaryData);
        newPackets[1].BinaryData.Should().BeEquivalentTo(packets[4].BinaryData);
    }

    [TestMethod]
    public async Task Resend_WithNonResendablePackets_InsertsOnlyResendable()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.Packet.AddRangeAsync([
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.Enqueued },
            new Packet { Channel = "NoResendChannel", DateCreated = DateTime.Now, Status = PacketStatus.Processed },
            new Packet { Channel = "NoResendChannel", DateCreated = DateTime.Now, Status = PacketStatus.Error },
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.FatalError },
            new Packet { Channel = "BadChannel", DateCreated = DateTime.Now, Status = PacketStatus.InProgress },
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.ManualStop }]);

        await context.SaveChangesAsync();

        var request = ImmutableArray.Create(
            new PacketResendDto(1),
            new PacketResendDto(2),
            new PacketResendDto(3),
            new PacketResendDto(4),
            new PacketResendDto(5));

        await _messenger.SendAsync(ConnectorContract.PacketResendTopic(_metadata.ConnectorIdentifier), request);

        var packets = await context.Packet.AsNoTracking().OrderBy(p => p.Id).ToArrayAsync();
        var newPackets = packets.Where(p => p.Id > 6).ToArray();

        newPackets.Should().HaveCount(2);
        newPackets.Should().OnlyContain(p => p.Status == PacketStatus.Enqueued);

        newPackets[0].BinaryData.Should().BeEquivalentTo(packets[1].BinaryData);
        newPackets[1].BinaryData.Should().BeEquivalentTo(packets[3].BinaryData);
    }

    [TestMethod]
    public async Task Delete_WithSinglePacket_DeletesPacket()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddHours(-1), DateTime.Now);

        var request = ImmutableArray.Create<long>(5);

        await _messenger.SendAsync(ConnectorContract.PacketDeleteSequenceTopic(_metadata.ConnectorIdentifier), request);

        var packets = await context.Packet.AsNoTracking().ToArrayAsync();
        packets.Should().HaveCount(9);
        packets.Should().OnlyContain(p => p.Id != 5);
    }

    [TestMethod]
    public async Task Delete_WithNonExistentPacket_DoesNothing()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddHours(-1), DateTime.Now);

        var request = ImmutableArray.Create<long>(100);

        await _messenger.SendAsync(ConnectorContract.PacketDeleteSequenceTopic(_metadata.ConnectorIdentifier), request);

        var packets = await context.Packet.AsNoTracking().ToArrayAsync();
        packets.Should().HaveCount(10);
    }

    [TestMethod]
    public async Task Delete_WithMultiplePackets_DeletesAllPackets()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddHours(-1), DateTime.Now);

        var request = ImmutableArray.Create<long>(2, 5, 9);

        await _messenger.SendAsync(ConnectorContract.PacketDeleteSequenceTopic(_metadata.ConnectorIdentifier), request);

        var packets = await context.Packet.AsNoTracking().ToArrayAsync();
        packets.Should().HaveCount(7);
        packets.Should().OnlyContain(p => p.Id != 2 && p.Id != 5 && p.Id != 9);
    }

    [TestMethod]
    public async Task ManualStop_WithSinglePacket_UpdatesPacket()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.Packet.AddRangeAsync([
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.Enqueued },
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.Processed },
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.Error },
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.FatalError },
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.InProgress },
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.ManualStop }]);

        await context.SaveChangesAsync();

        var request = ImmutableArray.Create<long>(3);

        await _messenger.SendAsync(ConnectorContract.PacketManualStopSequenceTopic(_metadata.ConnectorIdentifier), request);

        var packets = await context.Packet.AsNoTracking().OrderBy(p => p.Id).ToArrayAsync();

        packets.Should().HaveCount(6);
        packets[2].Status.Should().Be(PacketStatus.ManualStop);
    }

    [TestMethod]
    public async Task ManualStop_WithNonExistentPacket_DoesNothing()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddHours(-1), DateTime.Now);

        var request = ImmutableArray.Create<long>(100);

        var oldPackets = await context.Packet.AsNoTracking().OrderBy(p => p.Id).ToArrayAsync();

        await _messenger.SendAsync(ConnectorContract.PacketManualStopSequenceTopic(_metadata.ConnectorIdentifier), request);

        var packets = await context.Packet.AsNoTracking().OrderBy(p => p.Id).ToArrayAsync();

        packets.Should().BeEquivalentTo(oldPackets);
    }

    [TestMethod]
    public async Task ManualStop_WithAnyStatus_UpdatesAll()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.Packet.AddRangeAsync([
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.Enqueued },
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.Processed },
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.Error },
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.FatalError },
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.InProgress },
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.ManualStop },
            new Packet { Channel = "TestChannel", DateCreated = DateTime.Now, Status = PacketStatus.Enqueued }]);

        await context.SaveChangesAsync();

        var request = ImmutableArray.Create<long>(1, 2, 3, 4, 5, 6);

        await _messenger.SendAsync(ConnectorContract.PacketManualStopSequenceTopic(_metadata.ConnectorIdentifier), request);

        var packets = await context.Packet.AsNoTracking().OrderBy(p => p.Id).ToArrayAsync();
        packets.Should().HaveCount(7);

        packets.SkipLast(1).ForEach(p => p.Status.Should().Be(PacketStatus.ManualStop));
        packets[^1].Status.Should().Be(PacketStatus.Enqueued);
    }

    [TestMethod]
    public async Task Export_WhenDatabaseIsEmpty_ReturnsEmpty()
    {
        var filter = new PacketRequestDto(
            DateTimeStart: DateTime.MinValue,
            DateTimeEnd: DateTime.MaxValue);

        var exportTopic = ConnectorContract.PacketExportTopic(_metadata.ConnectorIdentifier);
        var exportResult = await _messenger
            .AskAsync<PacketRequestDto, ConnectorPacketsExportDto>(exportTopic, filter)
            .FirstOrDefaultResponse();

        exportResult.Should().NotBeNull();
        exportResult.Packets.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Export_WithValidDateRange_ReturnsPackets()
    {
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync();
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
            .ToArrayAsync();

        var expectedPackets = await Task.WhenAll(filteredPackets.Select(async p => await _packetDtoService.BuildDtoAsync(p)));

        var exportTopic = ConnectorContract.PacketExportTopic(_metadata.ConnectorIdentifier);
        var exportResult = await _messenger
            .AskAsync<PacketRequestDto, ConnectorPacketsExportDto>(exportTopic, filter)
            .FirstOrDefaultResponse();

        exportResult.Should().NotBeNull();
        exportResult.Packets.Should().BeEquivalentTo(expectedPackets);
    }

    [TestMethod]
    public async Task Export_WhenNoMatch_ReturnsEmpty()
    {
        var dateTimeNow = DateTime.Now;
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, dateTimeNow.AddHours(-3), dateTimeNow.AddHours(-2));

        var filter = new PacketRequestDto(
            DateTimeStart: dateTimeNow.AddHours(-1),
            DateTimeEnd: dateTimeNow);

        var exportTopic = ConnectorContract.PacketExportTopic(_metadata.ConnectorIdentifier);
        var exportResult = await _messenger
            .AskAsync<PacketRequestDto, ConnectorPacketsExportDto>(exportTopic, filter)
            .FirstOrDefaultResponse();

        exportResult.Should().NotBeNull();
        exportResult.Packets.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Export_WithInvalidDateRange_ReturnsEmpty()
    {
        var dateTimeNow = DateTime.Now;
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, dateTimeNow.AddHours(-3), dateTimeNow.AddHours(-2));

        var filter = new PacketRequestDto(
            DateTimeStart: dateTimeNow,
            DateTimeEnd: dateTimeNow.AddHours(-1));

        var exportTopic = ConnectorContract.PacketExportTopic(_metadata.ConnectorIdentifier);
        var exportResult = await _messenger
            .AskAsync<PacketRequestDto, ConnectorPacketsExportDto>(exportTopic, filter)
            .FirstOrDefaultResponse();

        exportResult.Should().NotBeNull();
        exportResult.Packets.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Import_WhenNoConflict_ImportsPackets()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
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
        var result = await _messenger
            .AskAsync<ConnectorPacketsTryImportDto, bool>(ConnectorContract.PacketTryImportTopic(_metadata.ConnectorIdentifier), importDto)
            .FirstOrDefaultResponse();

        var packets = await context.Packet.AsNoTracking().OrderBy(p => p.Id).ToArrayAsync();
        var importedPackets = packets.Where(p => p.Id > 10).ToArray();

        result.Should().BeTrue();
        importedPackets.Should().HaveCount(3);

        for (int i = 0; i < 3; i++)
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

    [TestMethod]
    public async Task Import_WhenConflictWithoutForceImport_DoesNotImport()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
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
        var result = await _messenger
            .AskAsync<ConnectorPacketsTryImportDto, bool>(ConnectorContract.PacketTryImportTopic(_metadata.ConnectorIdentifier), importDto)
            .FirstOrDefaultResponse();

        var packets = await context.Packet.AsNoTracking().OrderBy(p => p.Id).ToArrayAsync();

        result.Should().BeFalse();
        packets.Should().HaveCount(10);
    }

    [TestMethod]
    public async Task Import_WhenConflictWithForceImport_OverwritesConflictingPackets()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
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
        var result = await _messenger
            .AskAsync<ConnectorPacketsTryImportDto, bool>(ConnectorContract.PacketTryImportTopic(_metadata.ConnectorIdentifier), importDto)
            .FirstOrDefaultResponse();

        var packets = await context.Packet.AsNoTracking().OrderBy(p => p.Id).ToArrayAsync();
        var importedPackets = packets.Where(p => p.Id > 10).ToArray();

        result.Should().BeTrue();
        packets.Should().HaveCount(11);
        importedPackets.Should().HaveCount(3);

        for (int i = 0; i < 3; i++)
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

    [TestMethod]
    public async Task Import_WhenConflictOnExactDateTimeWithoutForceImport_DoesNotImport()
    {
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync();

        await context.Packet.AddAsync(new()
        {
            BinaryData = Encoding.UTF8.GetBytes("BinaryData"),
            Channel = "TestChannel",
            DateCreated = dateTimeNow,
            Status = PacketStatus.Processed,
        });

        await context.SaveChangesAsync();

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
        var result = await _messenger
            .AskAsync<ConnectorPacketsTryImportDto, bool>(ConnectorContract.PacketTryImportTopic(_metadata.ConnectorIdentifier), importDto)
            .FirstOrDefaultResponse();

        var packets = await context.Packet.AsNoTracking().OrderBy(p => p.Id).ToArrayAsync();

        result.Should().BeFalse();
        packets.Should().ContainSingle();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource.Dispose();

        await _packetTransferFeature.DisposeAsync();

        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }
}
