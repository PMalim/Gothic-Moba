namespace BotArena.Ai.Components;

public sealed class AlertState
{
    public Position? Center;
    public TimeSpan Until;
    public TimeSpan NextLook;
}

public sealed class Alert(TimeSpan? duration = null, float radius = 500) : MobComponent
{
    private readonly TimeSpan duration = duration ?? TimeSpan.FromSeconds(12);
    public override string Name => $"czujność {duration.TotalSeconds:0} s";

    public static void Begin(MobContext c, Position at)
    {
        if (c.Behaviour.Get<Alert>() is not { } alert) return;
        AlertState state = c.Get<AlertState>();
        state.Center = at;
        state.Until = c.Now + alert.duration;
        state.NextLook = c.Now + TimeSpan.FromMilliseconds(500);
    }

    public override void Sense(MobContext c)
    {
        AlertState state = c.Get<AlertState>();
        if (state.Center is not null && c.Now >= state.Until) state.Center = null;
    }

    public override IEnumerable<Branch> Branches()
    {
        yield return new Branch(Priority.Alert, new Sequence<MobContext>("czujność",
            new Condition<MobContext>("czujny?", c => c.Get<AlertState>() is { Center: not null } state && c.Now < state.Until),
            new Selector<MobContext>("pilnuje okolicy",
                new Sequence<MobContext>("obchód",
                    new Condition<MobContext>("pora się ruszyć?", c => c.Now >= c.Get<AlertState>().NextLook),
                    new NpcActionLeaf("obchodzi okolicę", c =>
                        {
                            Position point = c.RandomPoint(c.Get<AlertState>().Center!.Value, 150, radius);
                            c.Mob.SetFocus(point, FocusKind.Search);
                            return c.Commands.MoveTo(c.Npc, point, new Distance(80), walk: true, TimeSpan.FromSeconds(6));
                        },
                        TimeSpan.FromSeconds(7), finished: (c, _) =>
                            c.Get<AlertState>().NextLook = c.Now + TimeSpan.FromMilliseconds(c.Random.Next(1200, 2600)))),
                new Do<MobContext>("nasłuchuje", _ => NodeStatus.Running))));
    }
}
