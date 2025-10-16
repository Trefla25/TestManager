using eHub.Config;
using eHub.Contracts.UIConfig;
using eHub.Database.Models;
using eHub.Database;
using eHub.PlugIn;
using eHub.Scripting.Connectors;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using System.Text;
using eHub.Scripting.Connectors.Services;
using Microsoft.Extensions.Logging;
using eHub.PlugIn.UI;
using NSubstitute.ExceptionExtensions;
using eHub.Contracts;
using System.Linq.Expressions;
using eHub.Tests.Helper;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace eHub.Tests.Connectors.PacketTransfer.UI;

[TestClass]
public class PacketDtoServiceTests
{
    private const string TestChannel = "TestChannel";
    private const string ErrorChannel = "ErroChannel";

    private PacketDtoService _packetDtoService = default!;
    private ConnectorMetadata _metadata = default!;
    private ConnectorTemplate _connectorTemplate = default!;
    private ILoggerFactory _loggerFactory = default!;
    private IPacketTransfer _packetTransfer = default!;
    private IPacketConverter _packetConverter = default!;
    private IDbContextFactory<HubDbContext> _dbContextFactory = default!;
    private ILogger<PacketDtoService> _logger = default!;
    private SqliteConnection _connection = default!;

    [TestInitialize]
    public async Task TestInitialize()
    {
        _connection = new SqliteConnection(DbHelper.InMemoryConnectionString);
        await _connection.OpenAsync();

        _metadata = new ConnectorMetadata(new(), "MyTestConnector", "TestConnector");
        _connectorTemplate = new ConnectorTemplate()
        {
            PacketTransfer = new()
            {
                ChannelGroups = new Dictionary<string, ChannelGroup>()
                {
                    ["NormalGroup"] = new ChannelGroup()
                    {
                        CanResend = true,
                        Channels = [TestChannel]
                    },
                    ["NoResendGroup"] = new ChannelGroup()
                    {
                        CanResend = false,
                        Channels = [ErrorChannel]
                    }
                }
            }
        };

        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = _loggerFactory.CreateLogger<PacketDtoService>();
        _packetTransfer = Substitute.For<IPacketTransfer>();
        _packetConverter = Substitute.For<IPacketConverter>();
        _packetTransfer.Converter.Returns(_packetConverter);
        _dbContextFactory = DbHelper.CreateInMemoryFactory<HubDbContext>(_connection);

        _packetDtoService = new PacketDtoService(_logger, _packetTransfer, _metadata, _connectorTemplate);

        // Make sure the database is created
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
    }

    [TestMethod]
    public async Task BuildDtoAsync_WhenConversionSucceeds_ReturnsConvertedDto()
    {
        var packet = new Packet
        {
            Id = 4,
            Channel = TestChannel,
            BinaryData = Encoding.UTF8.GetBytes("TestData"),
            Status = PacketStatus.Error,
            DateCreated = DateTime.Now.AddMinutes(-15),
            DateChanged = DateTime.Now.AddMinutes(-10),
            Metadata = "meta",
            DynamicField = "dynamic",
            ParentId = null,
            RetryCount = 3
        };

        var expectedData = "{\"Id\": 4}";
        var expectedDataPreview = "Id: 4";
        var expectedDataType = UIDataTypes.Json;

        _packetTransfer.Converter.PacketToUIDataConverter(Arg.Any<PacketData>())
            .Returns(new UIConversionInfo { Data = expectedData, DataPreview = expectedDataPreview, DataType = expectedDataType });

        var packetDto = await _packetDtoService.BuildDtoAsync(packet);

        await _packetTransfer.Converter.Received(1).PacketToUIDataConverter(Arg.Any<PacketData>());

        // Properties should be copied from the packet
        AssertPacketDtoMatchesPacket(packet, packetDto);

        // Data properties should be set by the conversion
        packetDto.Data.Should().Be(expectedData);
        packetDto.PreviewData.Should().Be(expectedDataPreview);
        packetDto.DataType.Should().Be(expectedDataType);

        // Other properties should be set by the service correctly
        packetDto.CanResend.Should().BeTrue();
        packetDto.ConnectorName.Should().Be(_metadata.TemplateName);
    }

    [TestMethod]
    public async Task BuildDtoAsync_WithNoResendChannel_ReturnsDtoWithCanResendFalse()
    {
        var packet = new Packet
        {
            Id = 2,
            Channel = ErrorChannel,
            BinaryData = Encoding.UTF8.GetBytes("Error"),
            Status = PacketStatus.FatalError,
            DateCreated = DateTime.Now.AddMinutes(-15),
            DateChanged = DateTime.Now.AddMinutes(-10),
            Metadata = "meta",
            DynamicField = "dynamic",
            ParentId = 1,
            RetryCount = 0
        };

        var expectedData = "Error";
        var expectedDataPreview = "Err";
        var expectedDataType = UIDataTypes.Plaintext;

        _packetTransfer.Converter.PacketToUIDataConverter(Arg.Any<PacketData>())
            .Returns(new UIConversionInfo { Data = expectedData, DataPreview = expectedDataPreview, DataType = expectedDataType });

        var packetDto = await _packetDtoService.BuildDtoAsync(packet);

        await _packetTransfer.Converter.Received(1).PacketToUIDataConverter(Arg.Any<PacketData>());

        // Properties should be copied from the packet
        AssertPacketDtoMatchesPacket(packet, packetDto);

        // Data properties should be set by the conversion
        packetDto.Data.Should().Be(expectedData);
        packetDto.PreviewData.Should().Be(expectedDataPreview);
        packetDto.DataType.Should().Be(expectedDataType);

        // Other properties should be set by the service correctly
        packetDto.CanResend.Should().BeFalse();
        packetDto.ConnectorName.Should().Be(_metadata.TemplateName);
    }

    [TestMethod]
    public async Task BuildDtoAsync_WithUnknownChannel_ReturnsDtoWithCanResendFalse()
    {
        var packet = new Packet
        {
            Id = 10,
            Channel = "SomeOtherChannel",
            BinaryData = Encoding.UTF8.GetBytes("Data"),
            Status = PacketStatus.Processed,
            DateCreated = DateTime.Now,
        };

        _packetTransfer.Converter.Returns(new StringConverter());

        var packetDto = await _packetDtoService.BuildDtoAsync(packet);

        // Properties should be copied from the packet
        AssertPacketDtoMatchesPacket(packet, packetDto);

        // Data properties should be set by the conversion
        packetDto.Data.Should().Be("Data");
        packetDto.PreviewData.Should().Be("Data");
        packetDto.DataType.Should().Be(UIDataTypes.Plaintext);

        // Other properties should be set by the service correctly
        packetDto.CanResend.Should().BeFalse();
        packetDto.ConnectorName.Should().Be(_metadata.TemplateName);
    }

    [TestMethod]
    public async Task BuildDtoAsync_WhenConversionFails_UsesDefaultConversion()
    {
        var expectedData = "Lorem Ipsum is simply dummy text of the printing and typesetting industry.";
        var expectedDataPreview = "Lorem Ipsum is simpl";
        var expectedDataType = UIDataTypes.Plaintext;

        var packet = new Packet
        {
            Id = 1,
            Channel = TestChannel,
            BinaryData = Encoding.UTF8.GetBytes(expectedData),
            Status = PacketStatus.Error,
            DateCreated = DateTime.Now.AddMinutes(-1),
        };

        _packetTransfer.Converter.Throws<NotImplementedException>();

        var packetDto = await _packetDtoService.BuildDtoAsync(packet);

        // Properties should be copied from the packet
        AssertPacketDtoMatchesPacket(packet, packetDto);

        // Data properties should be set by the conversion
        packetDto.Data.Should().Be(expectedData);
        packetDto.PreviewData.Should().Be(expectedDataPreview);
        packetDto.DataType.Should().Be(expectedDataType);

        // Other properties should be set by the service correctly
        packetDto.CanResend.Should().BeTrue();
        packetDto.ConnectorName.Should().Be(_metadata.TemplateName);
    }

    [TestMethod]
    public async Task FillChildAndParentPacketsAsync_WithEmptyList_NoChanges()
    {
        var ignoredIds = new HashSet<long>();
        var packets = new List<PacketDto>();

        await using var context = await _dbContextFactory.CreateDbContextAsync();

        await _packetDtoService.FillChildAndParentPacketsAsync(context, packets, ignoredIds);

        packets.Should().BeEmpty();
        ignoredIds.Should().BeEmpty();
    }

    [TestMethod]
    public async Task FillChildAndParentPacketsAsync_WithNoMatchingParents_NoChanges()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.Packet.AddRangeAsync([
            new() { Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now},
            new() { Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now},
            new() { Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now}
        ]);
        await context.SaveChangesAsync();

        var ignoredIds = new HashSet<long>();
        var packets = new List<PacketDto>
        {
            new() { Id = 10, ParentId = 98, Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now },
            new() { Id = 11, ParentId = 99, Channel = TestChannel, Status = PacketStatus.FatalError, DateCreated = DateTime.Now }
        };

        await _packetDtoService.FillChildAndParentPacketsAsync(context, packets, ignoredIds);

        packets.Select(p => p.Id).Should().BeEquivalentTo([10, 11]);
        ignoredIds.Should().BeEmpty();
    }

    [TestMethod]
    public async Task FillChildAndParentPacketsAsync_WithChildPackets_AddsParents()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.Packet.AddRangeAsync([
            new() { Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now},
            new() { Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now},
            new() { Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now}
        ]);
        await context.SaveChangesAsync();

        var ignoredIds = new HashSet<long>();
        var packets = new List<PacketDto>
        {
            new() { Id = 10, ParentId = 1, Channel = TestChannel, Status = PacketStatus.InProgress, DateCreated = DateTime.Now },
            new() { Id = 11, ParentId = 2, Channel = TestChannel, Status = PacketStatus.FatalError, DateCreated = DateTime.Now },
            new() { Id = 12, ParentId = 99, Channel = TestChannel, Status = PacketStatus.ManualStop, DateCreated = DateTime.Now }
        };

        await _packetDtoService.FillChildAndParentPacketsAsync(context, packets, ignoredIds);

        packets.Select(p => p.Id).Should().BeEquivalentTo([1, 2, 10, 11, 12]);

        // The added parents should be ignored on the next call
        ignoredIds.Should().BeEquivalentTo([1, 2]);
    }

    [TestMethod]
    public async Task FillChildAndParentPacketsAsync_WithIgnoredParents_DoesNotAddParents()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.Packet.AddRangeAsync([
            new() { Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now},
            new() { Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now},
            new() { Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now},
            new() { Channel = TestChannel, Status = PacketStatus.InProgress, DateCreated = DateTime.Now}
        ]);
        await context.SaveChangesAsync();

        var ignoredIds = new HashSet<long> { 1, 2 };
        var packets = new List<PacketDto>
        {
            new() { Id = 10, ParentId = 1, Channel = TestChannel, Status = PacketStatus.InProgress, DateCreated = DateTime.Now },
            new() { Id = 11, ParentId = 2, Channel = TestChannel, Status = PacketStatus.FatalError, DateCreated = DateTime.Now },
            new() { Id = 12, ParentId = 2, Channel = TestChannel, Status = PacketStatus.ManualStop, DateCreated = DateTime.Now },
            new() { Id = 13, ParentId = 99, Channel = TestChannel, Status = PacketStatus.ManualStop, DateCreated = DateTime.Now },
            new() { Id = 14, ParentId = 3, Channel = TestChannel, Status = PacketStatus.ManualStop, DateCreated = DateTime.Now }
        };

        await _packetDtoService.FillChildAndParentPacketsAsync(context, packets, ignoredIds);

        packets.Select(p => p.Id).Should().BeEquivalentTo([3, 10, 11, 12, 13, 14]);
        ignoredIds.Should().BeEquivalentTo([1, 2, 3]);
    }

    [TestMethod]
    public async Task FillChildAndParentPacketsAsync_WithParentPackets_AddsChilds()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.Packet.AddRangeAsync([
            new() { ParentId = 10, Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now},
            new() { ParentId = 12, Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now},
            new() { ParentId = 12, Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now},
            new() { ParentId = 99, Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now},
            new() { ParentId = 13, Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now}
        ]);
        await context.SaveChangesAsync();

        var ignoredIds = new HashSet<long>();
        var packets = new List<PacketDto>
        {
            new() { Id = 10, ParentId = null, Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now },
            new() { Id = 11, ParentId = null, Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now },
            new() { Id = 12, ParentId = null, Channel = TestChannel, Status = PacketStatus.InProgress, DateCreated = DateTime.Now },
            new() { Id = 13, ParentId = null, Channel = TestChannel, Status = PacketStatus.FatalError, DateCreated = DateTime.Now },
        };

        await _packetDtoService.FillChildAndParentPacketsAsync(context, packets, ignoredIds);

        packets.Select(p => p.Id).Should().BeEquivalentTo([1, 2, 3, 5, 10, 11, 12, 13]);

        // The added childs should be ignored on the next call
        ignoredIds.Should().BeEquivalentTo([1, 2, 3, 5]);
    }

    [TestMethod]
    public async Task FillChildAndParentPacketsAsync_WithIgnoredChilds_DoesNotAddChilds()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.Packet.AddRangeAsync([
            new() { ParentId = 10, Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now},
            new() { ParentId = 12, Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now},
            new() { ParentId = 12, Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now},
            new() { ParentId = 99, Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now},
            new() { ParentId = 13, Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now}
        ]);
        await context.SaveChangesAsync();

        var ignoredIds = new HashSet<long> { 1, 3 };
        var packets = new List<PacketDto>
        {
            new() { Id = 10, ParentId = null, Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now },
            new() { Id = 11, ParentId = null, Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now },
            new() { Id = 12, ParentId = null, Channel = TestChannel, Status = PacketStatus.InProgress, DateCreated = DateTime.Now },
            new() { Id = 13, ParentId = null, Channel = TestChannel, Status = PacketStatus.FatalError, DateCreated = DateTime.Now },
        };

        await _packetDtoService.FillChildAndParentPacketsAsync(context, packets, ignoredIds);

        packets.Select(p => p.Id).Should().BeEquivalentTo([2, 5, 10, 11, 12, 13]);
        ignoredIds.Should().BeEquivalentTo([1, 2, 3, 5]);
    }

    private static void AssertPacketDtoMatchesPacket(Packet packet, PacketDto dto)
    {
        dto.Id.Should().Be(packet.Id);
        dto.Channel.Should().Be(packet.Channel);
        dto.Status.Should().Be(packet.Status);
        dto.DateCreated.Should().Be(packet.DateCreated);
        dto.DateChanged.Should().Be(packet.DateChanged);
        dto.Metadata.Should().Be(packet.Metadata);
        dto.DynamicField.Should().Be(packet.DynamicField);
        dto.ParentId.Should().Be(packet.ParentId);
        dto.RetryCount.Should().Be(packet.RetryCount);
    }

    [TestCleanup]
    public async Task TestCleanup()
    {
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }
}
