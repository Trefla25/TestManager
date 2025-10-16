using eHub.Database;
using eHub.Tests.Helper;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace eHub.Tests.Database;

[TestClass]
public class SqlitePragmasInterceptorTests
{
    private ConnectionEndEventData _eventData = default!;
    private TestEventDefinitionBase _eventDefinition = default!;
    private SqliteConnection _connection = default!;
    private HubDbContext _dbContext = default!;

    private readonly Func<EventDefinitionBase, EventData, string> _messageGenerator = (eventDef, eventData)
        => "Test connection end event generated.";

    [TestInitialize]
    public async Task TestInitialize()
    {
        var loggerOptions = Substitute.For<ILoggingOptions>();
        var eventId = new EventId();
        var level = LogLevel.Information;
        var eventIdCode = "code";

        _eventDefinition = new TestEventDefinitionBase(loggerOptions, eventId, level, eventIdCode);
        _connection = new SqliteConnection(DbHelper.InMemoryConnectionString);

        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<HubDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new HubDbContext(options);

        _eventData = new ConnectionEndEventData(
           _eventDefinition,
           _messageGenerator,
           _connection,
           _dbContext,
           new Guid(), true, DateTime.Now, TimeSpan.FromSeconds(1));
    }

    [TestMethod]
    public async Task ConnectionOpenedAsync_WhenCalledWithValues_AppliesSqlitePragmas()
    {
        var pragmas = new Dictionary<string, string>
        {
            { "busy_timeout", "3000" },
            { "user_version", "123" }
        };

        var interceptor = new SqlitePragmasInterceptor(pragmas);

        await interceptor.ConnectionOpenedAsync(_connection, _eventData);

        await using var cmd = _connection.CreateCommand();

        cmd.CommandText = "PRAGMA busy_timeout;";
        var busyResult = await cmd.ExecuteScalarAsync();

        cmd.CommandText = "PRAGMA user_version;";
        var versionResult = await cmd.ExecuteScalarAsync();

        busyResult.Should().Be(3000);
        versionResult.Should().Be(123);
    }

    [TestMethod]
    public async Task ConnectionOpenedAsync_WhenCalledEmpty_DoesNotApplySqlitePragmas()
    {
        var pragmas = new Dictionary<string, string>();
        var interceptor = new SqlitePragmasInterceptor(pragmas);


        await interceptor.ConnectionOpenedAsync(_connection, _eventData);

        await using var cmd = _connection.CreateCommand();

        cmd.CommandText = "PRAGMA busy_timeout;";
        var busyResult = await cmd.ExecuteScalarAsync();

        cmd.CommandText = "PRAGMA user_version;";
        var versionResult = await cmd.ExecuteScalarAsync();

        busyResult.Should().Be(0);
        versionResult.Should().Be(0);
    }

    [TestCleanup]
    public async Task TestCleanup()
    {
        await _dbContext.DisposeAsync();
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }
}


