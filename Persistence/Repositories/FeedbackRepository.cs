using BotArena.Persistence.Models;
using Dapper;

namespace BotArena.Persistence.Repositories;

public sealed class FeedbackRepository(Database database) : IFeedbackRepository
{
    public async Task<long> AddAsync(FeedbackRecord record, CancellationToken cancellationToken = default)
    {
        await database.EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        const string sql = """
            INSERT INTO feedback (account, player_name, kind, priority, title, message, context)
            VALUES (@Account, @PlayerName, @Kind, @Priority, @Title, @Message, @Context);
            SELECT LAST_INSERT_ID();
            """;
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, record, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<FeedbackRecord>> ListAsync(int limit, string? status = null, CancellationToken cancellationToken = default)
    {
        await database.EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        const string sql = """
            SELECT id AS Id, created_at AS CreatedAt, account AS Account, player_name AS PlayerName, kind AS Kind, priority AS Priority,
                   title AS Title, message AS Message, context AS Context, status AS Status
            FROM feedback WHERE (@status IS NULL OR status = @status) ORDER BY id DESC LIMIT @limit
            """;
        return [.. await connection.QueryAsync<FeedbackRecord>(new CommandDefinition(sql, new { status, limit }, cancellationToken: cancellationToken)).ConfigureAwait(false)];
    }

    public async Task<bool> SetStatusAsync(long id, string status, CancellationToken cancellationToken = default)
    {
        await database.EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(new CommandDefinition("UPDATE feedback SET status = @status WHERE id = @id", new { id, status }, cancellationToken: cancellationToken)).ConfigureAwait(false) > 0;
    }

    public async Task<int> CountRecentAsync(string account, TimeSpan window, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM feedback WHERE account = @account AND created_at >= @since",
            new { account, since = DateTime.Now - window }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
