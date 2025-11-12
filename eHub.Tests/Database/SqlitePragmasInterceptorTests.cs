using System.Collections.Frozen;
using eHub.Database;
using eHub.Tests.Helper;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace eHub.Tests.Database;

public class SqlitePragmasInterceptorTests : IAsyncLifetime
{
    private ConnectionEndEventData _eventData = null!;
    private TestEventDefinitionBase _eventDefinition = null!;
    private SqliteConnection _connection = null!;
    private HubDbContext _dbContext = null!;

    private readonly Func<EventDefinitionBase, EventData, string> _messageGenerator = (_, _)
        => "Test connection end event generated.";

    public async ValueTask InitializeAsync()
    {
        var loggerOptions = Substitute.For<ILoggingOptions>();
        var eventId = new EventId();
        const LogLevel level = LogLevel.Information;
        const string eventIdCode = "code";

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
           Guid.Empty, true, DateTime.Now, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ConnectionOpenedAsync_WhenCalledWithValues_AppliesSqlitePragmas()
    {
        // Arrange
        var pragmas = new Dictionary<string, string>
        {
            { "busy_timeout", "3000" },
            { "user_version", "123" }
        };

        // Act
        var interceptor = new SqlitePragmasInterceptor(pragmas);

        await interceptor.ConnectionOpenedAsync(_connection, _eventData, TestContext.Current.CancellationToken);

        await using var cmd = _connection.CreateCommand();

        cmd.CommandText = "PRAGMA busy_timeout;";
        var busyResult = await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        cmd.CommandText = "PRAGMA user_version;";
        var versionResult = await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        // Assert
        busyResult.Should().Be(3000);
        versionResult.Should().Be(123);
    }

    [Fact]
    public async Task ConnectionOpenedAsync_WhenCalledEmpty_DoesNotApplySqlitePragmas()
    {
        // Arrange
        var pragmas = FrozenDictionary<string, string>.Empty;
        var interceptor = new SqlitePragmasInterceptor(pragmas);

        // Act
        await interceptor.ConnectionOpenedAsync(_connection, _eventData, TestContext.Current.CancellationToken);

        await using var cmd = _connection.CreateCommand();

        cmd.CommandText = "PRAGMA busy_timeout;";
        var busyResult = await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        cmd.CommandText = "PRAGMA user_version;";
        var versionResult = await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        
        // Assert
        busyResult.Should().Be(0);
        versionResult.Should().Be(0);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }
}
