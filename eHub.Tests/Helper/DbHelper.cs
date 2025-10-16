using System.Data.Common;
using System.Text;
using eHub.Database;
using eHub.Database.Models;
using eHub.PlugIn;
using Microsoft.EntityFrameworkCore;

namespace eHub.Tests.Helper;

internal static class DbHelper
{
    public const string InMemoryConnectionString = "DataSource=:memory:";

    public static IDbContextFactory<TContext> CreateInMemoryFactory<TContext>(string connectionString = InMemoryConnectionString)
        where TContext : DbContext
    {
        var options = new DbContextOptionsBuilder<TContext>()
            .UseSqlite(connectionString)
            .Options;

        return new SimpleDbContextFactory<TContext>(options);
    }

    public static IDbContextFactory<TContext> CreateInMemoryFactory<TContext>(DbConnection connection)
        where TContext : DbContext
    {
        var options = new DbContextOptionsBuilder<TContext>()
            .UseSqlite(connection)
            .Options;

        return new SimpleDbContextFactory<TContext>(options);
    }

    public static async Task AddTestPacketsAsync(this HubDbContext context, int count, DateTime startDate, DateTime endDate, HashSet<string>? channels = null)
    {
        channels ??= ["TestChannel"];
        var random = new Random();

        // Get all values of the PacketStatus enum.
        var statuses = Enum.GetValues<PacketStatus>().Cast<PacketStatus>().ToArray();

        // Calculate the interval between packets.
        var totalDuration = endDate - startDate;
        var interval = count > 1
            ? TimeSpan.FromTicks(totalDuration.Ticks / (count - 1))
            : TimeSpan.Zero;

        for (int i = 0; i < count; i++)
        {
            var packet = new Packet
            {
                BinaryData = Encoding.UTF8.GetBytes($"BinaryData{i}"),
                Channel = channels.ElementAt(random.Next(channels.Count)),
                Status = statuses[random.Next(statuses.Length)],
                DateCreated = startDate.AddTicks(interval.Ticks * i)
            };

            await context.Packet.AddAsync(packet);
        }

        await context.SaveChangesAsync();
    }
}
