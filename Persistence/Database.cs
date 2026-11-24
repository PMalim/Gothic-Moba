using MySqlConnector;

namespace BotArena.Persistence;

public sealed class Database(string connectionString, string migrationsDirectory)
{
    public string Description
    {
        get
        {
            var builder = new MySqlConnectionStringBuilder(connectionString);
            return $"MySQL {builder.UserID}@{builder.Server}:{builder.Port}/{builder.Database}";
        }
    }

    private readonly SemaphoreSlim schemaLock = new(1, 1);
    private volatile bool schemaReady;

    public async Task<IReadOnlyList<string>> InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> applied = await MigrationRunner.ApplyAsync(connection, migrationsDirectory, cancellationToken).ConfigureAwait(false);
        schemaReady = true;
        return applied;
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (schemaReady) return;
        await schemaLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { if (!schemaReady) await InitializeAsync(cancellationToken).ConfigureAwait(false); }
        finally { schemaLock.Release(); }
    }

    public async Task<MySqlConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new MySqlConnection(connectionString);
        try { await connection.OpenAsync(cancellationToken).ConfigureAwait(false); return connection; }
        catch { await connection.DisposeAsync().ConfigureAwait(false); throw; }
    }

    public static Database? Create()
    {
        string? connection = DatabaseSettings.ConnectionString();
        if (connection is null) return null;
        string baseDirectory = Path.GetDirectoryName(typeof(Database).Assembly.Location) is { Length: > 0 } dir ? dir : AppContext.BaseDirectory;
        string[] candidates = [Path.Combine(baseDirectory, "migrations"), Path.Combine("gamemodes", "BotArena", "migrations"), Path.Combine(AppContext.BaseDirectory, "gamemodes", "BotArena", "migrations")];
        string migrations = candidates.FirstOrDefault(path => Directory.Exists(path) && Directory.GetFiles(path, "*.sql").Length > 0) ?? candidates[1];
        return new Database(connection, migrations);
    }
}
