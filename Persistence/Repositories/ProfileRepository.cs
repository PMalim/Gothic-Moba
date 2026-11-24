using BotArena.Persistence.Models;
using Dapper;

namespace BotArena.Persistence.Repositories;

public sealed class ProfileRepository(Database database) : IProfileRepository
{
    private const string Columns =
        "uid AS Uid, name AS Name, points AS Points, lp AS Lp, wins AS Wins, losses AS Losses, ranked_games AS RankedGames, " +
        "selected_hero AS SelectedHero, owned_heroes AS OwnedHeroes";

    public async Task<ProfileRecord?> LoadAsync(string uid, CancellationToken cancellationToken = default)
    {
        await database.EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = new CommandDefinition($"SELECT {Columns} FROM profiles WHERE uid = @uid", new { uid }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ProfileRecord>(command).ConfigureAwait(false);
    }

    public async Task SaveAsync(ProfileRecord record, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO profiles (uid, name, points, lp, wins, losses, ranked_games, selected_hero, owned_heroes)
            VALUES (@Uid, @Name, @Points, @Lp, @Wins, @Losses, @RankedGames, @SelectedHero, @OwnedHeroes) AS new
            ON DUPLICATE KEY UPDATE name = new.name, points = new.points, lp = new.lp, wins = new.wins, losses = new.losses,
                ranked_games = new.ranked_games, selected_hero = new.selected_hero, owned_heroes = new.owned_heroes
            """;
        await database.EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(sql, record, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProfileRecord>> TopAsync(int limit, CancellationToken cancellationToken = default)
    {
        await database.EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = new CommandDefinition($"SELECT {Columns} FROM profiles ORDER BY lp DESC, wins DESC LIMIT @limit", new { limit }, cancellationToken: cancellationToken);
        return [.. await connection.QueryAsync<ProfileRecord>(command).ConfigureAwait(false)];
    }
}
