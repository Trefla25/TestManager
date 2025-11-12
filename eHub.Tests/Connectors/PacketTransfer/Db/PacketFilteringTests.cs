using System.Data.Common;
using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eHub.Database;
using eHub.Database.Models;
using eHub.PlugIn;
using eHub.Scripting.Connectors.Helper;
using eHub.Tests.Helper;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace eHub.Tests.Connectors.PacketTransfer.Db;

public class PacketFilteringTests : IAsyncLifetime
{
    private IDbContextFactory<HubDbContext> _dbContextFactory = null!;
    private DbConnection _connection = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection(DbHelper.InMemoryConnectionString);
        await _connection.OpenAsync();

        _dbContextFactory = DbHelper.CreateInMemoryFactory<HubDbContext>(_connection);

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
    }

    [Fact]
    public async Task ApplyFilterDto_WithDateTimeStart_ReturnsQuery()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, dateTimeNow.AddMinutes(-10), dateTimeNow);

        var filterStartDate = dateTimeNow.AddMinutes(-5);
        var expectedPackets = await context.Packet
            .Where(p => p.DateCreated >= filterStartDate)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        var filter = new PacketRequestDto(
            DateTimeStart: filterStartDate);

        // Act
        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync(TestContext.Current.CancellationToken);

        // Assert
        packets.Should().HaveCount(expectedPackets.Length);
        packets.Should().BeEquivalentTo(expectedPackets);
    }

    [Fact]
    public async Task ApplyFilterDto_WithDateTimeEnd_ReturnsQuery()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, dateTimeNow.AddMinutes(-10), dateTimeNow);

        var filterEndDate = dateTimeNow.AddMinutes(-10);
        var expectedPackets = await context.Packet
            .Where(p => p.DateCreated <= filterEndDate)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        var filter = new PacketRequestDto(
            DateTimeEnd: filterEndDate);

        // Act
        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync(TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(expectedPackets.Length);
        packets.Should().BeEquivalentTo(expectedPackets);
    }

    [Fact]
    public async Task ApplyFilterDto_WithDateTimeRange_ReturnsQuery()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, dateTimeNow.AddMinutes(-10), dateTimeNow.AddMinutes(10));

        var filterStartDate = dateTimeNow.AddMinutes(-5);
        var filterEndDate = dateTimeNow.AddMinutes(5);

        var expectedPackets = await context.Packet
            .Where(p => p.DateCreated >= filterStartDate && p.DateCreated <= filterEndDate)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        var filter = new PacketRequestDto(
            DateTimeStart: filterStartDate,
            DateTimeEnd: filterEndDate);

        // Act
        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync(TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(expectedPackets.Length);
        packets.Should().BeEquivalentTo(expectedPackets);
    }

    [Fact]
    public async Task ApplyFilterDto_WithExactDateTime_ReturnsQuery()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(3, dateTimeNow.AddMinutes(-10), dateTimeNow.AddMinutes(10));

        var expectedPackets = await context.Packet
            .Where(p => p.DateCreated == dateTimeNow)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        var filter = new PacketRequestDto(
            DateTimeStart: dateTimeNow,
            DateTimeEnd: dateTimeNow);

        // Act
        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync(TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(expectedPackets.Length);
        packets.Should().BeEquivalentTo(expectedPackets);
    }

    [Fact]
    public async Task ApplyFilterDto_WithInvalidDateTimeRange_ReturnsEmpty()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, dateTimeNow.AddMinutes(-10), dateTimeNow.AddMinutes(10));

        var filterStartDate = dateTimeNow.AddMinutes(5);
        var filterEndDate = dateTimeNow.AddMinutes(-5);
        var filter = new PacketRequestDto(
            DateTimeStart: filterStartDate,
            DateTimeEnd: filterEndDate);

        // Act
        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync(TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyFilterDto_WithMinId_ReturnsQuery()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, dateTimeNow.AddMinutes(-10), dateTimeNow);

        const int filterMinId = 4;
        var expectedPackets = await context.Packet
            .Where(p => p.Id >= filterMinId)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        var filter = new PacketRequestDto(
            MinId: filterMinId);

        // Act
        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync(TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(expectedPackets.Length);
        packets.Should().BeEquivalentTo(expectedPackets);
    }

    [Fact]
    public async Task ApplyFilterDto_WithMaxId_ReturnsQuery()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, dateTimeNow.AddMinutes(-10), dateTimeNow);

        const int filterMaxId = 6;
        var expectedPackets = await context.Packet
            .Where(p => p.Id <= filterMaxId)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        var filter = new PacketRequestDto(
            MaxId: filterMaxId);

        // Act
        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync(TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(expectedPackets.Length);
        packets.Should().BeEquivalentTo(expectedPackets);
    }

    [Fact]
    public async Task ApplyFilterDto_WithIdRange_ReturnsQuery()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, dateTimeNow.AddMinutes(-10), dateTimeNow);

        const int filterMinId = 2;
        const int filterMaxId = 8;
        var expectedPackets = await context.Packet
            .Where(p => p.Id >= filterMinId && p.Id <= filterMaxId)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        var filter = new PacketRequestDto(
            MinId: filterMinId,
            MaxId: filterMaxId);

        // Act
        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync(TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(expectedPackets.Length);
        packets.Should().BeEquivalentTo(expectedPackets);
    }

    [Fact]
    public async Task ApplyFilterDto_WithExactId_ReturnsSingle()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, dateTimeNow.AddMinutes(-10), dateTimeNow);

        var filter = new PacketRequestDto(
            MinId: 5,
            MaxId: 5);

        // Act
        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync(TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(1);
        packets.Single().Id.Should().Be(5);
    }

    [Fact]
    public async Task ApplyFilterDto_WithInvalidIdRange_ReturnsEmpty()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, dateTimeNow.AddMinutes(-10), dateTimeNow);

        const int filterMinId = 8;
        const int filterMaxId = 2;
        var filter = new PacketRequestDto(
            MinId: filterMinId,
            MaxId: filterMaxId);

        // Act
        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync(TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyFilterDto_WithChannels_ReturnsQuery()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(15, dateTimeNow.AddMinutes(-10), dateTimeNow, ["channel1", "channel2", "channel3"]);

        var filterChannels = new[] { "channel1", "channel3" };
        var expectedPackets = await context.Packet
            .Where(p => filterChannels.Contains(p.Channel))
            .ToArrayAsync(TestContext.Current.CancellationToken);

        var filter = new PacketRequestDto(
            Channels: [.. filterChannels]);

        // Act
        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync(TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(expectedPackets.Length);
        packets.Should().BeEquivalentTo(expectedPackets);
    }

    [Fact]
    public async Task ApplyFilterDto_WithColumns_ReturnsQuery()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(15, dateTimeNow.AddMinutes(-10), dateTimeNow, ["channel1", "channel2", "channel3"]);

        var columnFilters = new ColumnFilterDto[]
        {
            new(nameof(Packet.Status), ColumnFilterOperator.Equal, nameof(PacketStatus.Processed)),
            new(nameof(Packet.Channel), ColumnFilterOperator.NotEqual, "channel1")
        };

        var expectedPackets = await context.Packet
            .Where(p => p.Status == PacketStatus.Processed && p.Channel != "channel1")
            .ToArrayAsync(TestContext.Current.CancellationToken);

        var filter = new PacketRequestDto(
            ColumnFilters: [.. columnFilters]);

        // Act
        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync(TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(expectedPackets.Length);
        packets.Should().BeEquivalentTo(expectedPackets);
    }

    [Fact]
    public async Task ApplyFilterDto_WhenAllFiltersApplied_ReturnsQuery()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(15, dateTimeNow.AddMinutes(-10), dateTimeNow.AddMinutes(10), ["channel1", "channel2", "channel3"]);

        var filterStartDate = dateTimeNow.AddMinutes(-5);
        var filterEndDate = dateTimeNow.AddMinutes(5);
        const int filterMinId = 3;
        const int filterMaxId = 13;
        var filterChannels = new[] { "channel1", "channel2" };
        var columnFilters = new ColumnFilterDto[]
        {
            new(nameof(Packet.Status), ColumnFilterOperator.Equal, nameof(PacketStatus.Processed)),
        };

        var expectedPackets = await context.Packet
            .Where(p => p.DateCreated >= filterStartDate && p.DateCreated <= filterEndDate)
            .Where(p => p.Id >= filterMinId && p.Id <= filterMaxId)
            .Where(p => filterChannels.Contains(p.Channel))
            .Where(p => p.Status == PacketStatus.Processed)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        var filter = new PacketRequestDto(
            DateTimeStart: filterStartDate,
            DateTimeEnd: filterEndDate,
            MinId: filterMinId,
            MaxId: filterMaxId,
            Channels: [.. filterChannels],
            ColumnFilters: [.. columnFilters]);

        // Act
        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync(TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(expectedPackets.Length);
        packets.Should().BeEquivalentTo(expectedPackets);
    }

    [Fact]
    public async Task ApplyFilterDto_NoMatches_ReturnsEmpty()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, dateTimeNow.AddMinutes(-10), dateTimeNow);

        var filter = new PacketRequestDto(
            MinId: 11);

        // Act
        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnIdEqual_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Id), ColumnFilterOperator.Equal, "7");

        // Act
        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().ContainSingle(p => p.Id == 7);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnIdNotEqual_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Id), ColumnFilterOperator.NotEqual, "3");

        // Act
        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(9);
        packets.Should().OnlyContain(p => p.Id != 3);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnIdContains_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Id), ColumnFilterOperator.Contains, "5");
        
        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"Contains operator is not supported for type {typeof(long)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnIdNotContains_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Id), ColumnFilterOperator.NotContains, "4");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"NotContains operator is not supported for type {typeof(long)}");
        
        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnIdGreaterThan_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Id), ColumnFilterOperator.GreaterThan, "4");

        // Act
        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().OnlyContain(p => p.Id > 4);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnIdGreaterThanOrEqual_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        var filter = new ColumnFilterDto(nameof(Packet.Id), ColumnFilterOperator.GreaterThanOrEqual, "4");

        // Act
        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().OnlyContain(p => p.Id >= 4);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnIdLessThan_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        var filter = new ColumnFilterDto(nameof(Packet.Id), ColumnFilterOperator.LessThan, "6");

        // Act
        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().OnlyContain(p => p.Id < 6);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnIdLessThanOrEqual_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        var filter = new ColumnFilterDto(nameof(Packet.Id), ColumnFilterOperator.LessThanOrEqual, "6");

        // Act
        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().OnlyContain(p => p.Id <= 6);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnIdStartsWith_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        var filter = new ColumnFilterDto(nameof(Packet.Id), ColumnFilterOperator.StartsWith, "3");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"StartsWith operator is not supported for type {typeof(long)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnIdEndsWith_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Id), ColumnFilterOperator.EndsWith, "3");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"EndsWith operator is not supported for type {typeof(long)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnIdEmpty_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Id), ColumnFilterOperator.Empty, null);

        // Act
        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnIdNotEmpty_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Id), ColumnFilterOperator.NotEmpty, null);

        // Act
        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(10);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnMetadataEqual_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        foreach (var packet in context.Packet)
        {
            packet.Metadata = $"Metadata{packet.Id}";
        }
        
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var filter = new ColumnFilterDto(nameof(Packet.Metadata), ColumnFilterOperator.Equal, "Metadata3");

        // Act
        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().ContainSingle();
        packets.Single().Metadata.Should().Be("Metadata3");
    }

    [Fact]
    public async Task ApplyColumnFilter_OnMetadataNotEqual_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        foreach (var packet in context.Packet)
        {
            packet.Metadata = $"Metadata{packet.Id}";
        }
        
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var filter = new ColumnFilterDto(nameof(Packet.Metadata), ColumnFilterOperator.NotEqual, "Metadata3");

        // Act
        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(9);
        packets.Should().OnlyContain(p => p.Metadata != "Metadata3");
    }

    [Fact]
    public async Task ApplyColumnFilter_OnMetadataContains_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "SomeMetadata"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "Metdata4"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "Met5data"), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.Metadata), ColumnFilterOperator.Contains, "t5da");

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().ContainSingle();
        packets.Single().Metadata.Should().Be("Met5data");
    }

    [Fact]
    public async Task ApplyColumnFilter_OnMetadataNotContains_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "SomeMetadata"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "Metdata4"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "Met5data"), cancellationToken: TestContext.Current.CancellationToken);
        var filter = new ColumnFilterDto(nameof(Packet.Metadata), ColumnFilterOperator.NotContains, "t5da");

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(9);
        packets.Should().OnlyContain(p => p.Metadata != "Met5data");
    }

    [Fact]
    public async Task ApplyColumnFilter_OnMetadataGreaterThan_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Metadata), ColumnFilterOperator.GreaterThan, "Metadata");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"GreaterThan operator is not supported for type {typeof(string)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnMetadataGreaterThanOrEqual_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Metadata), ColumnFilterOperator.GreaterThanOrEqual, "Metadata");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"GreaterThanOrEqual operator is not supported for type {typeof(string)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnMetadataLessThan_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Metadata), ColumnFilterOperator.LessThan, "Metadata");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"LessThan operator is not supported for type {typeof(string)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnMetadataLessThanOrEqual_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Metadata), ColumnFilterOperator.LessThanOrEqual, "Metadata");

        // Act
        var act = () => context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"LessThanOrEqual operator is not supported for type {typeof(string)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnMetadataStartsWith_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "SomeMetadata"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "Metadata4"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "Met5data"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "Metadata6"), cancellationToken: TestContext.Current.CancellationToken);
        var filter = new ColumnFilterDto(nameof(Packet.Metadata), ColumnFilterOperator.StartsWith, "Metadata");

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => p.Metadata != null && p.Metadata.StartsWith("Metadata"));
    }

    [Fact]
    public async Task ApplyColumnFilter_OnMetadataEndsWith_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "SomeMetadata"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "4Metadata"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "Met5data"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "Metadata6"), cancellationToken: TestContext.Current.CancellationToken);
        var filter = new ColumnFilterDto(nameof(Packet.Metadata), ColumnFilterOperator.EndsWith, "Metadata");

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => p.Metadata != null && p.Metadata.EndsWith("Metadata"));
    }

    [Fact]
    public async Task ApplyColumnFilter_OnMetadataEmpty_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "SomeMetadata"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 2).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, ""), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "Metdata4"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "Met5data"), cancellationToken: TestContext.Current.CancellationToken);
        var filter = new ColumnFilterDto(nameof(Packet.Metadata), ColumnFilterOperator.Empty, null);

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(7);
        packets.Should().OnlyContain(p => string.IsNullOrEmpty(p.Metadata));
    }

    [Fact]
    public async Task ApplyColumnFilter_OnMetadataNotEmpty_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "SomeMetadata"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 2).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, ""), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "Metdata4"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "Met5data"), cancellationToken: TestContext.Current.CancellationToken);
        var filter = new ColumnFilterDto(nameof(Packet.Metadata), ColumnFilterOperator.NotEmpty, null);

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(3);
        packets.Should().OnlyContain(p => !string.IsNullOrEmpty(p.Metadata));
    }

    [Fact]
    public async Task ApplyColumnFilter_OnStatusEqual_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        
        var filter = new ColumnFilterDto(nameof(Packet.Status), ColumnFilterOperator.Equal, nameof(PacketStatus.Processed));

        // Act
        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        var expectedPackets = await context.Packet.Where(p => p.Status == PacketStatus.Processed).ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().BeEquivalentTo(expectedPackets);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnStatusNotEqual_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Status), ColumnFilterOperator.NotEqual, nameof(PacketStatus.Processed));

        // Act
        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        var expectedPackets = await context.Packet.Where(p => p.Status != PacketStatus.Processed).ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        // Assert
        packets.Should().BeEquivalentTo(expectedPackets);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnStatusContains_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        var filter = new ColumnFilterDto(nameof(Packet.Status), ColumnFilterOperator.Contains, "queue");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"Contains operator is not supported for type {typeof(PacketStatus)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnStatusNotContains_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        var filter = new ColumnFilterDto(nameof(Packet.Status), ColumnFilterOperator.NotContains, "queue");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"NotContains operator is not supported for type {typeof(PacketStatus)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnStatusGreaterThan_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        var filter = new ColumnFilterDto(nameof(Packet.Status), ColumnFilterOperator.GreaterThan, "2");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"GreaterThan operator is not supported for type {typeof(PacketStatus)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnStatusGreaterThanOrEqual_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        var filter = new ColumnFilterDto(nameof(Packet.Status), ColumnFilterOperator.GreaterThanOrEqual, "2");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"GreaterThanOrEqual operator is not supported for type {typeof(PacketStatus)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnStatusLessThan_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        var filter = new ColumnFilterDto(nameof(Packet.Status), ColumnFilterOperator.LessThan, "4");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"LessThan operator is not supported for type {typeof(PacketStatus)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnStatusLessThanOrEqual_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        var filter = new ColumnFilterDto(nameof(Packet.Status), ColumnFilterOperator.LessThanOrEqual, "4");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"LessThanOrEqual operator is not supported for type {typeof(PacketStatus)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnStatusStartsWith_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        var filter = new ColumnFilterDto(nameof(Packet.Status), ColumnFilterOperator.StartsWith, "Pro");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"StartsWith operator is not supported for type {typeof(PacketStatus)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnStatusEndsWith_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        var filter = new ColumnFilterDto(nameof(Packet.Status), ColumnFilterOperator.EndsWith, "rror");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"EndsWith operator is not supported for type {typeof(PacketStatus)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnStatusEmpty_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        var filter = new ColumnFilterDto(nameof(Packet.Status), ColumnFilterOperator.Empty, null);

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"Empty operator is not supported for type {typeof(PacketStatus)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnStatusNotEmpty_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        var filter = new ColumnFilterDto(nameof(Packet.Status), ColumnFilterOperator.NotEmpty, null);

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"NotEmpty operator is not supported for type {typeof(PacketStatus)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnChannelEqual_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        foreach (var packet in context.Packet)
        {
            packet.Channel = $"Channel{packet.Id}";
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var filter = new ColumnFilterDto(nameof(Packet.Channel), ColumnFilterOperator.Equal, "Channel3");
        
        // Act
        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().ContainSingle();
        packets.Single().Channel.Should().Be("Channel3");
    }

    [Fact]
    public async Task ApplyColumnFilter_OnChannelNotEqual_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        foreach (var packet in context.Packet)
        {
            packet.Channel = $"Channel{packet.Id}";
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var filter = new ColumnFilterDto(nameof(Packet.Channel), ColumnFilterOperator.NotEqual, "Channel3");
        
        // Act
        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(9);
        packets.Should().OnlyContain(p => p.Channel != "Channel3");
    }

    [Fact]
    public async Task ApplyColumnFilter_OnChannelContains_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "SomeChannel"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "Channel4"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "Ch5nnel"), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.Channel), ColumnFilterOperator.Contains, "h5nn");
        
        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().ContainSingle();
        packets.Single().Channel.Should().Be("Ch5nnel");
    }

    [Fact]
    public async Task ApplyColumnFilter_OnChannelNotContains_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "SomeChannel"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "Channel4"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "Ch5nnel"), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.Channel), ColumnFilterOperator.NotContains, "h5nn");
        
        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(9);
        packets.Should().OnlyContain(p => p.Channel != "Ch5nnel");
    }

    [Fact]
    public async Task ApplyColumnFilter_OnChannelGreaterThan_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        var filter = new ColumnFilterDto(nameof(Packet.Channel), ColumnFilterOperator.GreaterThan, "Channel");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"GreaterThan operator is not supported for type {typeof(string)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnChannelGreaterThanOrEqual_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        
        var filter = new ColumnFilterDto(nameof(Packet.Channel), ColumnFilterOperator.GreaterThanOrEqual, "Channel");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"GreaterThanOrEqual operator is not supported for type {typeof(string)}");
        
        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnChannelLessThan_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Channel), ColumnFilterOperator.LessThan, "Channel");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"LessThan operator is not supported for type {typeof(string)}");
        
        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnChannelLessThanOrEqual_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Channel), ColumnFilterOperator.LessThanOrEqual, "Channel");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"LessThanOrEqual operator is not supported for type {typeof(string)}");
        
        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnChannelStartsWith_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "SomeChannel"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "Channel4"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "Ch5nnel"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "Channel6"), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.Channel), ColumnFilterOperator.StartsWith, "Channel");
        
        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => p.Channel.StartsWith("Channel"));
    }

    [Fact]
    public async Task ApplyColumnFilter_OnChannelEndsWith_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now, ["ChannelInvalid"]);
        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "SomeChannel"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "4Channel"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "Ch5nnel"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "Channel6"), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.Channel), ColumnFilterOperator.EndsWith, "Channel");
        
        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => p.Channel.EndsWith("Channel"));
    }

    [Fact]
    public async Task ApplyColumnFilter_OnChannelEmpty_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(5, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "SomeChannel"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 2).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, ""), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, ""), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "Channel4"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "Ch5nnel"), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.Channel), ColumnFilterOperator.Empty, null);
        
        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => string.IsNullOrEmpty(p.Channel));
    }

    [Fact]
    public async Task ApplyColumnFilter_OnChannelNotEmpty_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(5, DateTime.Now.AddMinutes(-10), DateTime.Now);
        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "SomeChannel"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 2).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, ""), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, ""), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "Channel4"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "Ch5nnel"), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.Channel), ColumnFilterOperator.NotEmpty, null);
        
        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(3);
        packets.Should().OnlyContain(p => !string.IsNullOrEmpty(p.Channel));
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDynamicFieldEqual_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        foreach (var packet in context.Packet)
        {
            packet.DynamicField = $"DynamicField{packet.Id}";
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var filter = new ColumnFilterDto(nameof(Packet.DynamicField), ColumnFilterOperator.Equal, "DynamicField3");
        
        // Act
        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().ContainSingle();
        packets.Single().DynamicField.Should().Be("DynamicField3");
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDynamicFieldNotEqual_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        foreach (var packet in context.Packet)
        {
            packet.DynamicField = $"DynamicField{packet.Id}";
        }
        
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var filter = new ColumnFilterDto(nameof(Packet.DynamicField), ColumnFilterOperator.NotEqual, "DynamicField3");

        // Act
        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(9);
        packets.Should().OnlyContain(p => p.DynamicField != "DynamicField3");
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDynamicFieldContains_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "SomeData"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "DynamicField4"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "Dyn5micField"), cancellationToken: TestContext.Current.CancellationToken);
        var filter = new ColumnFilterDto(nameof(Packet.DynamicField), ColumnFilterOperator.Contains, "n5mic");

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().ContainSingle();
        packets.Single().DynamicField.Should().Be("Dyn5micField");
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDynamicFieldNotContains_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "SomeData"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "DynamicField4"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "Dyn5micField"), cancellationToken: TestContext.Current.CancellationToken);
        var filter = new ColumnFilterDto(nameof(Packet.DynamicField), ColumnFilterOperator.NotContains, "n5mic");

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(9);
        packets.Should().OnlyContain(p => p.DynamicField != "Dyn5micField");
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDynamicFieldGreaterThan_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        var filter = new ColumnFilterDto(nameof(Packet.DynamicField), ColumnFilterOperator.GreaterThan, "DynamicField");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"GreaterThan operator is not supported for type {typeof(string)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDynamicFieldGreaterThanOrEqual_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        var filter = new ColumnFilterDto(nameof(Packet.DynamicField), ColumnFilterOperator.GreaterThanOrEqual, "DynamicField");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"GreaterThanOrEqual operator is not supported for type {typeof(string)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDynamicFieldLessThan_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.DynamicField), ColumnFilterOperator.LessThan, "DynamicField");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"LessThan operator is not supported for type {typeof(string)}");
        
        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDynamicFieldLessThanOrEqual_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.DynamicField), ColumnFilterOperator.LessThanOrEqual, "DynamicField");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"LessThanOrEqual operator is not supported for type {typeof(string)}");
        
        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDynamicFieldStartsWith_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "SomeDynamicField"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "DynamicField4"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "Dyn5micField"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "DynamicField6"), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.DynamicField), ColumnFilterOperator.StartsWith, "DynamicField");

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => p.DynamicField != null && p.DynamicField.StartsWith("DynamicField"));
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDynamicFieldEndsWith_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "SomeDynamicField"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "4DynamicField"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "Dyn5micField"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "DynamicField6"), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.DynamicField), ColumnFilterOperator.EndsWith, "DynamicField");
        
        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => p.DynamicField != null && p.DynamicField.EndsWith("DynamicField"));
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDynamicFieldEmpty_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "SomeDynamicField"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 2).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, ""), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "DynamicField4"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "Dyn5micField"), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.DynamicField), ColumnFilterOperator.Empty, null);

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(7);
        packets.Should().OnlyContain(p => string.IsNullOrEmpty(p.DynamicField));
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDynamicFieldNotEmpty_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "SomeDynamicField"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 2).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, ""), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "DynamicField4"), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "Dyn5micField"), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.DynamicField), ColumnFilterOperator.NotEmpty, null);

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(3);
        packets.Should().OnlyContain(p => !string.IsNullOrEmpty(p.DynamicField));
    }

    [Fact]
    public async Task ApplyColumnFilter_OnParentIdEqual_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 2), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 5), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 100), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.ParentId), ColumnFilterOperator.Equal, "5");
        
        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().OnlyContain(p => p.ParentId == 5);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnParentIdNotEqual_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 2), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 5), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 100), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.ParentId), ColumnFilterOperator.NotEqual, "2");

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(9);
        packets.Should().OnlyContain(p => p.ParentId != 2);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnParentIdContains_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        var filter = new ColumnFilterDto(nameof(Packet.ParentId), ColumnFilterOperator.Contains, "5");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"Contains operator is not supported for type {typeof(long?)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnParentIdNotContains_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        var filter = new ColumnFilterDto(nameof(Packet.ParentId), ColumnFilterOperator.NotContains, "4");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"NotContains operator is not supported for type {typeof(long?)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnParentIdGreaterThan_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 2), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 5), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 100), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.ParentId), ColumnFilterOperator.GreaterThan, "4");
        
        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => p.ParentId > 4);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnParentIdGreaterThanOrEqual_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 2), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 5), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 100), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.ParentId), ColumnFilterOperator.GreaterThanOrEqual, "5");

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => p.ParentId >= 5);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnParentIdLessThan_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 2), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 5), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 100), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.ParentId), ColumnFilterOperator.LessThan, "6");
        
        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => p.ParentId < 6);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnParentIdLessThanOrEqual_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 2), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 5), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 100), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.ParentId), ColumnFilterOperator.LessThanOrEqual, "2");

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(1);
        packets.Should().OnlyContain(p => p.ParentId <= 2);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnParentIdStartsWith_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.ParentId), ColumnFilterOperator.StartsWith, "3");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"StartsWith operator is not supported for type {typeof(long?)}");
        
        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnParentIdEndsWith_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        
        var filter = new ColumnFilterDto(nameof(Packet.ParentId), ColumnFilterOperator.EndsWith, "3");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"EndsWith operator is not supported for type {typeof(long?)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnParentIdEmpty_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 2), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 5), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 100), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.ParentId), ColumnFilterOperator.Empty, null);

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(7);
        packets.Should().OnlyContain(p => p.ParentId == null);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnParentIdNotEmpty_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 2), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 5), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 100), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.ParentId), ColumnFilterOperator.NotEmpty, null);

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(3);
        packets.Should().OnlyContain(p => p.ParentId != null);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnRetryCountEqual_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 2), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 5), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 100), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.RetryCount), ColumnFilterOperator.Equal, "5");

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().OnlyContain(p => p.RetryCount == 5);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnRetryCountNotEqual_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 2), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 5), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 100), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.RetryCount), ColumnFilterOperator.NotEqual, "2");

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(9);
        packets.Should().OnlyContain(p => p.RetryCount != 2);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnRetryCountContains_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        var filter = new ColumnFilterDto(nameof(Packet.RetryCount), ColumnFilterOperator.Contains, "5");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"Contains operator is not supported for type {typeof(int)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnRetryCountNotContains_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        var filter = new ColumnFilterDto(nameof(Packet.RetryCount), ColumnFilterOperator.NotContains, "4");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"NotContains operator is not supported for type {typeof(int)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnRetryCountGreaterThan_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 2), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 5), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 100), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.RetryCount), ColumnFilterOperator.GreaterThan, "4");

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => p.RetryCount > 4);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnRetryCountGreaterThanOrEqual_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 2), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 5), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 100), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.RetryCount), ColumnFilterOperator.GreaterThanOrEqual, "5");
        
        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => p.RetryCount >= 5);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnRetryCountLessThan_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 2), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 5), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 100), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.RetryCount), ColumnFilterOperator.LessThan, "5");
        
        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(8);
        packets.Should().OnlyContain(p => p.RetryCount < 5);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnRetryCountLessThanOrEqual_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 2), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 5), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 100), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.RetryCount), ColumnFilterOperator.LessThanOrEqual, "2");

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(8);
        packets.Should().OnlyContain(p => p.RetryCount <= 2);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnRetryCountStartsWith_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.RetryCount), ColumnFilterOperator.StartsWith, "3");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"StartsWith operator is not supported for type {typeof(int)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnRetryCountEndsWith_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.RetryCount), ColumnFilterOperator.EndsWith, "3");

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"EndsWith operator is not supported for type {typeof(int)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnRetryCountEmpty_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 2), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 5), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 100), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.RetryCount), ColumnFilterOperator.Empty, null);

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(7);
        packets.Should().OnlyContain(p => p.RetryCount == 0);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnRetryCountNotEmpty_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 2), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 5), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 100), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.RetryCount), ColumnFilterOperator.NotEmpty, null);

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(3);
        packets.Should().OnlyContain(p => p.RetryCount != 0);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDateCreatedEqual_ReturnsQuery()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;
        var startDateTime = DateTime.Now.AddMinutes(-10);
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, startDateTime, dateTimeNow);

        var filter = new ColumnFilterDto(nameof(Packet.DateCreated), ColumnFilterOperator.Equal, startDateTime.ToString("O"));

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().OnlyContain(p => p.DateCreated == startDateTime);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDateCreatedNotEqual_ReturnsQuery()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;
        var startDateTime = DateTime.Now.AddMinutes(-10);
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, startDateTime, dateTimeNow);

        var filter = new ColumnFilterDto(nameof(Packet.DateCreated), ColumnFilterOperator.NotEqual, startDateTime.ToString("O"));

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(9);
        packets.Should().OnlyContain(p => p.DateCreated != startDateTime);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDateCreatedContains_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        var filter = new ColumnFilterDto(nameof(Packet.DateCreated), ColumnFilterOperator.Contains, DateTime.Now.AddMinutes(-5).ToString("O"));

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"Contains operator is not supported for type {typeof(DateTime)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDateCreatedNotContains_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.DateCreated), ColumnFilterOperator.NotContains, DateTime.Now.AddMinutes(-5).ToString("O"));

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"NotContains operator is not supported for type {typeof(DateTime)}");
        
        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDateCreatedGreaterThan_ReturnsQuery()
    {
        // Arrange
        var startDate = DateTime.Now.AddMinutes(-10);
        var endDate = DateTime.Now;
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, startDate, endDate);

        var totalDuration = endDate - startDate;
        var interval = TimeSpan.FromTicks(totalDuration.Ticks / 9);
        var filterDate = startDate.Add(interval * 2);

        var filter = new ColumnFilterDto(nameof(Packet.DateCreated), ColumnFilterOperator.GreaterThan, filterDate.ToString("O"));
        
        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(7);
        packets.Should().OnlyContain(p => p.DateCreated > filterDate);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDateCreatedGreaterThanOrEqual_ReturnsQuery()
    {
        // Arrange
        var startDate = DateTime.Now.AddMinutes(-10);
        var endDate = DateTime.Now;
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, startDate, endDate);

        var totalDuration = endDate - startDate;
        var interval = TimeSpan.FromTicks(totalDuration.Ticks / 9);
        var filterDate = startDate.Add(interval * 2);

        var filter = new ColumnFilterDto(nameof(Packet.DateCreated), ColumnFilterOperator.GreaterThanOrEqual, filterDate.ToString("O"));

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(8);
        packets.Should().OnlyContain(p => p.DateCreated >= filterDate);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDateCreatedLessThan_ReturnsQuery()
    {
        // Arrange
        var startDate = DateTime.Now.AddMinutes(-10);
        var endDate = DateTime.Now;
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, startDate, endDate);

        var totalDuration = endDate - startDate;
        var interval = TimeSpan.FromTicks(totalDuration.Ticks / 9);
        var filterDate = startDate.Add(interval * 7);

        var filter = new ColumnFilterDto(nameof(Packet.DateCreated), ColumnFilterOperator.LessThan, filterDate.ToString("O"));

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(7);
        packets.Should().OnlyContain(p => p.DateCreated < filterDate);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDateCreatedLessThanOrEqual_ReturnsQuery()
    {
        // Arrange
        var startDate = DateTime.Now.AddMinutes(-10);
        var endDate = DateTime.Now;
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, startDate, endDate);
        var totalDuration = endDate - startDate;
        var interval = TimeSpan.FromTicks(totalDuration.Ticks / 9);
        var filterDate = startDate.Add(interval * 7);
        var filter = new ColumnFilterDto(nameof(Packet.DateCreated), ColumnFilterOperator.LessThanOrEqual, filterDate.ToString("O"));

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(8);
        packets.Should().OnlyContain(p => p.DateCreated <= filterDate);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDateCreatedStartsWith_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        var filter = new ColumnFilterDto(nameof(Packet.DateCreated), ColumnFilterOperator.StartsWith, DateTime.Now.Date.ToString("O"));

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"StartsWith operator is not supported for type {typeof(DateTime)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDateCreatedEndsWith_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        var filter = new ColumnFilterDto(nameof(Packet.DateCreated), ColumnFilterOperator.EndsWith, DateTime.Now.Date.ToString("O"));

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"EndsWith operator is not supported for type {typeof(DateTime)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDateCreatedEmpty_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        var filter = new ColumnFilterDto(nameof(Packet.DateCreated), ColumnFilterOperator.Empty, null);

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDateCreatedNotEmpty_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        var filter = new ColumnFilterDto(nameof(Packet.DateCreated), ColumnFilterOperator.NotEmpty, null);

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(10);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDateChangedEqual_ReturnsQuery()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;
        var startDateTime = DateTime.Now.AddMinutes(-10);
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, startDateTime, dateTimeNow);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-8)), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-5)), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-2)), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.DateChanged), ColumnFilterOperator.Equal, dateTimeNow.AddMinutes(-5).ToString("O"));

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().OnlyContain(p => p.DateChanged == dateTimeNow.AddMinutes(-5));
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDateChangedNotEqual_ReturnsQuery()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;
        var startDateTime = DateTime.Now.AddMinutes(-10);
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, startDateTime, dateTimeNow);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-8)), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-5)), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-2)), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.DateChanged), ColumnFilterOperator.NotEqual, dateTimeNow.AddMinutes(-5).ToString("O"));

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(9);
        packets.Should().OnlyContain(p => p.DateChanged != dateTimeNow.AddMinutes(-5));
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDateChangedContains_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        var filter = new ColumnFilterDto(nameof(Packet.DateChanged), ColumnFilterOperator.Contains, DateTime.Now.AddMinutes(-5).ToString("O"));

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"Contains operator is not supported for type {typeof(DateTime?)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDateChangedNotContains_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.DateChanged), ColumnFilterOperator.NotContains, DateTime.Now.AddMinutes(-5).ToString("O"));

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"NotContains operator is not supported for type {typeof(DateTime?)}");
        
        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDateChangedGreaterThan_ReturnsQuery()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;
        var startDateTime = DateTime.Now.AddMinutes(-10);

        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, startDateTime, dateTimeNow);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-8)), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-5)), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-2)), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.DateChanged), ColumnFilterOperator.GreaterThan, dateTimeNow.AddMinutes(-5).ToString("O"));

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(1);
        packets.Should().OnlyContain(p => p.DateChanged > dateTimeNow.AddMinutes(-5));
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDateChangedGreaterThanOrEqual_ReturnsQuery()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;
        var startDateTime = DateTime.Now.AddMinutes(-10);

        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, startDateTime, dateTimeNow);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-8)), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-5)), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-2)), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.DateChanged), ColumnFilterOperator.GreaterThanOrEqual, dateTimeNow.AddMinutes(-5).ToString("O"));

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => p.DateChanged >= dateTimeNow.AddMinutes(-5));
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDateChangedLessThan_ReturnsQuery()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;
        var startDateTime = DateTime.Now.AddMinutes(-10);

        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, startDateTime, dateTimeNow);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-8)), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-5)), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-2)), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.DateChanged), ColumnFilterOperator.LessThan, dateTimeNow.AddMinutes(-5).ToString("O"));

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(1);
        packets.Should().OnlyContain(p => p.DateChanged < dateTimeNow.AddMinutes(-5));
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDateChangedLessThanOrEqual_ReturnsQuery()
    {
        // Arrange
        var dateTimeNow = DateTime.Now;
        var startDateTime = DateTime.Now.AddMinutes(-10);
        
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, startDateTime, dateTimeNow);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-8)), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-5)), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-2)), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.DateChanged), ColumnFilterOperator.LessThanOrEqual, dateTimeNow.AddMinutes(-5).ToString("O"));
        
        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => p.DateChanged <= dateTimeNow.AddMinutes(-5));
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDateChangedStartsWith_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.DateChanged), ColumnFilterOperator.StartsWith, DateTime.Now.Date.ToString("O"));

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"StartsWith operator is not supported for type {typeof(DateTime?)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDateChangedEndsWith_ThrowsException()
    {
        // Arrange
        var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.DateChanged), ColumnFilterOperator.EndsWith, DateTime.Now.Date.ToString("O"));

        // Act
        var act = () => context.Packet.ApplyColumnFilter(filter);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"EndsWith operator is not supported for type {typeof(DateTime?)}");

        // Cleanup
        await context.DisposeAsync();
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDateChangedEmpty_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, DateTime.Now.AddMinutes(-8)), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, DateTime.Now.AddMinutes(-5)), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, DateTime.Now.AddMinutes(-2)), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.DateChanged), ColumnFilterOperator.Empty, null);

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        packets.Should().HaveCount(7);
        packets.Should().OnlyContain(p => p.DateChanged == null);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnDateChangedNotEmpty_ReturnsQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, DateTime.Now.AddMinutes(-8)), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, DateTime.Now.AddMinutes(-5)), cancellationToken: TestContext.Current.CancellationToken);
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, DateTime.Now.AddMinutes(-2)), cancellationToken: TestContext.Current.CancellationToken);

        var filter = new ColumnFilterDto(nameof(Packet.DateChanged), ColumnFilterOperator.NotEmpty, null);

        // Act
        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        packets.Should().HaveCount(3);
        packets.Should().OnlyContain(p => p.DateChanged != null);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnData_ReturnsSameQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        var oldQuery = context.Packet;
        
        var filter = new ColumnFilterDto(nameof(PacketDto.Data), ColumnFilterOperator.Contains, "SomeData");

        // Act
        var query = context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        query.AsEnumerable().Should().BeSameAs(oldQuery);
    }

    [Fact]
    public async Task ApplyColumnFilter_OnInvalidColumn_ReturnsSameQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        var oldQuery = context.Packet;
        
        var filter = new ColumnFilterDto("InvalidColumn", ColumnFilterOperator.Contains, "SomeData");

        // Act
        var query = context.Packet.ApplyColumnFilter(filter);
        
        // Assert
        query.AsEnumerable().Should().BeSameAs(oldQuery);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }
}
