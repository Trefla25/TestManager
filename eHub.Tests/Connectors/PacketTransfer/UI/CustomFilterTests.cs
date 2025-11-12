using eHub.Config;
using eHub.Contracts;
using eHub.Database.Models;
using eHub.Database;
using eHub.PlugIn;
using eHub.Scripting.Connectors.Services;
using eHub.Scripting.Connectors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Linq.Expressions;
using FluentAssertions;
using System.Text;
using eHub.PlugIn.UI;
using eHub.Tests.Helper;

namespace eHub.Tests.Connectors.PacketTransfer.UI;

public class CustomFilterTests : IAsyncLifetime
{
    private const string TestChannel = "TestChannel";

    private PacketDtoService _packetDtoService = null!;
    private ConnectorMetadata _metadata = null!;
    private ConnectorTemplate _connectorTemplate = null!;
    private ILoggerFactory _loggerFactory = null!;
    private IPacketFilterConfigurator _packetTransfer = null!;
    private IPacketConverter _packetConverter = null!;
    private IDbContextFactory<HubDbContext> _dbContextFactory = null!;
    private ILogger<PacketDtoService> _logger = null!;
    private SqliteConnection _connection = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection(DbHelper.InMemoryConnectionString);
        await _connection.OpenAsync();

        _metadata = new ConnectorMetadata(new(), "MyTestConnector", "TestConnector");
        _connectorTemplate = new ConnectorTemplate();

        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = _loggerFactory.CreateLogger<PacketDtoService>();
        _packetTransfer = Substitute.For<IPacketFilterConfigurator>();
        _packetConverter = Substitute.For<IPacketConverter>();
        _packetTransfer.Converter.Returns(_packetConverter);
        _dbContextFactory = DbHelper.CreateInMemoryFactory<HubDbContext>(_connection);

        // Make sure the database is created
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
    }

    [Fact]
    public void GetUICustomFilters_WithNoCustomFilters_ReturnsEmpty()
    {
        // Arrange
        _packetTransfer.ConfigureCustomFilters(Arg.Do<ICustomFilterBuilder>(_ => { }));
        _packetDtoService = new PacketDtoService(_logger, _packetTransfer, _metadata, _connectorTemplate);
        
        // Act
        var filters = _packetDtoService.GetUICustomFilters();
        
        // Assert
        filters.Should().BeEmpty();
    }

    [Fact]
    public void GetUICustomFilters_WithCustomFilters_ReturnsCustomFilters()
    {
        // Arrange
        _packetTransfer.ConfigureCustomFilters(Arg.Do<ICustomFilterBuilder>(builder =>
        {
            builder.AddCustomFilter<string>("MyStringFilter", "SUBSTRING(BinaryData, 1, 4)");
            builder.AddCustomFilter<int>("MyIntFilter", "CAST(SUBSTRING(BinaryData, 5, 3) AS INT)");
            builder.AddCustomFilter<DateTime>("MyDateTimeFilter", "CAST(SUBSTRING(BinaryData, 9, 10) AS DATETIME)");
        }));
        
        _packetDtoService = new PacketDtoService(_logger, _packetTransfer, _metadata, _connectorTemplate);

        // Act
        var filters = _packetDtoService.GetUICustomFilters();
        
        // Assert
        filters.Should().HaveCount(3);
        filters.Should().ContainKey("MyStringFilter");
        filters.Should().ContainKey("MyIntFilter");
        filters.Should().ContainKey("MyDateTimeFilter");

        filters["MyStringFilter"].Should().Be(typeof(string).ToString());
        filters["MyIntFilter"].Should().Be(typeof(int).ToString());
        filters["MyDateTimeFilter"].Should().Be(typeof(DateTime).ToString());
    }

    [Fact]
    public async Task CreateFilteredQuery_NoCustomFilters_DoesNotAddFromSqlQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);

        var columnFilter = new ColumnFilterDto(nameof(Packet.Status), ColumnFilterOperator.Equal, PacketStatus.Enqueued.ToString());
        var filter = new PacketRequestDto
        (
            DateTimeStart: DateTime.Now.AddMinutes(-10),
            DateTimeEnd: DateTime.Now,
            ColumnFilters: [columnFilter]
        );

        _packetDtoService = new PacketDtoService(_logger, _packetTransfer, _metadata, _connectorTemplate);
        
        // Act
        var query = _packetDtoService.CreateFilteredQuery(context, filter);
        var expression = (MethodCallExpression)query.Expression;

        // Assert
        #pragma warning disable EF1001 // Internal EF Core API usage.
        expression.Arguments.Should().NotContainItemsAssignableTo<FromSqlQueryRootExpression>();
        #pragma warning restore EF1001 // Internal EF Core API usage.
    }

    [Fact]
    public async Task CreateFilteredQuery_WithBinaryDataFilter_AppliesSqlQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.Packet.AddRangeAsync([
            new() { BinaryData = Encoding.UTF8.GetBytes("SomeData"), Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("MyDataBinary"), Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("NoMatch"), Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("Data"), Channel = TestChannel, Status = PacketStatus.InProgress, DateCreated = DateTime.Now}
        ]);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var columnFilter = new ColumnFilterDto(nameof(Packet.BinaryData), ColumnFilterOperator.Contains, "Data");
        var filter = new PacketRequestDto(ColumnFilters: [columnFilter]);
        _packetDtoService = new PacketDtoService(_logger, _packetTransfer, _metadata, _connectorTemplate);
        
        // Act
        var query = _packetDtoService.CreateFilteredQuery(context, filter);
        var packets = await query.AsNoTracking().ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(3);
        packets.Should().OnlyContain(p => Encoding.UTF8.GetString(p.BinaryData).Contains("Data"));
    }

    [Fact]
    public async Task CreateFilteredQuery_WithCustomFilterStringEqual_AppliesSqlQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.Packet.AddRangeAsync([
            new() { BinaryData = Encoding.UTF8.GetBytes("START   OK  END"), Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("START   NO  END"), Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("START   NO  END"), Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("START   OK  END"), Channel = TestChannel, Status = PacketStatus.InProgress, DateCreated = DateTime.Now}
        ]);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _packetTransfer.ConfigureCustomFilters(Arg.Do<ICustomFilterBuilder>(builder =>
        {
            builder.AddCustomFilter<string>("MyCustomFilter", "SUBSTRING(BinaryData, 9, 2)");
        }));

        var columnFilter = new ColumnFilterDto("MyCustomFilter", ColumnFilterOperator.Equal, "OK");
        var filter = new PacketRequestDto(ColumnFilters: [columnFilter]);

        _packetDtoService = new PacketDtoService(_logger, _packetTransfer, _metadata, _connectorTemplate);
        
        // Act
        var query = _packetDtoService.CreateFilteredQuery(context, filter);
        var packets = await query.AsNoTracking().ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => Encoding.UTF8.GetString(p.BinaryData).Substring(8, 2) == "OK");
    }

    [Fact]
    public async Task CreateFilteredQuery_WithCustomFilterStringNotEqual_AppliesSqlQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.Packet.AddRangeAsync([
            new() { BinaryData = Encoding.UTF8.GetBytes("START   OK  END"), Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("START   NO  END"), Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("START   NO  END"), Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("START   OK  END"), Channel = TestChannel, Status = PacketStatus.InProgress, DateCreated = DateTime.Now}
        ]);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _packetTransfer.ConfigureCustomFilters(Arg.Do<ICustomFilterBuilder>(builder =>
        {
            builder.AddCustomFilter<string>("MyCustomFilter", "SUBSTRING(BinaryData, 9, 2)");
        }));

        var columnFilter = new ColumnFilterDto("MyCustomFilter", ColumnFilterOperator.NotEqual, "OK");
        var filter = new PacketRequestDto(ColumnFilters: [columnFilter]);

        _packetDtoService = new PacketDtoService(_logger, _packetTransfer, _metadata, _connectorTemplate);
        
        // Act
        var query = _packetDtoService.CreateFilteredQuery(context, filter);
        var packets = await query.AsNoTracking().ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => Encoding.UTF8.GetString(p.BinaryData).Substring(8, 2) != "OK");
    }

    [Fact]
    public async Task CreateFilteredQuery_WithCustomFilterStartsWith_AppliesSqlQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.Packet.AddRangeAsync([
            new() { BinaryData = Encoding.UTF8.GetBytes("START   OK1 END"), Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("START   NO2 END"), Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("START   NOK END"), Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("START   OK4 END"), Channel = TestChannel, Status = PacketStatus.InProgress, DateCreated = DateTime.Now}
        ]);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _packetTransfer.ConfigureCustomFilters(Arg.Do<ICustomFilterBuilder>(builder =>
        {
            builder.AddCustomFilter<string>("MyCustomFilter", "SUBSTRING(BinaryData, 9, 3)");
        }));

        var columnFilter = new ColumnFilterDto("MyCustomFilter", ColumnFilterOperator.StartsWith, "OK");
        var filter = new PacketRequestDto(ColumnFilters: [columnFilter]);

        _packetDtoService = new PacketDtoService(_logger, _packetTransfer, _metadata, _connectorTemplate);
        
        // Act
        var query = _packetDtoService.CreateFilteredQuery(context, filter);
        var packets = await query.AsNoTracking().ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => Encoding.UTF8.GetString(p.BinaryData).Substring(8, 3).StartsWith("OK"));
    }

    [Fact]
    public async Task CreateFilteredQuery_WithCustomFilterEndsWith_AppliesSqlQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.Packet.AddRangeAsync([
            new() { BinaryData = Encoding.UTF8.GetBytes("START   1OK END"), Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("START   NO2 END"), Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("START   NOK END"), Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("START   OK4 END"), Channel = TestChannel, Status = PacketStatus.InProgress, DateCreated = DateTime.Now}
        ]);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _packetTransfer.ConfigureCustomFilters(Arg.Do<ICustomFilterBuilder>(builder =>
        {
            builder.AddCustomFilter<string>("MyCustomFilter", "SUBSTRING(BinaryData, 9, 3)");
        }));

        var columnFilter = new ColumnFilterDto("MyCustomFilter", ColumnFilterOperator.EndsWith, "OK");
        var filter = new PacketRequestDto(ColumnFilters: [columnFilter]);

        _packetDtoService = new PacketDtoService(_logger, _packetTransfer, _metadata, _connectorTemplate);
        
        // Act
        var query = _packetDtoService.CreateFilteredQuery(context, filter);
        var packets = await query.AsNoTracking().ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => Encoding.UTF8.GetString(p.BinaryData).Substring(8, 3).EndsWith("OK"));
    }

    [Fact]
    public async Task CreateFilteredQuery_WithCustomFilterContains_AppliesSqlQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.Packet.AddRangeAsync([
            new() { BinaryData = Encoding.UTF8.GetBytes("START   OK1 END"), Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("START   NO2 END"), Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("START   NOK END"), Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("START  OK4  END"), Channel = TestChannel, Status = PacketStatus.InProgress, DateCreated = DateTime.Now}
        ]);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _packetTransfer.ConfigureCustomFilters(Arg.Do<ICustomFilterBuilder>(builder =>
        {
            builder.AddCustomFilter<string>("MyCustomFilter", "SUBSTRING(BinaryData, 9, 4)");
        }));

        var columnFilter = new ColumnFilterDto("MyCustomFilter", ColumnFilterOperator.Contains, "OK");
        var filter = new PacketRequestDto(ColumnFilters: [columnFilter]);

        _packetDtoService = new PacketDtoService(_logger, _packetTransfer, _metadata, _connectorTemplate);
        
        // Act
        var query = _packetDtoService.CreateFilteredQuery(context, filter);
        var packets = await query.AsNoTracking().ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => Encoding.UTF8.GetString(p.BinaryData).Substring(8, 4).Contains("OK"));
    }

    [Fact]
    public async Task CreateFilteredQuery_WithCustomFilterNotContains_AppliesSqlQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.Packet.AddRangeAsync([
            new() { BinaryData = Encoding.UTF8.GetBytes("START   OK1 END"), Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("START   NO2 END"), Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("START   NOK END"), Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("START  OK4  END"), Channel = TestChannel, Status = PacketStatus.InProgress, DateCreated = DateTime.Now}
        ]);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _packetTransfer.ConfigureCustomFilters(Arg.Do<ICustomFilterBuilder>(builder =>
        {
            builder.AddCustomFilter<string>("MyCustomFilter", "SUBSTRING(BinaryData, 9, 4)");
        }));

        var columnFilter = new ColumnFilterDto("MyCustomFilter", ColumnFilterOperator.NotContains, "OK");
        var filter = new PacketRequestDto(ColumnFilters: [columnFilter]);

        _packetDtoService = new PacketDtoService(_logger, _packetTransfer, _metadata, _connectorTemplate);
        
        // Act
        var query = _packetDtoService.CreateFilteredQuery(context, filter);
        var packets = await query.AsNoTracking().ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => !Encoding.UTF8.GetString(p.BinaryData).Substring(8, 4).Contains("OK"));
    }

    [Fact]
    public async Task CreateFilteredQuery_WithCustomFilterEmpty_AppliesSqlQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.Packet.AddRangeAsync([
            new() { BinaryData = Encoding.UTF8.GetBytes("START   OK1 END"), Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("START       END"), Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("START   NOK END"), Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("START123    END"), Channel = TestChannel, Status = PacketStatus.InProgress, DateCreated = DateTime.Now}
        ]);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _packetTransfer.ConfigureCustomFilters(Arg.Do<ICustomFilterBuilder>(builder =>
        {
            builder.AddCustomFilter<string>("MyCustomFilter", "SUBSTRING(BinaryData, 9, 4)");
        }));

        var columnFilter = new ColumnFilterDto("MyCustomFilter", ColumnFilterOperator.Empty, null);
        var filter = new PacketRequestDto(ColumnFilters: [columnFilter]);

        _packetDtoService = new PacketDtoService(_logger, _packetTransfer, _metadata, _connectorTemplate);
        
        // Act
        var query = _packetDtoService.CreateFilteredQuery(context, filter);
        var packets = await query.AsNoTracking().ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => string.IsNullOrWhiteSpace(Encoding.UTF8.GetString(p.BinaryData).Substring(8, 4)));
    }

    [Fact]
    public async Task CreateFilteredQuery_WithCustomFilterNotEmpty_AppliesSqlQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.Packet.AddRangeAsync([
            new() { BinaryData = Encoding.UTF8.GetBytes("START   OK1 END"), Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("START       END"), Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("START   NOK END"), Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("START123    END"), Channel = TestChannel, Status = PacketStatus.InProgress, DateCreated = DateTime.Now}
        ]);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _packetTransfer.ConfigureCustomFilters(Arg.Do<ICustomFilterBuilder>(builder =>
        {
            builder.AddCustomFilter<string>("MyCustomFilter", "SUBSTRING(BinaryData, 9, 4)");
        }));

        var columnFilter = new ColumnFilterDto("MyCustomFilter", ColumnFilterOperator.NotEmpty, null);
        var filter = new PacketRequestDto(ColumnFilters: [columnFilter]);

        _packetDtoService = new PacketDtoService(_logger, _packetTransfer, _metadata, _connectorTemplate);
        
        // Act
        var query = _packetDtoService.CreateFilteredQuery(context, filter);
        var packets = await query.AsNoTracking().ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => !string.IsNullOrWhiteSpace(Encoding.UTF8.GetString(p.BinaryData).Substring(8, 4)));
    }

    [Fact]
    public async Task CreateFilteredQuery_WithCustomNumericFilterEqual_AppliesSqlQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.Packet.AddRangeAsync([
            new() { BinaryData = Encoding.UTF8.GetBytes("0005"), Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("5"), Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("50"), Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("125"), Channel = TestChannel, Status = PacketStatus.InProgress, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("05"), Channel = TestChannel, Status = PacketStatus.FatalError, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("Random"), Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now}
        ]);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _packetTransfer.ConfigureCustomFilters(Arg.Do<ICustomFilterBuilder>(builder =>
        {
            builder.AddCustomFilter<int>("MyCustomFilter", "CAST(BinaryData AS INT)");
        }));

        var columnFilter = new ColumnFilterDto("MyCustomFilter", ColumnFilterOperator.Equal, "5");
        var filter = new PacketRequestDto(ColumnFilters: [columnFilter]);

        _packetDtoService = new PacketDtoService(_logger, _packetTransfer, _metadata, _connectorTemplate);
        
        // Act
        var query = _packetDtoService.CreateFilteredQuery(context, filter);
        var packets = await query.AsNoTracking().ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(3);
        packets.Should().OnlyContain(p => Convert.ToInt32(Encoding.UTF8.GetString(p.BinaryData)) == 5);
    }

    [Fact]
    public async Task CreateFilteredQuery_WithCustomNumericFilterNotEqual_AppliesSqlQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.Packet.AddRangeAsync([
            new() { BinaryData = Encoding.UTF8.GetBytes("0005"), Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("5"), Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("50"), Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("125"), Channel = TestChannel, Status = PacketStatus.InProgress, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("05"), Channel = TestChannel, Status = PacketStatus.FatalError, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("Random"), Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now}
        ]);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _packetTransfer.ConfigureCustomFilters(Arg.Do<ICustomFilterBuilder>(builder =>
        {
            builder.AddCustomFilter<int>("MyCustomFilter", "CAST(BinaryData AS INT)");
        }));

        var columnFilter = new ColumnFilterDto("MyCustomFilter", ColumnFilterOperator.NotEqual, "5");
        var filter = new PacketRequestDto(ColumnFilters: [columnFilter]);

        _packetDtoService = new PacketDtoService(_logger, _packetTransfer, _metadata, _connectorTemplate);
        
        // Act
        var query = _packetDtoService.CreateFilteredQuery(context, filter);
        var packets = await query.AsNoTracking().ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(3);

        foreach (var packet in packets)
        {
            if (int.TryParse(packet.BinaryData, out var result))
            {
                result.Should().NotBe(5);
            }
        }
    }

    [Fact]
    public async Task CreateFilteredQuery_WithCustomFilterGreater_AppliesSqlQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.Packet.AddRangeAsync([
            new() { BinaryData = Encoding.UTF8.GetBytes("Data01"), Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("DataNr"), Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("Data03"), Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("Data05"), Channel = TestChannel, Status = PacketStatus.InProgress, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("Data11"), Channel = TestChannel, Status = PacketStatus.FatalError, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("Data15"), Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now}
        ]);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _packetTransfer.ConfigureCustomFilters(Arg.Do<ICustomFilterBuilder>(builder =>
        {
            builder.AddCustomFilter<int>("MyCustomFilter", "CAST(SUBSTRING(BinaryData, 5, 2) AS INT)");
        }));

        var columnFilter = new ColumnFilterDto("MyCustomFilter", ColumnFilterOperator.GreaterThan, "4");
        var filter = new PacketRequestDto(ColumnFilters: [columnFilter]);

        _packetDtoService = new PacketDtoService(_logger, _packetTransfer, _metadata, _connectorTemplate);
        
        // Act
        var query = _packetDtoService.CreateFilteredQuery(context, filter);
        var packets = await query.AsNoTracking().ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(3);
        packets.Should().OnlyContain(p => Convert.ToInt32(Encoding.UTF8.GetString(p.BinaryData).Substring(4, 2)) > 4);
    }

    [Fact]
    public async Task CreateFilteredQuery_WithCustomFilterGreaterOrEqual_AppliesSqlQuery()
    {
        // Arrange
        var dateTime = DateTime.Now;
        const string format = "yyyy-MM-dd HH:mm:ss";

        dateTime = DateTime.Parse(dateTime.ToString(format));

        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.Packet.AddRangeAsync([
            new() { BinaryData = Encoding.UTF8.GetBytes(dateTime.ToString(format)), Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes(dateTime.AddYears(-1).ToString(format)), Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes(dateTime.AddHours(1).ToString(format)), Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("invalid-date"), Channel = TestChannel, Status = PacketStatus.InProgress, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("9999-99-99 00:00:00"), Channel = TestChannel, Status = PacketStatus.FatalError, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes(dateTime.AddSeconds(-1).ToString(format)), Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes(dateTime.AddSeconds(1).ToString(format)), Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now}
        ]);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _packetTransfer.ConfigureCustomFilters(Arg.Do<ICustomFilterBuilder>(builder =>
        {
            builder.AddCustomFilter<DateTime>("MyCustomFilter", "DATETIME(BinaryData)");
        }));

        var columnFilter = new ColumnFilterDto("MyCustomFilter", ColumnFilterOperator.GreaterThanOrEqual, dateTime.ToString(format));
        var filter = new PacketRequestDto(ColumnFilters: [columnFilter]);

        _packetDtoService = new PacketDtoService(_logger, _packetTransfer, _metadata, _connectorTemplate);
        
        // Act
        var query = _packetDtoService.CreateFilteredQuery(context, filter);
        var packets = await query.AsNoTracking().ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(3);
        packets.Should().OnlyContain(p => Convert.ToDateTime(Encoding.UTF8.GetString(p.BinaryData)) >= dateTime);
    }

    [Fact]
    public async Task CreateFilteredQuery_WithCustomFilterLess_AppliesSqlQuery()
    {
        // Arrange
        var dateTime = DateTime.Now;
        const string format = "yyyy-MM-dd HH:mm:ss";

        dateTime = DateTime.Parse(dateTime.ToString(format));

        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.Packet.AddRangeAsync([
            new() { BinaryData = Encoding.UTF8.GetBytes(dateTime.ToString(format)), Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes(dateTime.AddYears(-1).ToString(format)), Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes(dateTime.AddHours(1).ToString(format)), Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("invalid-date"), Channel = TestChannel, Status = PacketStatus.InProgress, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("9999-99-99 00:00:00.000"), Channel = TestChannel, Status = PacketStatus.FatalError, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes(dateTime.AddSeconds(-1).ToString(format)), Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes(dateTime.AddSeconds(1).ToString(format)), Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now}
        ]);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _packetTransfer.ConfigureCustomFilters(Arg.Do<ICustomFilterBuilder>(builder =>
        {
            builder.AddCustomFilter<DateTime>("MyCustomFilter", "DATETIME(BinaryData)");
        }));

        var columnFilter = new ColumnFilterDto("MyCustomFilter", ColumnFilterOperator.LessThan, dateTime.ToString(format));
        var filter = new PacketRequestDto(ColumnFilters: [columnFilter]);

        _packetDtoService = new PacketDtoService(_logger, _packetTransfer, _metadata, _connectorTemplate);
        
        // Act
        var query = _packetDtoService.CreateFilteredQuery(context, filter);
        var packets = await query.AsNoTracking().ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(2);
        packets.Should().OnlyContain(p => Convert.ToDateTime(Encoding.UTF8.GetString(p.BinaryData)) < dateTime);
    }

    [Fact]
    public async Task CreateFilteredQuery_WithCustomFilterLessOrEqual_AppliesSqlQuery()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await context.Packet.AddRangeAsync([
            new() { BinaryData = Encoding.UTF8.GetBytes("Data01"), Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("Data02"), Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("Data03"), Channel = TestChannel, Status = PacketStatus.Error, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("Data05"), Channel = TestChannel, Status = PacketStatus.InProgress, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("Data11"), Channel = TestChannel, Status = PacketStatus.FatalError, DateCreated = DateTime.Now},
            new() { BinaryData = Encoding.UTF8.GetBytes("Data15"), Channel = TestChannel, Status = PacketStatus.Processed, DateCreated = DateTime.Now}
        ]);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _packetTransfer.ConfigureCustomFilters(Arg.Do<ICustomFilterBuilder>(builder =>
        {
            builder.AddCustomFilter<int>("MyCustomFilter", "CAST(SUBSTRING(BinaryData, 5, 2) AS INT)");
        }));

        var columnFilter = new ColumnFilterDto("MyCustomFilter", ColumnFilterOperator.LessThanOrEqual, "3");
        var filter = new PacketRequestDto(ColumnFilters: [columnFilter]);

        _packetDtoService = new PacketDtoService(_logger, _packetTransfer, _metadata, _connectorTemplate);
        
        // Act
        var query = _packetDtoService.CreateFilteredQuery(context, filter);
        var packets = await query.AsNoTracking().ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(3);
        packets.Should().OnlyContain(p => Convert.ToInt32(Encoding.UTF8.GetString(p.BinaryData).Substring(4, 2)) <= 3);
    }

    [Fact]
    public async Task CreateFilteredQuery_WithValueBuilder_FiltersCorrectly()
    {
        // Arrange
        await using var context = await _dbContextFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);

        await context.Packet.AddRangeAsync([
            new() { BinaryData = Encoding.UTF8.GetBytes("<test>alpha"), Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now },
            new() { BinaryData = Encoding.UTF8.GetBytes("<test>beta"), Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now },
            new() { BinaryData = Encoding.UTF8.GetBytes("alpha<test>>"), Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now },
            new() { BinaryData = Encoding.UTF8.GetBytes("beta"), Channel = TestChannel, Status = PacketStatus.Enqueued, DateCreated = DateTime.Now }
        ]);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _packetTransfer.ConfigureCustomFilters(Arg.Do<ICustomFilterBuilder>(builder =>
        {
            builder.AddCustomFilter<string>(
                "MyCustomFilter",
                "BinaryData",
                value => $"<test>{value}"
            );
        }));

        var columnFilter = new ColumnFilterDto("MyCustomFilter", ColumnFilterOperator.Contains, "alpha");
        var filter = new PacketRequestDto(ColumnFilters: [columnFilter]);

        _packetDtoService = new PacketDtoService(_logger, _packetTransfer, _metadata, _connectorTemplate);
        
        // Act
        var query = _packetDtoService.CreateFilteredQuery(context, filter);
        var packets = await query.AsNoTracking().ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert
        packets.Should().HaveCount(1);
        packets.Single().BinaryData.Should().BeEquivalentTo(Encoding.UTF8.GetBytes("<test>alpha"));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
