namespace BotArena.Ai.Components;

public sealed class FleeState
{
    public bool FledThisFight;
    public TimeSpan PanicUntil;
    public Character? PanicFrom;
}

public sealed class Flee(float below = 0, bool whenHurt = false) : MobComponent
{
    private static readonly Distance Safe = new(1400);
    public override string Name => whenHurt ? "płochliwy" : below > 0 ? $"ucieczka < {below * 100:0}% HP" : "panika";

    public override void Sense(MobContext c)
    {
        if (c.Health >= 0.9f) c.Get<FleeState>().FledThisFight = false;
    }

    public override IEnumerable<Branch> Branches()
    {
        yield return new Branch(Priority.Flee, new Sequence<MobContext>("ucieczka",
            new Condition<MobContext>("powód do ucieczki?", ShouldFlee),
            new NpcActionLeaf("ucieka", c =>
                {
                    Position threat = ThreatPosition(c);
                    c.Mob.SetFocus(threat, FocusKind.Threat);
                    return c.Commands.FleeFrom(c.Npc, threat, Safe, TimeSpan.FromSeconds(4));
                },
                TimeSpan.FromSeconds(5), finished: (c, _) =>
                {
                    FleeState state = c.Get<FleeState>();
                    state.FledThisFight = true;
                    state.PanicUntil = default;
                })));
    }

    public override void Damaged(MobHit hit)
    {
        if (whenHurt) Panic(hit.Mob, hit.Attacker, hit.Now + TimeSpan.FromSeconds(4));
    }

    public static void Panic(Mob mob, Character from, TimeSpan until)
    {
        FleeState state = mob.Memory.Get<FleeState>();
        state.PanicUntil = until;
        state.PanicFrom = from;
    }

    private bool ShouldFlee(MobContext c)
    {
        FleeState state = c.Get<FleeState>();
        if (c.Now < state.PanicUntil) return true;
        return below > 0 && c.Health < below && !state.FledThisFight && c.Target is not null;
    }

    private static Position ThreatPosition(MobContext c)
    {
        FleeState state = c.Get<FleeState>();
        Character? from = c.Now < state.PanicUntil ? state.PanicFrom : c.Target?.Character;
        if (from is not null && c.Find(from) is { } threat) return threat.Position;
        if (c.Memory.Recall(from) is { } known) return known.LastSeen;
        return c.Target?.Position ?? c.Mob.Home;
    }
}
