namespace BotArena.Ai.Components;

public sealed class Companion : MobComponent
{
    public const float FollowRange = 3000f;
    private const float StayNear = 450f;

    public override string Name => "pomocnik";

    public override void Sense(MobContext c)
    {
        if (c.Mob.Owner is { IsValid: true, IsDead: false }) return;
        if (MasterNpc(c.Mob) is not null) return;
        c.Director.Retire(c.Mob, "pan zginął albo odszedł");
    }

    private static Npc? MasterNpc(Mob mob) =>
        mob.Master is { State: not MobState.Dead, Npc: { IsValid: true, IsDead: false } npc } ? npc : null;

    private static Position? MasterPosition(Mob mob) =>
        mob.Owner is { IsValid: true } owner ? owner.Position : MasterNpc(mob) is not null ? mob.Master!.LastPosition : null;

    public override void React(MobContext c)
    {
        Character? focus;
        if (c.Mob.Owner is { IsValid: true } owner)
            try { focus = owner.Focus; } catch (Exception) { return; }
        else if (MasterNpc(c.Mob) is not null) focus = c.Mob.Master!.Target;
        else return;
        if (focus is not { IsValid: true, IsDead: false } || c.Find(focus) is not { } target || !IsEnemy(c, target)) return;
        if (c.Position.DistanceTo(target.Position).Centimeters > FollowRange) return;
        c.Memory.See(target.Character, target.Position, c.Now);
        c.Target = target;
        c.Mob.Target = target.Character;
        c.Mob.Quarry = target.Character;
    }

    private static bool IsEnemy(MobContext c, Combatant who) =>
        who.Mob is { } mob
            ? !ReferenceEquals(mob, c.Mob) && Factions.Hostile(c.Kind.Faction, mob.Kind.Faction)
            : Teams.IsEnemy(c.Kind.Faction, who.Character);

    public override IEnumerable<Node<MobContext>> Idle()
    {
        yield return new Sequence<MobContext>("za panem",
            new Condition<MobContext>("daleko od pana?", c => MasterPosition(c.Mob) is { } master && c.Position.DistanceTo(master).Centimeters > StayNear * 1.5f),
            new NpcActionLeaf("idzie za panem", c =>
                {
                    Position at = MasterPosition(c.Mob) ?? c.Position;
                    c.Mob.SetFocus(at, FocusKind.Home);
                    return c.Commands.MoveTo(c.Npc, at, new Distance(StayNear), walk: false, TimeSpan.FromSeconds(4));
                },
                TimeSpan.FromSeconds(5)));
    }
}

public sealed class Decoy : MobComponent
{
    public override string Name => "kopia";

    public override IEnumerable<Branch> Branches()
    {
        yield return new Branch(Priority.Lane, new Sequence<MobContext>("kopia biegnie",
            new Condition<MobContext>("ma dokąd?", c => c.Mob.RunTo is not null),
            new NpcActionLeaf("biegnie jak oryginał", c =>
                {
                    Position to = c.Mob.RunTo!.Value;
                    c.Mob.SetFocus(to, FocusKind.Wander);
                    return c.Commands.MoveTo(c.Npc, to, new Distance(80), walk: false, TimeSpan.FromSeconds(4));
                },
                TimeSpan.FromSeconds(5))));
    }
}
