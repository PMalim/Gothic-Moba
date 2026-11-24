namespace BotArena.Ai.Components;

public sealed class TerritoryState
{
    public bool Returning;
}

public sealed class Territory(Distance leash, TimeSpan? grace = null, bool committed = false) : MobComponent
{
    private static readonly TimeSpan IgnoreOnReturn = TimeSpan.FromSeconds(8);
    private readonly TimeSpan grace = grace ?? TimeSpan.FromSeconds(6);
    public override string Name => $"teren {leash.Centimeters / 100:0} m";
    public Distance Leash => leash;

    public override IEnumerable<Branch> Branches()
    {
        yield return new Branch(Priority.Leash, new Sequence<MobContext>("powrót",
            new Condition<MobContext>("za daleko od domu?", c => c.Get<TerritoryState>().Returning
                || (c.FromHome > leash && !c.Mob.HurtWithin(grace, c.Now))),
            new Do<MobContext>("porzuca pościg", c =>
            {
                c.Get<TerritoryState>().Returning = true;
                foreach (KnownCharacter known in c.Memory.Known.ToArray()) c.Memory.Ignore(known.Character, c.Now + IgnoreOnReturn);
                c.Target = null;
                c.Mob.Target = null;
                c.Mob.Quarry = null;
                return NodeStatus.Success;
            }),
            new Selector<MobContext>("droga do domu",
                new NpcActionLeaf("biegnie do domu", c =>
                    {
                        c.Mob.SetFocus(c.Mob.Home, FocusKind.Home);
                        return c.Commands.MoveTo(c.Npc, c.Mob.Home, new Distance(220), walk: false, TimeSpan.FromSeconds(8));
                    },
                    TimeSpan.FromSeconds(9)),
                new NpcActionLeaf("wraca trasą", c =>
                    {
                        c.Mob.SetFocus(c.Mob.Home, FocusKind.Home);
                        return c.Commands.PathTo(c.Npc, null, c.Mob.Home, new Distance(220), walk: false, TimeSpan.FromSeconds(30));
                    },
                    TimeSpan.FromSeconds(32)),
                new Do<MobContext>("utknął w drodze do domu", c => { c.Get<TerritoryState>().Returning = false; return NodeStatus.Failure; })),
            new Do<MobContext>("w domu", c => { c.Get<TerritoryState>().Returning = false; return NodeStatus.Success; })));
    }

    public override void Damaged(MobHit hit)
    {
        if (!committed) hit.Mob.Memory.Get<TerritoryState>().Returning = false;
    }
}
