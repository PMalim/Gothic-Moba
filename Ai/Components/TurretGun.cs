namespace BotArena.Ai.Components;

public sealed class TurretState
{
    public Character? Locked;
    public Character? Last;
    public int Streak;
    public TimeSpan LastShot;
}

public sealed class TurretGun(Distance range, TimeSpan cooldown) : MobComponent
{
    private static readonly TimeSpan DefendWindow = TimeSpan.FromSeconds(1.5);
    public override string Name => $"działo {range.Centimeters / 100:0} m";
    public Distance Range => range;

    public override IEnumerable<Branch> Branches()
    {
        yield return new Branch(Priority.Combat, new Sequence<MobContext>("strzał",
            new Condition<MobContext>("ma cel w zasięgu?", c => c.Target is { } target && InRange(c, target)),
            new Condition<MobContext>("gotowy do strzału?", c => c.Now >= c.Get<CombatState>().NextAttack),
            new Do<MobContext>("strzela laserem", c =>
            {
                c.Get<CombatState>().NextAttack = c.Now + c.Jitter(cooldown);
                c.Mob.LastCombat = c.Now;
                Combatant target = c.Target!.Value;
                TurretState state = c.Get<TurretState>();
                bool sameTarget = ReferenceEquals(state.Last, target.Character) && c.Now - state.LastShot <= LaneSettings.TurretStreakWindow;
                state.Streak = sameTarget ? state.Streak + 1 : 0;
                state.Last = target.Character;
                state.LastShot = c.Now;
                c.Director.TurretFire(c.Mob, target.Character, Damage(c, target, state.Streak), c.Now);
                return NodeStatus.Success;
            })));
    }

    private static int Damage(MobContext c, Combatant target, int streak)
    {
        if (target.Mob is { Champion: false, Kind.Immobile: false } minion)
            return (int)Math.Ceiling(minion.MaxHealth * MinionStats.TowerShotShare(minion.Kind.Role ?? MinionRole.Melee));
        float ad = MinionStats.TowerAd(c.Director.GameMinutes(c.Now));
        return (int)Math.Round(ad * (1f + MinionStats.TowerStackBonus * Math.Min(streak, MinionStats.TowerMaxStacks)));
    }

    public override void React(MobContext c)
    {
        TurretState state = c.Get<TurretState>();
        Combatant? target = Defend(c) ?? Keep(c, state.Locked) ?? Pick(c);
        state.Locked = target?.Character;
        c.Target = target;
        c.Mob.Target = target?.Character;
    }

    private bool InRange(MobContext c, Combatant who) =>
        Flat(c.Position, who.Position) - (who.Mob?.Kind.Radius ?? 0f) <= range.Centimeters;

    private static bool Alive(Combatant who) => who.Character is { IsValid: true, IsDead: false };

    private static bool Enemy(MobContext c, Combatant who) => who.Mob is { } mob
        ? mob.Lane is not null && !mob.Kind.Immobile && Factions.Hostile(c.Kind.Faction, mob.Kind.Faction)
        : Teams.IsEnemy(c.Kind.Faction, who.Character) && !Teams.Untargetable(who.Character);

    private static bool AllyChampion(MobContext c, Character who) => who switch
    {
        Player player => !Teams.IsEnemy(c.Kind.Faction, player),
        _ => c.Director.MobOf(who) is { Champion: true } mob && mob.Kind.Faction == c.Kind.Faction,
    };

    private Combatant? Defend(MobContext c)
    {
        foreach ((Character attacker, Character victim, TimeSpan _) in c.Director.RecentAggression(c.Now, DefendWindow))
        {
            if (!AllyChampion(c, victim) || c.Find(attacker) is not { } who || !Alive(who) || !Enemy(c, who) || !InRange(c, who)) continue;
            if (c.Find(victim) is { } hurt && !InRange(c, hurt)) continue;
            return who;
        }
        return null;
    }

    private Combatant? Keep(MobContext c, Character? locked)
    {
        if (locked is null || c.Find(locked) is not { } who || !Alive(who) || !Enemy(c, who) || !InRange(c, who)) return null;
        return who;
    }

    private Combatant? Pick(MobContext c)
    {
        Combatant? minion = null, champion = null;
        float nearestMinion = float.MaxValue, nearestChampion = float.MaxValue;
        foreach (Combatant who in c.Mobs.Concat(c.Players))
        {
            if (!Alive(who) || !Enemy(c, who) || !InRange(c, who)) continue;
            float distance = Flat(c.Position, who.Position);
            if (who.Mob is { Champion: false })
            {
                if (distance < nearestMinion) { nearestMinion = distance; minion = who; }
            }
            else if (distance < nearestChampion) { nearestChampion = distance; champion = who; }
        }
        return minion ?? champion;
    }

    private static float Flat(Position a, Position b)
    {
        float dx = a.X - b.X, dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}
