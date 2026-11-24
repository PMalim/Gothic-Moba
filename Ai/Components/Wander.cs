namespace BotArena.Ai.Components;

public sealed class WanderState
{
    public TimeSpan NextWander;
}

public sealed class Wander(float radius = 350, bool stroll = true) : MobComponent
{
    public override string Name => stroll ? $"spacer {radius / 100:0.#} m" : "posterunek";

    public override void Sense(MobContext c)
    {
        WanderState state = c.Get<WanderState>();
        if (state.NextWander == default) state.NextWander = c.Now + TimeSpan.FromMilliseconds(c.Random.Next(1000, 4000));
    }

    public override IEnumerable<Node<MobContext>> Idle()
    {
        float away = MathF.Max(radius * 2, 300);
        yield return new Sequence<MobContext>("powrót do domu",
            new Condition<MobContext>("daleko od domu?", c => c.FromHome.Centimeters > away),
            new Cooldown<MobContext>(TimeSpan.FromSeconds(3), new NpcActionLeaf("wraca spacerem do domu", c =>
                {
                    c.Mob.SetFocus(c.Mob.Home, FocusKind.Home);
                    return c.FromHome.Centimeters > 900
                        ? c.Commands.PathTo(c.Npc, null, c.Mob.Home, new Distance(150), walk: true, TimeSpan.FromSeconds(40))
                        : c.Commands.MoveTo(c.Npc, c.Mob.Home, new Distance(150), walk: true, TimeSpan.FromSeconds(10));
                },
                TimeSpan.FromSeconds(42))));
        if (!stroll) yield break;
        yield return new Sequence<MobContext>("spacer",
            new Condition<MobContext>("pora na spacer?", c => c.Now >= c.Get<WanderState>().NextWander),
            new NpcActionLeaf("spaceruje", c =>
                {
                    Position point = c.RandomPoint(c.Mob.Home, radius * 0.3f, radius);
                    c.Mob.SetFocus(point, FocusKind.Wander);
                    return c.Commands.MoveTo(c.Npc, point, new Distance(80), walk: true, TimeSpan.FromSeconds(8));
                },
                TimeSpan.FromSeconds(9), finished: (c, _) =>
                    c.Get<WanderState>().NextWander = c.Now + TimeSpan.FromMilliseconds(c.Random.Next(5000, 11000))));
    }
}
