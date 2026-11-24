using Dapper;
using MySqlConnector;

namespace BotArena.Persistence;

public static class MigrationRunner
{
    public static async Task<IReadOnlyList<string>> ApplyAsync(MySqlConnection connection, string directory, CancellationToken cancellationToken = default)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "CREATE TABLE IF NOT EXISTS schema_migrations (name VARCHAR(255) NOT NULL PRIMARY KEY, applied_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP)",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        var applied = (await connection.QueryAsync<string>(new CommandDefinition("SELECT name FROM schema_migrations", cancellationToken: cancellationToken))
            .ConfigureAwait(false)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var done = new List<string>();
        if (!Directory.Exists(directory)) return done;
        foreach (string file in Directory.GetFiles(directory, "*.sql").Order(StringComparer.OrdinalIgnoreCase))
        {
            string name = Path.GetFileName(file);
            if (applied.Contains(name)) continue;
            string sql = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
            await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
            await connection.ExecuteAsync(new CommandDefinition("INSERT INTO schema_migrations (name) VALUES (@name)", new { name }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            done.Add(name);
        }
        return done;
    }
}
