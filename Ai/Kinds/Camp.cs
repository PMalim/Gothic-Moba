namespace BotArena.Ai.Kinds;

public sealed record Camp(string Name, float OffsetX, float OffsetZ, params MobKind[] Members)
{
    public float? Ground { get; init; }

    public Position[]? Spots { get; init; }

    public TimeSpan? Respawn { get; init; }

    public TimeSpan? FirstSpawn { get; init; }

    public static Camp At(string name, Position at, params MobKind[] members) =>
        new(name, at.X - ArenaSettings.Center.X, at.Z - ArenaSettings.Center.Z, members) { Ground = at.Y };

    public Position CenterFrom(Position arena) => new(arena.X + OffsetX, Ground ?? arena.Y, arena.Z + OffsetZ);

    public Position HomeOf(Position arena, int index)
    {
        if (Spots is { } spots && index < spots.Length) return spots[index];
        Position center = CenterFrom(arena);
        if (Members[index].Behaviour.Get<Pack>() is { Leader: true }) return center;
        float angle = index * MathF.Tau / Members.Length;
        return new Position(center.X + MathF.Cos(angle) * 280, center.Y, center.Z + MathF.Sin(angle) * 280);
    }
}
