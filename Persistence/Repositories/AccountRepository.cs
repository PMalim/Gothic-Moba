using BotArena.Persistence.Models;
using Dapper;
using MySqlConnector;

namespace BotArena.Persistence.Repositories;

public sealed class AccountRepository(Database database) : IAccountRepository
{
    private const string Columns =
        "username AS Username, display_name AS DisplayName, pass_hash AS PassHash, salt AS Salt, iterations AS Iterations, last_uid AS LastUid";

    public async Task<AccountRecord?> FindAsync(string username, CancellationToken cancellationToken = default)
    {
        await database.EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = new CommandDefinition($"SELECT {Columns} FROM accounts WHERE username = @username", new { username }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<AccountRecord>(command).ConfigureAwait(false);
    }

    public async Task<bool> CreateAsync(AccountRecord record, CancellationToken cancellationToken = default)
    {
        await database.EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        const string sql = """
            INSERT INTO accounts (username, display_name, pass_hash, salt, iterations, last_uid)
            VALUES (@Username, @DisplayName, @PassHash, @Salt, @Iterations, @LastUid)
            """;
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(sql, record, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return true;
        }
        catch (MySqlException error) when (error.ErrorCode == MySqlErrorCode.DuplicateKeyEntry) { return false; }
    }

    public async Task TouchLoginAsync(string username, string uid, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition("UPDATE accounts SET last_login = CURRENT_TIMESTAMP, last_uid = @uid WHERE username = @username",
            new { username, uid }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
