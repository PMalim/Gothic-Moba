using BotArena.Persistence.Models;
using Dapper;

namespace BotArena.Persistence.Repositories;

public sealed class VobRepository(Database database) : IVobRepository
{
    private const string Columns =
        "id AS Id, world AS World, visual AS Visual, x AS X, y AS Y, z AS Z, yaw AS Yaw, pitch AS Pitch, roll AS Roll, " +
        "scale AS Scale, collision AS Collision, placed_by AS PlacedBy, placed_uid AS PlacedUid";

    public string Description => database.Description;

    public async Task<IReadOnlyList<PlacedVob>> ListAsync(string world, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = new CommandDefinition($"SELECT {Columns} FROM world_vobs WHERE world = @world ORDER BY id", new { world }, cancellationToken: cancellationToken);
        return (await connection.QueryAsync<PlacedVob>(command).ConfigureAwait(false)).ToList();
    }

    public async Task<int> InsertAsync(PlacedVob vob, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO world_vobs (world, visual, x, y, z, yaw, pitch, roll, scale, collision, placed_by, placed_uid)
            VALUES (@World, @Visual, @X, @Y, @Z, @Yaw, @Pitch, @Roll, @Scale, @Collision, @PlacedBy, @PlacedUid);
            SELECT LAST_INSERT_ID();
            """;
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, vob, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<bool> UpdateAsync(PlacedVob vob, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE world_vobs SET x = @X, y = @Y, z = @Z, yaw = @Yaw, pitch = @Pitch, roll = @Roll, scale = @Scale, collision = @Collision
            WHERE id = @Id
            """;
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(new CommandDefinition(sql, vob, cancellationToken: cancellationToken)).ConfigureAwait(false) > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(new CommandDefinition("DELETE FROM world_vobs WHERE id = @id", new { id }, cancellationToken: cancellationToken)).ConfigureAwait(false) > 0;
    }
}
