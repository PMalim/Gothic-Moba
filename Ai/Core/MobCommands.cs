namespace BotArena.Ai;

public sealed class MobCommands(Server server)
{
    private const int Walk = 1, Flee = 2;

    public int Chase(Npc npc, Character target, Distance stop, bool walk, TimeSpan timeout, int flankAngle = 0) =>
        Script("arenaChase", npc.Id.Value, IdOf(target), stop.Centimeters, walk ? Walk : 0, Milliseconds(timeout), flankAngle);

    public static Func<Position, Position, IReadOnlyList<Position>?>? Navigator { get; set; }

    public int PathTo(Npc npc, Character? target, Position goal, Distance stop, bool walk, TimeSpan timeout)
    {
        if (PositionOf(npc) is { } from && Navigator?.Invoke(from, goal) is { Count: > 0 } route) return Follow(npc, target, route, stop, walk, timeout);
        return Script("arenaPath", npc.Id.Value, target is null ? -1 : IdOf(target), goal.X, goal.Y, goal.Z, stop.Centimeters,
            walk ? Walk : 0, Milliseconds(timeout));
    }

    private static Position? PositionOf(Npc npc)
    {
        if (!npc.IsValid) return null;
        try { return npc.Position; }
        catch (Exception e) when (e is ObjectDisposedException or InvalidOperationException) { return null; }
    }

    public int Follow(Npc npc, Character? target, IReadOnlyList<Position> route, Distance stop, bool walk, TimeSpan timeout)
    {
        string points = string.Join(",", route.Select(p => FormattableString.Invariant($"{p.X:0},{p.Y:0},{p.Z:0}")));
        return Script("arenaFollow", npc.Id.Value, target is null ? -1 : IdOf(target), stop.Centimeters, walk ? Walk : 0, Milliseconds(timeout), points);
    }

    public int MoveTo(Npc npc, Position point, Distance stop, bool walk, TimeSpan timeout) =>
        Script("arenaMove", npc.Id.Value, point.X, point.Y, point.Z, stop.Centimeters, walk ? Walk : 0, Milliseconds(timeout));

    public int FleeFrom(Npc npc, Position threat, Distance safe, TimeSpan timeout) =>
        Script("arenaMove", npc.Id.Value, threat.X, threat.Y, threat.Z, safe.Centimeters, Flee, Milliseconds(timeout));

    public int AttackMelee(Npc npc, Character target, MeleeAttack attack, int combo)
    {
        npc.AttackMelee(target, attack, combo, turn: true);
        return npc.Actions.LastId.Value;
    }

    public int AttackRanged(Npc npc, Character target)
    {
        npc.AttackRanged(target, turn: true);
        return npc.Actions.LastId.Value;
    }

    public int CastSpell(Npc npc, Character target)
    {
        npc.CastSpell(target, turn: true);
        return npc.Actions.LastId.Value;
    }

    public void ReadySpell(Npc npc, int slot, int mana) => npc.ReadySpell(slot, mana);

    public void UnreadySpell(Npc npc) => npc.UnreadySpell();

    public int DrawWeapon(Npc npc, WeaponMode mode)
    {
        npc.DrawWeapon(mode);
        return npc.Actions.LastId.Value;
    }

    public void Face(Npc npc, Position from, Position to) =>
        npc.SetAngle(new Angle(server.Scripting.Call<float>("getVectorAngle", from.X, from.Z, to.X, to.Z)));

    public void Cancel(Npc npc) => npc.Actions.Clear();

    private int Script(string function, params object?[] arguments) =>
        server.Scripting.Call(function, arguments) is long id ? (int)id : -1;

    private static int Milliseconds(TimeSpan timeout) => (int)Math.Clamp(timeout.TotalMilliseconds, 200, 60000);

    public static int IdOf(Character character) => character switch
    {
        Player player => player.Id.Value,
        Npc npc => npc.Id.Value,
        _ => throw new ArgumentException("Unknown character type.", nameof(character)),
    };
}
