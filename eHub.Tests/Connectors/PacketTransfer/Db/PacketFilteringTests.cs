using System.Collections.Immutable;
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
using MudBlazor.Extensions;



namespace eHub.Tests.Connectors.PacketTransfer.Db;

[TestClass]
public class PacketFilteringTests
{
    private IDbContextFactory<HubDbContext> _dbContextFactory = default!;
    private DbConnection _connection = default!;

    [TestInitialize]
    public async Task TestInitialize()
    {
        _connection = new SqliteConnection(DbHelper.InMemoryConnectionString);
        await _connection.OpenAsync();

        _dbContextFactory = DbHelper.CreateInMemoryFactory<HubDbContext>(_connection);

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
    }

    [TestMethod]
    public async Task ApplyFilterDto_WithDateTimeStart_ReturnsQuery()
    {
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, dateTimeNow.AddMinutes(-10), dateTimeNow);

        var filterStartDate = dateTimeNow.AddMinutes(-5);
        var expectedPackets = await context.Packet
            .Where(p => p.DateCreated >= filterStartDate)
            .ToArrayAsync();

        var filter = new PacketRequestDto(
            DateTimeStart: filterStartDate);

        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(expectedPackets.Length);
        packets.Should().BeEquivalentTo(expectedPackets);
    }

    [TestMethod]
    public async Task ApplyFilterDto_WithDateTimeEnd_ReturnsQuery()
    {
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, dateTimeNow.AddMinutes(-10), dateTimeNow);

        var filterEndDate = dateTimeNow.AddMinutes(-10);
        var expectedPackets = await context.Packet
            .Where(p => p.DateCreated <= filterEndDate)
            .ToArrayAsync();

        var filter = new PacketRequestDto(
            DateTimeEnd: filterEndDate);

        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(expectedPackets.Length);
        packets.Should().BeEquivalentTo(expectedPackets);
    }

    [TestMethod]
    public async Task ApplyFilterDto_WithDateTimeRange_ReturnsQuery()
    {
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, dateTimeNow.AddMinutes(-10), dateTimeNow.AddMinutes(10));

        var filterStartDate = dateTimeNow.AddMinutes(-5);
        var filterEndDate = dateTimeNow.AddMinutes(5);

        var expectedPackets = await context.Packet
            .Where(p => p.DateCreated >= filterStartDate && p.DateCreated <= filterEndDate)
            .ToArrayAsync();

        var filter = new PacketRequestDto(
            DateTimeStart: filterStartDate,
            DateTimeEnd: filterEndDate);

        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(expectedPackets.Length);
        packets.Should().BeEquivalentTo(expectedPackets);
    }

    [TestMethod]
    public async Task ApplyFilterDto_WithExactDateTime_ReturnsQuery()
    {
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(3, dateTimeNow.AddMinutes(-10), dateTimeNow.AddMinutes(10));

        var expectedPackets = await context.Packet
            .Where(p => p.DateCreated == dateTimeNow)
            .ToArrayAsync();

        var filter = new PacketRequestDto(
            DateTimeStart: dateTimeNow,
            DateTimeEnd: dateTimeNow);

        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(expectedPackets.Length);
        packets.Should().BeEquivalentTo(expectedPackets);
    }

    [TestMethod]
    public async Task ApplyFilterDto_WithInvalidDateTimeRange_ReturnsEmpty()
    {
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, dateTimeNow.AddMinutes(-10), dateTimeNow.AddMinutes(10));

        var filterStartDate = dateTimeNow.AddMinutes(5);
        var filterEndDate = dateTimeNow.AddMinutes(-5);
        var filter = new PacketRequestDto(
            DateTimeStart: filterStartDate,
            DateTimeEnd: filterEndDate);

        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ApplyFilterDto_WithMinId_ReturnsQuery()
    {
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, dateTimeNow.AddMinutes(-10), dateTimeNow);

        var filterMinId = 4;
        var expectedPackets = await context.Packet
            .Where(p => p.Id >= filterMinId)
            .ToArrayAsync();

        var filter = new PacketRequestDto(
            MinId: filterMinId);

        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(expectedPackets.Length);
        packets.Should().BeEquivalentTo(expectedPackets);
    }

    [TestMethod]
    public async Task ApplyFilterDto_WithMaxId_ReturnsQuery()
    {
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, dateTimeNow.AddMinutes(-10), dateTimeNow);

        var filterMaxId = 6;
        var expectedPackets = await context.Packet
            .Where(p => p.Id <= filterMaxId)
            .ToArrayAsync();

        var filter = new PacketRequestDto(
            MaxId: filterMaxId);

        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(expectedPackets.Length);
        packets.Should().BeEquivalentTo(expectedPackets);
    }

    [TestMethod]
    public async Task ApplyFilterDto_WithIdRange_ReturnsQuery()
    {
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, dateTimeNow.AddMinutes(-10), dateTimeNow);

        var filterMinId = 2;
        var filterMaxId = 8;
        var expectedPackets = await context.Packet
            .Where(p => p.Id >= filterMinId && p.Id <= filterMaxId)
            .ToArrayAsync();

        var filter = new PacketRequestDto(
            MinId: filterMinId,
            MaxId: filterMaxId);

        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(expectedPackets.Length);
        packets.Should().BeEquivalentTo(expectedPackets);
    }

    [TestMethod]
    public async Task ApplyFilterDto_WithExactId_ReturnsSingle()
    {
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, dateTimeNow.AddMinutes(-10), dateTimeNow);

        var filter = new PacketRequestDto(
            MinId: 5,
            MaxId: 5);

        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(1);
        packets.Single().Id.Should().Be(5);
    }

    [TestMethod]
    public async Task ApplyFilterDto_WithInvalidIdRange_ReturnsEmpty()
    {
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, dateTimeNow.AddMinutes(-10), dateTimeNow);

        var filterMinId = 8;
        var filterMaxId = 2;
        var filter = new PacketRequestDto(
            MinId: filterMinId,
            MaxId: filterMaxId);

        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ApplyFilterDto_WithChannels_ReturnsQuery()
    {
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(15, dateTimeNow.AddMinutes(-10), dateTimeNow, ["channel1", "channel2", "channel3"]);

        var filterChannels = new string[] { "channel1", "channel3" };
        var expectedPackets = await context.Packet
            .Where(p => filterChannels.Contains(p.Channel))
            .ToArrayAsync();

        var filter = new PacketRequestDto(
            Channels: [.. filterChannels]);

        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(expectedPackets.Length);
        packets.Should().BeEquivalentTo(expectedPackets);
    }

    [TestMethod]
    public async Task ApplyFilterDto_WithColumns_ReturnsQuery()
    {
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(15, dateTimeNow.AddMinutes(-10), dateTimeNow, ["channel1", "channel2", "channel3"]);

        var columnFilters = new ColumnFilterDto[]
        {
            new(nameof(Packet.Status), ColumnFilterOperator.Equal, PacketStatus.Processed.ToString()),
            new(nameof(Packet.Channel), ColumnFilterOperator.NotEqual, "channel1")
        };

        var expectedPackets = await context.Packet
            .Where(p => p.Status == PacketStatus.Processed && p.Channel != "channel1")
            .ToArrayAsync();

        var filter = new PacketRequestDto(
            ColumnFilters: [.. columnFilters]);

        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(expectedPackets.Length);
        packets.Should().BeEquivalentTo(expectedPackets);
    }

    [TestMethod]
    public async Task ApplyFilterDto_WhenAllFiltersApplied_ReturnsQuery()
    {
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(15, dateTimeNow.AddMinutes(-10), dateTimeNow.AddMinutes(10), ["channel1", "channel2", "channel3"]);

        var filterStartDate = dateTimeNow.AddMinutes(-5);
        var filterEndDate = dateTimeNow.AddMinutes(5);
        var filterMinId = 3;
        var filterMaxId = 13;
        var filterChannels = new string[] { "channel1", "channel2" };
        var columnFilters = new ColumnFilterDto[]
        {
            new(nameof(Packet.Status), ColumnFilterOperator.Equal, PacketStatus.Processed.ToString()),
        };

        var expectedPackets = await context.Packet
            .Where(p => p.DateCreated >= filterStartDate && p.DateCreated <= filterEndDate)
            .Where(p => p.Id >= filterMinId && p.Id <= filterMaxId)
            .Where(p => filterChannels.Contains(p.Channel))
            .Where(p => p.Status == PacketStatus.Processed)
            .ToArrayAsync();

        var filter = new PacketRequestDto(
            DateTimeStart: filterStartDate,
            DateTimeEnd: filterEndDate,
            MinId: filterMinId,
            MaxId: filterMaxId,
            Channels: [.. filterChannels],
            ColumnFilters: [.. columnFilters]);

        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(expectedPackets.Length);
        packets.Should().BeEquivalentTo(expectedPackets);
    }

    [TestMethod]
    public async Task ApplyFilterDto_NoMatches_ReturnsEmpty()
    {
        var dateTimeNow = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, dateTimeNow.AddMinutes(-10), dateTimeNow);

        var filter = new PacketRequestDto(
            MinId: 11);

        var query = context.Packet.ApplyFilterDto(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnIdEqual_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Id), ColumnFilterOperator.Equal, "7");

        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().ContainSingle(p => p.Id == 7);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnIdNotEqual_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Id), ColumnFilterOperator.NotEqual, "3");

        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(9);
        packets.Should().OnlyContain(p => p.Id != 3);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnIdContains_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Id), ColumnFilterOperator.Contains, "5");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"Contains operator is not supported for type {typeof(long)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnIdNotContains_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Id), ColumnFilterOperator.NotContains, "4");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"NotContains operator is not supported for type {typeof(long)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnIdGreaterThan_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Id), ColumnFilterOperator.GreaterThan, "4");

        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().OnlyContain(p => p.Id > 4);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnIdGreaterThanOrEqual_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Id), ColumnFilterOperator.GreaterThanOrEqual, "4");

        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().OnlyContain(p => p.Id >= 4);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnIdLessThan_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Id), ColumnFilterOperator.LessThan, "6");

        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().OnlyContain(p => p.Id < 6);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnIdLessThanOrEqual_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Id), ColumnFilterOperator.LessThanOrEqual, "6");

        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().OnlyContain(p => p.Id <= 6);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnIdStartsWith_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Id), ColumnFilterOperator.StartsWith, "3");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"StartsWith operator is not supported for type {typeof(long)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnIdEndsWith_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Id), ColumnFilterOperator.EndsWith, "3");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"EndsWith operator is not supported for type {typeof(long)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnIdEmpty_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Id), ColumnFilterOperator.Empty, null);

        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnIdNotEmpty_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Id), ColumnFilterOperator.NotEmpty, null);

        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(10);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnMetadataEqual_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        foreach (var packet in context.Packet)
        {
            packet.Metadata = $"Metadata{packet.Id}";
        }

        await context.SaveChangesAsync();

        var filter = new ColumnFilterDto(nameof(Packet.Metadata), ColumnFilterOperator.Equal, "Metadata3");

        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().ContainSingle();
        packets.Single().Metadata.Should().Be("Metadata3");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnMetadataNotEqual_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        foreach (var packet in context.Packet)
        {
            packet.Metadata = $"Metadata{packet.Id}";
        }

        await context.SaveChangesAsync();

        var filter = new ColumnFilterDto(nameof(Packet.Metadata), ColumnFilterOperator.NotEqual, "Metadata3");

        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(9);
        packets.Should().OnlyContain(p => p.Metadata != "Metadata3");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnMetadataContains_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "SomeMetadata"));
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "Metdata4"));
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "Met5data"));

        var filter = new ColumnFilterDto(nameof(Packet.Metadata), ColumnFilterOperator.Contains, "t5da");

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().ContainSingle();
        packets.Single().Metadata.Should().Be("Met5data");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnMetadataNotContains_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "SomeMetadata"));
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "Metdata4"));
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "Met5data"));

        var filter = new ColumnFilterDto(nameof(Packet.Metadata), ColumnFilterOperator.NotContains, "t5da");

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(9);
        packets.Should().OnlyContain(p => p.Metadata != "Met5data");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnMetadataGreaterThan_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Metadata), ColumnFilterOperator.GreaterThan, "Metadata");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"GreaterThan operator is not supported for type {typeof(string)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnMetadataGreaterThanOrEqual_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Metadata), ColumnFilterOperator.GreaterThanOrEqual, "Metadata");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"GreaterThanOrEqual operator is not supported for type {typeof(string)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnMetadataLessThan_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Metadata), ColumnFilterOperator.LessThan, "Metadata");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"LessThan operator is not supported for type {typeof(string)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnMetadataLessThanOrEqual_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Metadata), ColumnFilterOperator.LessThanOrEqual, "Metadata");

        var act = () => context.Packet.AsNoTracking().ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"LessThanOrEqual operator is not supported for type {typeof(string)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnMetadataStartsWith_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "SomeMetadata"));
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "Metadata4"));
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "Met5data"));
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "Metadata6"));

        var filter = new ColumnFilterDto(nameof(Packet.Metadata), ColumnFilterOperator.StartsWith, "Metadata");

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => p.Metadata != null && p.Metadata.StartsWith("Metadata"));
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnMetadataEndsWith_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "SomeMetadata"));
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "4Metadata"));
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "Met5data"));
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "Metadata6"));

        var filter = new ColumnFilterDto(nameof(Packet.Metadata), ColumnFilterOperator.EndsWith, "Metadata");

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => p.Metadata != null && p.Metadata.EndsWith("Metadata"));
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnMetadataEmpty_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "SomeMetadata"));
        await context.Packet.Where(p => p.Id == 2).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, ""));
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "Metdata4"));
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "Met5data"));

        var filter = new ColumnFilterDto(nameof(Packet.Metadata), ColumnFilterOperator.Empty, null);

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(7);
        packets.Should().OnlyContain(p => string.IsNullOrEmpty(p.Metadata));
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnMetadataNotEmpty_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "SomeMetadata"));
        await context.Packet.Where(p => p.Id == 2).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, ""));
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "Metdata4"));
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Metadata, "Met5data"));

        var filter = new ColumnFilterDto(nameof(Packet.Metadata), ColumnFilterOperator.NotEmpty, null);

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(3);
        packets.Should().OnlyContain(p => !string.IsNullOrEmpty(p.Metadata));
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnStatusEqual_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Status), ColumnFilterOperator.Equal, PacketStatus.Processed.ToString());

        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        var expectedPackets = await context.Packet.Where(p => p.Status == PacketStatus.Processed).ToArrayAsync();

        packets.Should().BeEquivalentTo(expectedPackets);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnStatusNotEqual_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Status), ColumnFilterOperator.NotEqual, PacketStatus.Processed.ToString());

        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        var expectedPackets = await context.Packet.Where(p => p.Status != PacketStatus.Processed).ToArrayAsync();

        packets.Should().BeEquivalentTo(expectedPackets);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnStatusContains_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Status), ColumnFilterOperator.Contains, "queue");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"Contains operator is not supported for type {typeof(PacketStatus)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnStatusNotContains_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Status), ColumnFilterOperator.NotContains, "queue");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"NotContains operator is not supported for type {typeof(PacketStatus)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnStatusGreaterThan_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Status), ColumnFilterOperator.GreaterThan, "2");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"GreaterThan operator is not supported for type {typeof(PacketStatus)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnStatusGreaterThanOrEqual_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Status), ColumnFilterOperator.GreaterThanOrEqual, "2");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"GreaterThanOrEqual operator is not supported for type {typeof(PacketStatus)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnStatusLessThan_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Status), ColumnFilterOperator.LessThan, "4");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"LessThan operator is not supported for type {typeof(PacketStatus)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnStatusLessThanOrEqual_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Status), ColumnFilterOperator.LessThanOrEqual, "4");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"LessThanOrEqual operator is not supported for type {typeof(PacketStatus)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnStatusStartsWith_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Status), ColumnFilterOperator.StartsWith, "Pro");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"StartsWith operator is not supported for type {typeof(PacketStatus)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnStatusEndsWith_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Status), ColumnFilterOperator.EndsWith, "rror");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"EndsWith operator is not supported for type {typeof(PacketStatus)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnStatusEmpty_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Status), ColumnFilterOperator.Empty, null);

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"Empty operator is not supported for type {typeof(PacketStatus)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnStatusNotEmpty_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Status), ColumnFilterOperator.NotEmpty, null);

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"NotEmpty operator is not supported for type {typeof(PacketStatus)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnChannelEqual_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        foreach (var packet in context.Packet)
        {
            packet.Channel = $"Channel{packet.Id}";
        }

        await context.SaveChangesAsync();

        var filter = new ColumnFilterDto(nameof(Packet.Channel), ColumnFilterOperator.Equal, "Channel3");

        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().ContainSingle();
        packets.Single().Channel.Should().Be("Channel3");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnChannelNotEqual_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        foreach (var packet in context.Packet)
        {
            packet.Channel = $"Channel{packet.Id}";
        }

        await context.SaveChangesAsync();

        var filter = new ColumnFilterDto(nameof(Packet.Channel), ColumnFilterOperator.NotEqual, "Channel3");

        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(9);
        packets.Should().OnlyContain(p => p.Channel != "Channel3");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnChannelContains_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "SomeChannel"));
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "Channel4"));
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "Ch5nnel"));

        var filter = new ColumnFilterDto(nameof(Packet.Channel), ColumnFilterOperator.Contains, "h5nn");

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().ContainSingle();
        packets.Single().Channel.Should().Be("Ch5nnel");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnChannelNotContains_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "SomeChannel"));
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "Channel4"));
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "Ch5nnel"));

        var filter = new ColumnFilterDto(nameof(Packet.Channel), ColumnFilterOperator.NotContains, "h5nn");

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(9);
        packets.Should().OnlyContain(p => p.Channel != "Ch5nnel");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnChannelGreaterThan_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Channel), ColumnFilterOperator.GreaterThan, "Channel");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"GreaterThan operator is not supported for type {typeof(string)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnChannelGreaterThanOrEqual_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Channel), ColumnFilterOperator.GreaterThanOrEqual, "Channel");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"GreaterThanOrEqual operator is not supported for type {typeof(string)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnChannelLessThan_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Channel), ColumnFilterOperator.LessThan, "Channel");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"LessThan operator is not supported for type {typeof(string)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnChannelLessThanOrEqual_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.Channel), ColumnFilterOperator.LessThanOrEqual, "Channel");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"LessThanOrEqual operator is not supported for type {typeof(string)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnChannelStartsWith_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "SomeChannel"));
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "Channel4"));
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "Ch5nnel"));
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "Channel6"));

        var filter = new ColumnFilterDto(nameof(Packet.Channel), ColumnFilterOperator.StartsWith, "Channel");

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => p.Channel.StartsWith("Channel"));
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnChannelEndsWith_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now, ["ChannelInvalid"]);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "SomeChannel"));
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "4Channel"));
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "Ch5nnel"));
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "Channel6"));

        var filter = new ColumnFilterDto(nameof(Packet.Channel), ColumnFilterOperator.EndsWith, "Channel");

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => p.Channel.EndsWith("Channel"));
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnChannelEmpty_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(5, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "SomeChannel"));
        await context.Packet.Where(p => p.Id == 2).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, ""));
        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, ""));
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "Channel4"));
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "Ch5nnel"));

        var filter = new ColumnFilterDto(nameof(Packet.Channel), ColumnFilterOperator.Empty, null);

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => string.IsNullOrEmpty(p.Channel));
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnChannelNotEmpty_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(5, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "SomeChannel"));
        await context.Packet.Where(p => p.Id == 2).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, ""));
        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, ""));
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "Channel4"));
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Channel, "Ch5nnel"));

        var filter = new ColumnFilterDto(nameof(Packet.Channel), ColumnFilterOperator.NotEmpty, null);

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(3);
        packets.Should().OnlyContain(p => !string.IsNullOrEmpty(p.Channel));
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDynamicFieldEqual_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        foreach (var packet in context.Packet)
        {
            packet.DynamicField = $"DynamicField{packet.Id}";
        }

        await context.SaveChangesAsync();

        var filter = new ColumnFilterDto(nameof(Packet.DynamicField), ColumnFilterOperator.Equal, "DynamicField3");

        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().ContainSingle();
        packets.Single().DynamicField.Should().Be("DynamicField3");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDynamicFieldNotEqual_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);
        foreach (var packet in context.Packet)
        {
            packet.DynamicField = $"DynamicField{packet.Id}";
        }

        await context.SaveChangesAsync();

        var filter = new ColumnFilterDto(nameof(Packet.DynamicField), ColumnFilterOperator.NotEqual, "DynamicField3");

        var query = context.Packet.ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(9);
        packets.Should().OnlyContain(p => p.DynamicField != "DynamicField3");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDynamicFieldContains_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "SomeData"));
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "DynamicField4"));
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "Dyn5micField"));

        var filter = new ColumnFilterDto(nameof(Packet.DynamicField), ColumnFilterOperator.Contains, "n5mic");

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().ContainSingle();
        packets.Single().DynamicField.Should().Be("Dyn5micField");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDynamicFieldNotContains_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "SomeData"));
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "DynamicField4"));
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "Dyn5micField"));

        var filter = new ColumnFilterDto(nameof(Packet.DynamicField), ColumnFilterOperator.NotContains, "n5mic");

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(9);
        packets.Should().OnlyContain(p => p.DynamicField != "Dyn5micField");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDynamicFieldGreaterThan_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.DynamicField), ColumnFilterOperator.GreaterThan, "DynamicField");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"GreaterThan operator is not supported for type {typeof(string)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDynamicFieldGreaterThanOrEqual_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.DynamicField), ColumnFilterOperator.GreaterThanOrEqual, "DynamicField");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"GreaterThanOrEqual operator is not supported for type {typeof(string)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDynamicFieldLessThan_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.DynamicField), ColumnFilterOperator.LessThan, "DynamicField");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"LessThan operator is not supported for type {typeof(string)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDynamicFieldLessThanOrEqual_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.DynamicField), ColumnFilterOperator.LessThanOrEqual, "DynamicField");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"LessThanOrEqual operator is not supported for type {typeof(string)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDynamicFieldStartsWith_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "SomeDynamicField"));
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "DynamicField4"));
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "Dyn5micField"));
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "DynamicField6"));

        var filter = new ColumnFilterDto(nameof(Packet.DynamicField), ColumnFilterOperator.StartsWith, "DynamicField");

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => p.DynamicField != null && p.DynamicField.StartsWith("DynamicField"));
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDynamicFieldEndsWith_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "SomeDynamicField"));
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "4DynamicField"));
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "Dyn5micField"));
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "DynamicField6"));

        var filter = new ColumnFilterDto(nameof(Packet.DynamicField), ColumnFilterOperator.EndsWith, "DynamicField");

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => p.DynamicField != null && p.DynamicField.EndsWith("DynamicField"));
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDynamicFieldEmpty_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "SomeDynamicField"));
        await context.Packet.Where(p => p.Id == 2).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, ""));
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "DynamicField4"));
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "Dyn5micField"));

        var filter = new ColumnFilterDto(nameof(Packet.DynamicField), ColumnFilterOperator.Empty, null);

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(7);
        packets.Should().OnlyContain(p => string.IsNullOrEmpty(p.DynamicField));
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDynamicFieldNotEmpty_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "SomeDynamicField"));
        await context.Packet.Where(p => p.Id == 2).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, ""));
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "DynamicField4"));
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DynamicField, "Dyn5micField"));

        var filter = new ColumnFilterDto(nameof(Packet.DynamicField), ColumnFilterOperator.NotEmpty, null);

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(3);
        packets.Should().OnlyContain(p => !string.IsNullOrEmpty(p.DynamicField));
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnParentIdEqual_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 2));
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 5));
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 100));

        var filter = new ColumnFilterDto(nameof(Packet.ParentId), ColumnFilterOperator.Equal, "5");

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().OnlyContain(p => p.ParentId == 5);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnParentIdNotEqual_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 2));
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 5));
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 100));

        var filter = new ColumnFilterDto(nameof(Packet.ParentId), ColumnFilterOperator.NotEqual, "2");

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(9);
        packets.Should().OnlyContain(p => p.ParentId != 2);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnParentIdContains_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.ParentId), ColumnFilterOperator.Contains, "5");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"Contains operator is not supported for type {typeof(long?)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnParentIdNotContains_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.ParentId), ColumnFilterOperator.NotContains, "4");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"NotContains operator is not supported for type {typeof(long?)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnParentIdGreaterThan_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 2));
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 5));
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 100));

        var filter = new ColumnFilterDto(nameof(Packet.ParentId), ColumnFilterOperator.GreaterThan, "4");

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => p.ParentId > 4);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnParentIdGreaterThanOrEqual_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 2));
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 5));
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 100));

        var filter = new ColumnFilterDto(nameof(Packet.ParentId), ColumnFilterOperator.GreaterThanOrEqual, "5");

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => p.ParentId >= 5);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnParentIdLessThan_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 2));
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 5));
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 100));

        var filter = new ColumnFilterDto(nameof(Packet.ParentId), ColumnFilterOperator.LessThan, "6");

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => p.ParentId < 6);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnParentIdLessThanOrEqual_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 2));
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 5));
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 100));

        var filter = new ColumnFilterDto(nameof(Packet.ParentId), ColumnFilterOperator.LessThanOrEqual, "2");

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(1);
        packets.Should().OnlyContain(p => p.ParentId <= 2);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnParentIdStartsWith_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.ParentId), ColumnFilterOperator.StartsWith, "3");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"StartsWith operator is not supported for type {typeof(long?)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnParentIdEndsWith_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.ParentId), ColumnFilterOperator.EndsWith, "3");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"EndsWith operator is not supported for type {typeof(long?)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnParentIdEmpty_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 2));
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 5));
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 100));

        var filter = new ColumnFilterDto(nameof(Packet.ParentId), ColumnFilterOperator.Empty, null);

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(7);
        packets.Should().OnlyContain(p => p.ParentId == null);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnParentIdNotEmpty_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 2));
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 5));
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ParentId, 100));

        var filter = new ColumnFilterDto(nameof(Packet.ParentId), ColumnFilterOperator.NotEmpty, null);

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(3);
        packets.Should().OnlyContain(p => p.ParentId != null);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnRetryCountEqual_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 2));
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 5));
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 100));

        var filter = new ColumnFilterDto(nameof(Packet.RetryCount), ColumnFilterOperator.Equal, "5");

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().OnlyContain(p => p.RetryCount == 5);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnRetryCountNotEqual_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 2));
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 5));
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 100));

        var filter = new ColumnFilterDto(nameof(Packet.RetryCount), ColumnFilterOperator.NotEqual, "2");

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(9);
        packets.Should().OnlyContain(p => p.RetryCount != 2);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnRetryCountContains_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.RetryCount), ColumnFilterOperator.Contains, "5");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"Contains operator is not supported for type {typeof(int)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnRetryCountNotContains_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.RetryCount), ColumnFilterOperator.NotContains, "4");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"NotContains operator is not supported for type {typeof(int)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnRetryCountGreaterThan_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 2));
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 5));
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 100));

        var filter = new ColumnFilterDto(nameof(Packet.RetryCount), ColumnFilterOperator.GreaterThan, "4");

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => p.RetryCount > 4);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnRetryCountGreaterThanOrEqual_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 2));
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 5));
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 100));

        var filter = new ColumnFilterDto(nameof(Packet.RetryCount), ColumnFilterOperator.GreaterThanOrEqual, "5");

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => p.RetryCount >= 5);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnRetryCountLessThan_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 2));
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 5));
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 100));

        var filter = new ColumnFilterDto(nameof(Packet.RetryCount), ColumnFilterOperator.LessThan, "5");

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(8);
        packets.Should().OnlyContain(p => p.RetryCount < 5);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnRetryCountLessThanOrEqual_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 2));
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 5));
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 100));

        var filter = new ColumnFilterDto(nameof(Packet.RetryCount), ColumnFilterOperator.LessThanOrEqual, "2");

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(8);
        packets.Should().OnlyContain(p => p.RetryCount <= 2);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnRetryCountStartsWith_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.RetryCount), ColumnFilterOperator.StartsWith, "3");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"StartsWith operator is not supported for type {typeof(int)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnRetryCountEndsWith_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.RetryCount), ColumnFilterOperator.EndsWith, "3");

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"EndsWith operator is not supported for type {typeof(int)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnRetryCountEmpty_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 2));
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 5));
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 100));

        var filter = new ColumnFilterDto(nameof(Packet.RetryCount), ColumnFilterOperator.Empty, null);

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(7);
        packets.Should().OnlyContain(p => p.RetryCount == 0);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnRetryCountNotEmpty_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 3).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 2));
        await context.Packet.Where(p => p.Id == 6).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 5));
        await context.Packet.Where(p => p.Id == 9).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RetryCount, 100));

        var filter = new ColumnFilterDto(nameof(Packet.RetryCount), ColumnFilterOperator.NotEmpty, null);

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(3);
        packets.Should().OnlyContain(p => p.RetryCount != 0);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDateCreatedEqual_ReturnsQuery()
    {
        var dateTimeNow = DateTime.Now;
        var startDateTime = DateTime.Now.AddMinutes(-10);

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, startDateTime, dateTimeNow);

        var filter = new ColumnFilterDto(nameof(Packet.DateCreated), ColumnFilterOperator.Equal, startDateTime.ToString("O"));

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().OnlyContain(p => p.DateCreated == startDateTime);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDateCreatedNotEqual_ReturnsQuery()
    {
        var dateTimeNow = DateTime.Now;
        var startDateTime = DateTime.Now.AddMinutes(-10);

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, startDateTime, dateTimeNow);

        var filter = new ColumnFilterDto(nameof(Packet.DateCreated), ColumnFilterOperator.NotEqual, startDateTime.ToString("O"));

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(9);
        packets.Should().OnlyContain(p => p.DateCreated != startDateTime);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDateCreatedContains_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.DateCreated), ColumnFilterOperator.Contains, DateTime.Now.AddMinutes(-5).ToString("O"));

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"Contains operator is not supported for type {typeof(DateTime)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDateCreatedNotContains_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.DateCreated), ColumnFilterOperator.NotContains, DateTime.Now.AddMinutes(-5).ToString("O"));

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"NotContains operator is not supported for type {typeof(DateTime)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDateCreatedGreaterThan_ReturnsQuery()
    {
        var startDate = DateTime.Now.AddMinutes(-10);
        var endDate = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, startDate, endDate);

        var totalDuration = endDate - startDate;
        var interval = TimeSpan.FromTicks(totalDuration.Ticks / 9);
        var filterDate = startDate.Add(interval * 2);

        var filter = new ColumnFilterDto(nameof(Packet.DateCreated), ColumnFilterOperator.GreaterThan, filterDate.ToString("O"));

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(7);
        packets.Should().OnlyContain(p => p.DateCreated > filterDate);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDateCreatedGreaterThanOrEqual_ReturnsQuery()
    {
        var startDate = DateTime.Now.AddMinutes(-10);
        var endDate = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, startDate, endDate);

        var totalDuration = endDate - startDate;
        var interval = TimeSpan.FromTicks(totalDuration.Ticks / 9);
        var filterDate = startDate.Add(interval * 2);

        var filter = new ColumnFilterDto(nameof(Packet.DateCreated), ColumnFilterOperator.GreaterThanOrEqual, filterDate.ToString("O"));

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(8);
        packets.Should().OnlyContain(p => p.DateCreated >= filterDate);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDateCreatedLessThan_ReturnsQuery()
    {
        var startDate = DateTime.Now.AddMinutes(-10);
        var endDate = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, startDate, endDate);

        var totalDuration = endDate - startDate;
        var interval = TimeSpan.FromTicks(totalDuration.Ticks / 9);
        var filterDate = startDate.Add(interval * 7);

        var filter = new ColumnFilterDto(nameof(Packet.DateCreated), ColumnFilterOperator.LessThan, filterDate.ToString("O"));

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(7);
        packets.Should().OnlyContain(p => p.DateCreated < filterDate);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDateCreatedLessThanOrEqual_ReturnsQuery()
    {
        var startDate = DateTime.Now.AddMinutes(-10);
        var endDate = DateTime.Now;

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, startDate, endDate);

        var totalDuration = endDate - startDate;
        var interval = TimeSpan.FromTicks(totalDuration.Ticks / 9);
        var filterDate = startDate.Add(interval * 7);

        var filter = new ColumnFilterDto(nameof(Packet.DateCreated), ColumnFilterOperator.LessThanOrEqual, filterDate.ToString("O"));

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(8);
        packets.Should().OnlyContain(p => p.DateCreated <= filterDate);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDateCreatedStartsWith_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.DateCreated), ColumnFilterOperator.StartsWith, DateTime.Now.Date.ToString("O"));

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"StartsWith operator is not supported for type {typeof(DateTime)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDateCreatedEndsWith_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.DateCreated), ColumnFilterOperator.EndsWith, DateTime.Now.Date.ToString("O"));

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"EndsWith operator is not supported for type {typeof(DateTime)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDateCreatedEmpty_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.DateCreated), ColumnFilterOperator.Empty, null);

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDateCreatedNotEmpty_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.DateCreated), ColumnFilterOperator.NotEmpty, null);

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(10);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDateChangedEqual_ReturnsQuery()
    {
        var dateTimeNow = DateTime.Now;
        var startDateTime = DateTime.Now.AddMinutes(-10);

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, startDateTime, dateTimeNow);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-8)));
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-5)));
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-2)));

        var filter = new ColumnFilterDto(nameof(Packet.DateChanged), ColumnFilterOperator.Equal, dateTimeNow.AddMinutes(-5).ToString("O"));

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().OnlyContain(p => p.DateChanged == dateTimeNow.AddMinutes(-5));
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDateChangedNotEqual_ReturnsQuery()
    {
        var dateTimeNow = DateTime.Now;
        var startDateTime = DateTime.Now.AddMinutes(-10);

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, startDateTime, dateTimeNow);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-8)));
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-5)));
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-2)));

        var filter = new ColumnFilterDto(nameof(Packet.DateChanged), ColumnFilterOperator.NotEqual, dateTimeNow.AddMinutes(-5).ToString("O"));

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(9);
        packets.Should().OnlyContain(p => p.DateChanged != dateTimeNow.AddMinutes(-5));
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDateChangedContains_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.DateChanged), ColumnFilterOperator.Contains, DateTime.Now.AddMinutes(-5).ToString("O"));

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"Contains operator is not supported for type {typeof(DateTime?)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDateChangedNotContains_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.DateChanged), ColumnFilterOperator.NotContains, DateTime.Now.AddMinutes(-5).ToString("O"));

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"NotContains operator is not supported for type {typeof(DateTime?)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDateChangedGreaterThan_ReturnsQuery()
    {
        var dateTimeNow = DateTime.Now;
        var startDateTime = DateTime.Now.AddMinutes(-10);

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, startDateTime, dateTimeNow);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-8)));
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-5)));
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-2)));

        var filter = new ColumnFilterDto(nameof(Packet.DateChanged), ColumnFilterOperator.GreaterThan, dateTimeNow.AddMinutes(-5).ToString("O"));

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(1);
        packets.Should().OnlyContain(p => p.DateChanged > dateTimeNow.AddMinutes(-5));
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDateChangedGreaterThanOrEqual_ReturnsQuery()
    {
        var dateTimeNow = DateTime.Now;
        var startDateTime = DateTime.Now.AddMinutes(-10);

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, startDateTime, dateTimeNow);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-8)));
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-5)));
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-2)));

        var filter = new ColumnFilterDto(nameof(Packet.DateChanged), ColumnFilterOperator.GreaterThanOrEqual, dateTimeNow.AddMinutes(-5).ToString("O"));

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => p.DateChanged >= dateTimeNow.AddMinutes(-5));
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDateChangedLessThan_ReturnsQuery()
    {
        var dateTimeNow = DateTime.Now;
        var startDateTime = DateTime.Now.AddMinutes(-10);

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, startDateTime, dateTimeNow);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-8)));
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-5)));
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-2)));

        var filter = new ColumnFilterDto(nameof(Packet.DateChanged), ColumnFilterOperator.LessThan, dateTimeNow.AddMinutes(-5).ToString("O"));

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(1);
        packets.Should().OnlyContain(p => p.DateChanged < dateTimeNow.AddMinutes(-5));
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDateChangedLessThanOrEqual_ReturnsQuery()
    {
        var dateTimeNow = DateTime.Now;
        var startDateTime = DateTime.Now.AddMinutes(-10);

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, startDateTime, dateTimeNow);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-8)));
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-5)));
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, dateTimeNow.AddMinutes(-2)));

        var filter = new ColumnFilterDto(nameof(Packet.DateChanged), ColumnFilterOperator.LessThanOrEqual, dateTimeNow.AddMinutes(-5).ToString("O"));

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => p.DateChanged <= dateTimeNow.AddMinutes(-5));
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDateChangedStartsWith_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.DateChanged), ColumnFilterOperator.StartsWith, DateTime.Now.Date.ToString("O"));

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"StartsWith operator is not supported for type {typeof(DateTime?)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDateChangedEndsWith_ThrowsException()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        var filter = new ColumnFilterDto(nameof(Packet.DateChanged), ColumnFilterOperator.EndsWith, DateTime.Now.Date.ToString("O"));

        var act = () => context.Packet.ApplyColumnFilter(filter);

        act.Should().Throw<InvalidOperationException>().WithMessage($"EndsWith operator is not supported for type {typeof(DateTime?)}");
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDateChangedEmpty_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, DateTime.Now.AddMinutes(-8)));
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, DateTime.Now.AddMinutes(-5)));
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, DateTime.Now.AddMinutes(-2)));

        var filter = new ColumnFilterDto(nameof(Packet.DateChanged), ColumnFilterOperator.Empty, null);

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(7);
        packets.Should().OnlyContain(p => p.DateChanged == null);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnDateChangedNotEmpty_ReturnsQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.AddTestPacketsAsync(10, DateTime.Now.AddMinutes(-10), DateTime.Now);

        await context.Packet.Where(p => p.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, DateTime.Now.AddMinutes(-8)));
        await context.Packet.Where(p => p.Id == 4).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, DateTime.Now.AddMinutes(-5)));
        await context.Packet.Where(p => p.Id == 5).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.DateChanged, DateTime.Now.AddMinutes(-2)));

        var filter = new ColumnFilterDto(nameof(Packet.DateChanged), ColumnFilterOperator.NotEmpty, null);

        var query = context.Packet.AsNoTracking().ApplyColumnFilter(filter);
        var packets = await query.ToArrayAsync();

        packets.Should().HaveCount(3);
        packets.Should().OnlyContain(p => p.DateChanged != null);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnData_ReturnsSameQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var oldQuery = context.Packet;

        var filter = new ColumnFilterDto(nameof(PacketDto.Data), ColumnFilterOperator.Contains, "SomeData");

        var query = context.Packet.ApplyColumnFilter(filter);

        query.Should().BeSameAs(oldQuery);
    }

    [TestMethod]
    public async Task ApplyColumnFilter_OnInvalidColumn_ReturnsSameQuery()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var oldQuery = context.Packet;

        var filter = new ColumnFilterDto("InvalidColumn", ColumnFilterOperator.Contains, "SomeData");

        var query = context.Packet.ApplyColumnFilter(filter);

        query.Should().BeSameAs(oldQuery);
    }

    [TestCleanup]
    public async Task TestCleanup()
    {
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }
}
