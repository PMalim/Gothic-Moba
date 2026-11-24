using BotArena.Persistence.Models;
using Dapper;

namespace BotArena.Persistence.Repositories;

public sealed class PlayerRepository(Database database) : IPlayerRepository
{
    private const string Columns =
        "uid AS Uid, name AS Name, team AS Team, kills AS Kills, deaths AS Deaths, play_seconds AS PlaySeconds, " +
        "last_world AS LastWorld, last_x AS LastX, last_y AS LastY, last_z AS LastZ";

    public string Description => database.Description;

    public async Task<PlayerRecord?> LoadAsync(string uid, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = new CommandDefinition($"SELECT {Columns} FROM players WHERE uid = @uid", new { uid }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<PlayerRecord>(command).ConfigureAwait(false);
    }

    public async Task SaveAsync(PlayerRecord record, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO players (uid, name, team, kills, deaths, play_seconds, last_world, last_x, last_y, last_z)
            VALUES (@Uid, @Name, @Team, @Kills, @Deaths, @PlaySeconds, @LastWorld, @LastX, @LastY, @LastZ) AS new
            ON DUPLICATE KEY UPDATE name = new.name, team = new.team, kills = new.kills, deaths = new.deaths,
                play_seconds = new.play_seconds, last_world = new.last_world, last_x = new.last_x, last_y = new.last_y,
                last_z = new.last_z
            """;
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(sql, record, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
