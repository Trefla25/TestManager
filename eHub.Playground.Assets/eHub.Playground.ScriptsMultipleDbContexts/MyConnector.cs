using eHub.PlugIn;
using eController.Util;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace eHub.Playground.ScriptsMultipleDbContexts;

public class MyConnector(
    IOptions<MyConfig> options,
    ILogger<MyConnector> logger,
    IConfiguration configuration)
    : IPacketTransfer
{
    private readonly MyConfig _options = options.Value;
    private readonly IConfiguration _configuration = configuration;
    public IPacketConverter Converter => new MyConverter();

    public IPacketRepository PacketRepository { get; set; } = default!;

    public event UpdateStatusDelegate? UpdateStatus;

    public ValueTask<ProcessPacketState> ProcessPacket(PacketData packet, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(ProcessPacketState.Success);
    }

    public async Task Run(CancellationToken cancellationToken)
    {
        var connectionString = _configuration.GetConnectionString("PacketTransfer");

        var dbOptionsBuilder = new DbContextOptionsBuilder<MyDbContext>()
            .UseSqlite(connectionString);

        var dbContext = new MyDbContext(dbOptionsBuilder.Options);

        await InitializeDatabase(dbContext);

        await dbContext.AddAsync(new MyEntity() { Data = "Random Data", Status = 1, DateCreated = DateTime.Now }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        await Task.CompletedTask;
    }

    private async ValueTask InitializeDatabase(DbContext dbContext)
    {
        var currentMigrations = (await dbContext.Database.GetAppliedMigrationsAsync()).ToArray();
        var dbExists = currentMigrations.Any();
        if (dbExists)
        {
            logger.LogInformation("PacketTransfer database on schema version: {CurrentSchemaVersion}", currentMigrations.Last());
        }
        else
        {
            logger.LogInformation("PacketTransfer database does not exist yet");
        }

        var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync()).ToArray();
        if (pendingMigrations.Length > 0)
        {
            logger.LogInformation("Applying {Count} migrations.", pendingMigrations.Length);
            await dbContext.Database.MigrateAsync();

            var migratedTo = (await dbContext.Database.GetAppliedMigrationsAsync()).Last();
            logger.LogInformation("PacketTransfer database upgraded to schema version: {NewSchemaVersion}", migratedTo);
        }
    }
}
