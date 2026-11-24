namespace BotArena.Ai.Components;

public sealed class Search(int sweeps = 2, float radius = 600, TimeSpan? lookAround = null) : MobComponent
{
    private const float Lead = 2.5f, MaxLead = 1200f;
    private static readonly Distance Spot = new(200);
    private readonly TimeSpan look = lookAround ?? TimeSpan.FromSeconds(1.5);
    public override string Name => "szukanie";

    public override void React(MobContext c)
    {
        if (c.Mob.Quarry is { } quarry && c.Target is null && c.Memory.Recall(quarry) is null)
        {
            c.Mob.Quarry = null;
            Alert.Begin(c, c.Position);
        }
    }

    public override IEnumerable<Branch> Branches()
    {
        var steps = new List<Node<MobContext>>
        {
            new Optional<MobContext>(new NpcActionLeaf("biegnie tam, gdzie go widział", c =>
                {
                    KnownCharacter known = Lost(c)!;
                    c.Mob.SetFocus(known.LastSeen, FocusKind.LastSeen);
                    return c.Commands.PathTo(c.Npc, known.Character, known.LastSeen, Spot, walk: false, TimeSpan.FromSeconds(12));
                },
                TimeSpan.FromSeconds(13))),
            new Optional<MobContext>(new NpcActionLeaf("biegnie, dokąd uciekał", c =>
                {
                    KnownCharacter known = Lost(c)!;
                    Position ahead = known.Predict(Lead, MaxLead);
                    if (ahead.DistanceTo(known.LastSeen).Centimeters < 300) return -1;
                    c.Mob.SetFocus(ahead, FocusKind.Search);
                    return c.Commands.PathTo(c.Npc, known.Character, ahead, Spot, walk: false, TimeSpan.FromSeconds(10));
                },
                TimeSpan.FromSeconds(11))),
            new Wait<MobContext>("rozgląda się", _ => look, started: LookAround),
        };
        for (int i = 1; i <= sweeps; i++)
            steps.Add(new Optional<MobContext>(new NpcActionLeaf($"przeszukuje okolicę {i}/{sweeps}", c =>
                {
                    KnownCharacter known = Lost(c)!;
                    Position point = c.RandomPoint(known.Predict(Lead, MaxLead), 250, radius);
                    c.Mob.SetFocus(point, FocusKind.Search);
                    return c.Commands.MoveTo(c.Npc, point, new Distance(100), walk: false, TimeSpan.FromSeconds(5));
                },
                TimeSpan.FromSeconds(6))));
        steps.Add(new Do<MobContext>("traci trop", c =>
        {
            if (c.Mob.Quarry is { } quarry) c.Memory.Forget(quarry);
            c.Mob.Quarry = null;
            Alert.Begin(c, c.Position);
            return NodeStatus.Success;
        }));

        yield return new Branch(Priority.Search, new Sequence<MobContext>("szukanie",
            new Condition<MobContext>("zgubił cel?", c => c.Target is null && Lost(c) is not null),
            new Steps<MobContext>("trop", [.. steps])));
    }

    private static KnownCharacter? Lost(MobContext c) => c.Memory.Recall(c.Mob.Quarry) is { Visible: false } known ? known : null;

    private static void LookAround(MobContext c)
    {
        if (Lost(c) is { } known) c.Mob.SetFocus(known.LastSeen, FocusKind.LastSeen);
        float angle = (float)(c.Random.NextDouble() * Math.Tau);
        var look = new Position(c.Position.X + MathF.Sin(angle) * 500, c.Position.Y, c.Position.Z + MathF.Cos(angle) * 500);
        c.Commands.Face(c.Npc, c.Position, look);
    }
}
