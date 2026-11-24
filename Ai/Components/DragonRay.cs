namespace BotArena.Ai.Components;

public sealed class DragonRay(Distance range, TimeSpan cooldown) : MobComponent
{
    public override string Name => $"promień śmierci {range.Centimeters / 100:0} m";

    public override IEnumerable<Branch> Branches()
    {
        yield return new Branch(Priority.Combat, new Sequence<MobContext>("promień",
            new Condition<MobContext>("wróg w zasięgu?", c => c.Target is { } target && InRange(c, target)),
            new Condition<MobContext>("gotowy?", c => c.Now >= c.Get<CombatState>().NextAttack),
            new Do<MobContext>("strzela promieniem", c =>
            {
                c.Get<CombatState>().NextAttack = c.Now + cooldown;
                c.Mob.LastCombat = c.Now;
                c.Director.RayFire(c.Mob, c.Target!.Value.Character, c.Now);
                return NodeStatus.Success;
            })));
    }

    public override void React(MobContext c)
    {
        Combatant? best = null;
        float nearest = float.MaxValue;
        foreach (Combatant who in c.Mobs.Concat(c.Players))
        {
            if (who.Character is not { IsValid: true, IsDead: false } || !Enemy(c, who) || !InRange(c, who)) continue;
            float distance = Flat(c.Position, who.Position);
            if (distance < nearest) { nearest = distance; best = who; }
        }
        c.Target = best;
        c.Mob.Target = best?.Character;
    }

    private bool InRange(MobContext c, Combatant who) => Flat(c.Position, who.Position) <= range.Centimeters;

    private static bool Enemy(MobContext c, Combatant who) => who.Mob is { } mob
        ? mob.Champion && Factions.Hostile(c.Kind.Faction, mob.Kind.Faction)
        : Teams.IsEnemy(c.Kind.Faction, who.Character);

    private static float Flat(Position a, Position b)
    {
        float dx = a.X - b.X, dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}
