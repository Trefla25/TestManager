using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace eHub.Database;

public class SqlitePragmasInterceptor(IReadOnlyDictionary<string, string> pragmas) : DbConnectionInterceptor
{
    private readonly IReadOnlyDictionary<string, string> _pragmas = pragmas;

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (connection is SqliteConnection sqlite)
        {
            using var cmd = sqlite.CreateCommand();
            foreach (var kv in _pragmas)
            {
                cmd.CommandText += $"PRAGMA {kv.Key} = {kv.Value};";
            }

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }
}
