namespace BotArena.Ai.Components;

public sealed class CastState
{
    public bool Readied;
}

public sealed class CastCombat(Distance range, TimeSpan cooldown, int slot, int mana) : MobComponent
{
    private static readonly TimeSpan PutAwayAfter = TimeSpan.FromSeconds(3);
    public override string Name => $"czar z runy {range.Centimeters / 100:0} m";
    public Distance Range => range;
    public TimeSpan Cooldown => cooldown;

    public override void Sense(MobContext c)
    {
        CastState state = c.Get<CastState>();
        if (!state.Readied || c.Now - c.Mob.LastCombat <= PutAwayAfter) return;
        state.Readied = false;
        c.Commands.UnreadySpell(c.Npc);
    }

    public override IEnumerable<Branch> Branches()
    {
        yield return new Branch(Priority.Combat, Combat.Fight(_ => range,
            new Selector<MobContext>("walka czarem",
                new Sequence<MobContext>("rzut",
                    new Condition<MobContext>("w zasięgu rzutu?", c => c.TargetDistance <= range),
                    new Selector<MobContext>("rzuca albo celuje",
                        new Sequence<MobContext>("rzuca",
                            new Condition<MobContext>("gotowy do rzutu?", Combat.AttackReady),
                            new NpcActionLeaf("rzuca kulę ognia", c =>
                                {
                                    CastState state = c.Get<CastState>();
                                    if (!state.Readied)
                                    {
                                        c.Commands.ReadySpell(c.Npc, slot, mana);
                                        state.Readied = true;
                                    }
                                    return c.Commands.CastSpell(c.Npc, c.Target!.Value.Character);
                                },
                                TimeSpan.FromSeconds(5),
                                started: c =>
                                {
                                    Combat.AttackStarted(c, cooldown, TimeSpan.FromMilliseconds(900));
                                    if (LaneSettings.LogSpells)
                                        c.Director.Log($"[CZAR] {c.Mob.Label}: rzuca w {c.Target!.Value.Name}, {c.TargetDistance.Centimeters:0} cm, mana {c.Npc.Mana}, w kolejce {c.State.PendingActions}");
                                },
                                finished: (c, ok) =>
                                {
                                    if (LaneSettings.LogSpells)
                                        c.Director.Log($"[CZAR] {c.Mob.Label}: wynik {(ok ? "OK" : "PORAZKA")} po {c.ActionElapsed.TotalMilliseconds:0} ms");
                                })),
                        new Do<MobContext>("celuje", Combat.FaceTarget))),
                Combat.Approach("podchodzi", _ => new Distance(range.Centimeters * 0.85f)))));
    }
}
