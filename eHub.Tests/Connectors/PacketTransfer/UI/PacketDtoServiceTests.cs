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
using eHub.Tests.Helper;

namespace eHub.Tests.Connectors.PacketTransfer.UI;

public class PacketDtoServiceTests : IAsyncLifetime
{
    private const string TestChannel = "TestChannel";
    private const string ErrorChannel = "ErroChannel";

    private PacketDtoService _packetDtoService = null!;
    private ConnectorMetadata _metadata = null!;
    private ConnectorTemplate _connectorTemplate = null!;
    private ILoggerFactory _loggerFactory = null!;
    private IPacketTransfer _packetTransfer = null!;
    private IPacketConverter _packetConverter = null!;
    private IDbContextFactory<HubDbContext> _dbContextFactory = null!;
    private ILogger<PacketDtoService> _logger = null!;
    private SqliteConnection _connection = null!;

    public async ValueTask InitializeAsync()
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

    [Fact]
    public async Task BuildDtoAsync_WhenConversionSucceeds_ReturnsConvertedDto()
    {
        // Arrange
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

        const string expectedData = "{\"Id\": 4}";
        const string expectedDataPreview = "Id: 4";
        const string expectedDataType = UIDataTypes.Json;

        _packetTransfer.Converter.PacketToUIDataConverter(Arg.Any<PacketData>(), Arg.Any<CancellationToken>())
            .Returns(new UIConversionInfo { Data = expectedData, DataPreview = expectedDataPreview, DataType = expectedDataType });
        
        // Act
        var packetDto = await _packetDtoService.BuildDtoAsync(packet);
        
        // Assert
        await _packetTransfer.Converter.Received(1).PacketToUIDataConverter(Arg.Any<PacketData>(), Arg.Any<CancellationToken>());

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

    [Fact]
    public async Task BuildDtoAsync_WithNoResendChannel_ReturnsDtoWithCanResendFalse()
    {
        // Arrange
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

        const string expectedData = "Error";
        const string expectedDataPreview = "Err";
        const string expectedDataType = UIDataTypes.Plaintext;

        _packetTransfer.Converter.PacketToUIDataConverter(Arg.Any<PacketData>(), Arg.Any<CancellationToken>())
            .Returns(new UIConversionInfo { Data = expectedData, DataPreview = expectedDataPreview, DataType = expectedDataType });
        
        // Act
        var packetDto = await _packetDtoService.BuildDtoAsync(packet);
        
        // Assert
        await _packetTransfer.Converter.Received(1).PacketToUIDataConverter(Arg.Any<PacketData>(), Arg.Any<CancellationToken>());

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

    [Fact]
    public async Task BuildDtoAsync_WithUnknownChannel_ReturnsDtoWithCanResendFalse()
    {
        // Arrange
        var packet = new Packet
        {
            Id = 10,
            Channel = "SomeOtherChannel",
            BinaryData = Encoding.UTF8.GetBytes("Data"),
            Status = PacketStatus.Processed,
            DateCreated = DateTime.Now,
        };

        _packetTransfer.Converter.Returns(new StringConverter());

        // Act
        var packetDto = await _packetDtoService.BuildDtoAsync(packet);
        
        // Assert
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

    [Fact]
    public async Task BuildDtoAsync_WhenConversionFails_UsesDefaultConversion()
    {
        // Arrange
        const string expectedData = "Lorem Ipsum is simply dummy text of the printing and typesetting industry.";
        const string expectedDataPreview = "Lorem Ipsum is simpl";
        const string expectedDataType = UIDataTypes.Plaintext;

        var packet = new Packet
        {
            Id = 1,
            Channel = TestChannel,
            BinaryData = Encoding.UTF8.GetBytes(expectedData),
            Status = PacketStatus.Error,
            DateCreated = DateTime.Now.AddMinutes(-1),
        };

        _packetTransfer.Converter.Throws<NotImplementedException>();

        // Act
        var packetDto = await _packetDtoService.BuildDtoAsync(packet);
        
        // Assert
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

    [Fact]
    public async Task FillChildAndParentPacketsAsync_WithEmptyList_NoChanges()
    {
        // Arrange
        var ignoredIds = new HashSet<long>();
        var packets = new List<PacketDto>();

        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        
        // Act
        await _packetDtoService.FillChildAndParentPacketsAsync(context, packets, ignoredIds);
        
        // Assert
        packets.Should().BeEmpty();
        ignoredIds.Should().BeEmpty();
    }

    [Fact]
    public async Task FillChildAndParentPacketsAsync_WithNoMatchingParents_NoChanges()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.Packet.AddRangeAsync([
            new() { Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now},
            new() { Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now},
            new() { Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now}
        ]);
        
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var ignoredIds = new HashSet<long>();
        var packets = new List<PacketDto>
        {
            new() { Id = 10, ParentId = 98, Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now },
            new() { Id = 11, ParentId = 99, Channel = TestChannel, Status = PacketStatus.FatalError, DateCreated = DateTime.Now }
        };
        
        // Act
        await _packetDtoService.FillChildAndParentPacketsAsync(context, packets, ignoredIds);
        
        // Assert
        packets.Select(p => p.Id).Should().BeEquivalentTo([10, 11]);
        ignoredIds.Should().BeEmpty();
    }

    [Fact]
    public async Task FillChildAndParentPacketsAsync_WithChildPackets_AddsParents()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.Packet.AddRangeAsync([
            new() { Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now},
            new() { Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now},
            new() { Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now}
        ]);
        
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var ignoredIds = new HashSet<long>();
        var packets = new List<PacketDto>
        {
            new() { Id = 10, ParentId = 1, Channel = TestChannel, Status = PacketStatus.InProgress, DateCreated = DateTime.Now },
            new() { Id = 11, ParentId = 2, Channel = TestChannel, Status = PacketStatus.FatalError, DateCreated = DateTime.Now },
            new() { Id = 12, ParentId = 99, Channel = TestChannel, Status = PacketStatus.ManualStop, DateCreated = DateTime.Now }
        };
        
        // Act
        await _packetDtoService.FillChildAndParentPacketsAsync(context, packets, ignoredIds);
        
        // Assert
        packets.Select(p => p.Id).Should().BeEquivalentTo([1, 2, 10, 11, 12]);

        // The added parents should be ignored on the next call
        ignoredIds.Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public async Task FillChildAndParentPacketsAsync_WithIgnoredParents_DoesNotAddParents()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.Packet.AddRangeAsync([
            new() { Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now},
            new() { Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now},
            new() { Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now},
            new() { Channel = TestChannel, Status = PacketStatus.InProgress, DateCreated = DateTime.Now}
        ]);
        
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var ignoredIds = new HashSet<long> { 1, 2 };
        var packets = new List<PacketDto>
        {
            new() { Id = 10, ParentId = 1, Channel = TestChannel, Status = PacketStatus.InProgress, DateCreated = DateTime.Now },
            new() { Id = 11, ParentId = 2, Channel = TestChannel, Status = PacketStatus.FatalError, DateCreated = DateTime.Now },
            new() { Id = 12, ParentId = 2, Channel = TestChannel, Status = PacketStatus.ManualStop, DateCreated = DateTime.Now },
            new() { Id = 13, ParentId = 99, Channel = TestChannel, Status = PacketStatus.ManualStop, DateCreated = DateTime.Now },
            new() { Id = 14, ParentId = 3, Channel = TestChannel, Status = PacketStatus.ManualStop, DateCreated = DateTime.Now }
        };
        
        // Act
        await _packetDtoService.FillChildAndParentPacketsAsync(context, packets, ignoredIds);
        
        // Assert
        packets.Select(p => p.Id).Should().BeEquivalentTo([3, 10, 11, 12, 13, 14]);
        ignoredIds.Should().BeEquivalentTo([1, 2, 3]);
    }

    [Fact]
    public async Task FillChildAndParentPacketsAsync_WithParentPackets_AddsChildren()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.Packet.AddRangeAsync([
            new() { ParentId = 10, Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now},
            new() { ParentId = 12, Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now},
            new() { ParentId = 12, Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now},
            new() { ParentId = 99, Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now},
            new() { ParentId = 13, Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now}
        ]);
        
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var ignoredIds = new HashSet<long>();
        var packets = new List<PacketDto>
        {
            new() { Id = 10, ParentId = null, Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now },
            new() { Id = 11, ParentId = null, Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now },
            new() { Id = 12, ParentId = null, Channel = TestChannel, Status = PacketStatus.InProgress, DateCreated = DateTime.Now },
            new() { Id = 13, ParentId = null, Channel = TestChannel, Status = PacketStatus.FatalError, DateCreated = DateTime.Now },
        };
        
        // Act
        await _packetDtoService.FillChildAndParentPacketsAsync(context, packets, ignoredIds);
        
        // Assert
        packets.Select(p => p.Id).Should().BeEquivalentTo([1, 2, 3, 5, 10, 11, 12, 13]);

        // The added children should be ignored on the next call
        ignoredIds.Should().BeEquivalentTo([1, 2, 3, 5]);
    }

    [Fact]
    public async Task FillChildAndParentPacketsAsync_WithIgnoredChildren_DoesNotAddChildren()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.Packet.AddRangeAsync([
            new() { ParentId = 10, Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now},
            new() { ParentId = 12, Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now},
            new() { ParentId = 12, Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now},
            new() { ParentId = 99, Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now},
            new() { ParentId = 13, Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now}
        ]);
        
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var ignoredIds = new HashSet<long> { 1, 3 };
        var packets = new List<PacketDto>
        {
            new() { Id = 10, ParentId = null, Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now },
            new() { Id = 11, ParentId = null, Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now },
            new() { Id = 12, ParentId = null, Channel = TestChannel, Status = PacketStatus.InProgress, DateCreated = DateTime.Now },
            new() { Id = 13, ParentId = null, Channel = TestChannel, Status = PacketStatus.FatalError, DateCreated = DateTime.Now },
        };
        
        // Act
        await _packetDtoService.FillChildAndParentPacketsAsync(context, packets, ignoredIds);
        
        // Assert
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

    public async ValueTask DisposeAsync()
    {
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }
}
