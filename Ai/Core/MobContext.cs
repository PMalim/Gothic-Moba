namespace BotArena.Ai;

public readonly record struct Combatant(Character Character, Position Position, Mob? Mob)
{
    public bool IsPlayer => Mob is null;
    public string Name => Mob?.Label ?? ((Player)Character).Name;
}

public sealed class MobContext : IBehaviorContext
{
    public required Mob Mob { get; init; }
    public required Npc Npc { get; init; }
    public required NpcState State { get; init; }
    public required TimeSpan Now { get; init; }
    public required IReadOnlyList<Combatant> Players { get; init; }
    public required IReadOnlyList<Combatant> Mobs { get; init; }
    public required IReadOnlyDictionary<Character, Combatant> Index { get; init; }
    public required MobCommands Commands { get; init; }
    public required Random Random { get; init; }
    public required MobDirector Director { get; init; }
    public Combatant? Target { get; set; }
    public bool Replan { get; set; }
    public TimeSpan ActionElapsed { get; internal set; }

    public BehaviorMemory Tree => Mob.Tree!;
    public MobMemory Memory => Mob.Memory;
    public MobKind Kind => Mob.Kind;
    public MobBehaviour Behaviour => Mob.Behaviour;
    public Position Position => State.Position;
    public float Health => State.MaxHealth > 0 ? (float)State.Health / State.MaxHealth : 1f;
    public Distance FromHome => State.Position.DistanceTo(Mob.Home);
    public float TargetRadius => Target is { Mob: { } targetMob } ? targetMob.Kind.Radius : 0f;
    public Distance TargetDistance => Target is { } target
        ? new Distance(MathF.Max(0f, Position.DistanceTo(target.Position).Centimeters - TargetRadius))
        : new Distance(float.MaxValue);
    public KnownCharacter? TargetKnown => Target is { } target ? Memory.Recall(target.Character) : null;

    public T Get<T>() where T : class, new() => Memory.Get<T>();

    public TimeSpan Jitter(TimeSpan value) => value * (0.85 + Random.NextDouble() * 0.3);

    public Combatant? Find(Character character) => Index.TryGetValue(character, out Combatant found) ? found : null;

    public Position RandomPoint(Position center, float min, float max)
    {
        float angle = (float)(Random.NextDouble() * Math.Tau);
        float radius = min + (float)Random.NextDouble() * (max - min);
        return new Position(center.X + MathF.Cos(angle) * radius, center.Y, center.Z + MathF.Sin(angle) * radius);
    }
}
