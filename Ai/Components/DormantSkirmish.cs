namespace BotArena.Ai.Components;

public static class DormantSkirmish
{
    public static Combatant? NearestEnemy(MobContext c)
    {
        Combatant? best = null, structure = null;
        float nearest = LaneSettings.ContactRange.Centimeters, structureDistance = float.MaxValue;
        foreach (Combatant other in c.Mobs)
        {
            if (other.Mob is not { Lane: not null } mob || ReferenceEquals(mob, c.Mob) || !Factions.Hostile(c.Kind.Faction, mob.Kind.Faction)) continue;
            if (other.Character is not { IsValid: true, IsDead: false }) continue;
            if (mob.Kind.Immobile && c.Director.IsProtected(mob)) continue;
            float distance = Flat(c.Position, other.Position) - mob.Kind.Radius;
            if (distance < nearest) { nearest = distance; best = other; }
            else if (mob.Kind.Immobile && !c.Kind.Immobile && ReferenceEquals(mob.Lane, c.Mob.Lane)
                && Flat(c.Position, other.Position) <= LaneSettings.StructureNotice.Centimeters
                && (structure is null || distance < structureDistance)) { structure = other; structureDistance = distance; }
        }
        return best ?? structure;
    }

    public static float Reach(MobContext c) =>
        c.Behaviour.Get<BoltCombat>()?.Range.Centimeters
        ?? c.Behaviour.Get<CastCombat>()?.Range.Centimeters
        ?? c.Behaviour.Get<RangedCombat>()?.Preferred.Centimeters
        ?? c.Kind.AttackRange.Centimeters;

    public static bool Engage(MobContext c, Combatant enemy, float step)
    {
        float radius = enemy.Mob?.Kind.Radius ?? 0f;
        float reach = Reach(c);
        float distance = Flat(c.Position, enemy.Position) - radius;
        c.Mob.Target = enemy.Character;
        c.Mob.LastCombat = c.Now;
        if (distance > reach)
        {
            float move = MathF.Min(step, distance - reach * 0.8f);
            if (move > 0f)
            {
                float dx = enemy.Position.X - c.Position.X, dz = enemy.Position.Z - c.Position.Z;
                float length = MathF.Max(1f, MathF.Sqrt(dx * dx + dz * dz)), k = move / length;
                var at = new Position(c.Position.X + dx * k, c.Position.Y + (enemy.Position.Y - c.Position.Y) * MathF.Min(1f, k), c.Position.Z + dz * k);
                c.Npc.Position = at;
                c.Mob.LastPosition = at;
            }
            c.Mob.Note = $"bez hosta: idzie do {enemy.Name}";
            return true;
        }
        CombatState state = c.Get<CombatState>();
        c.Mob.Note = $"bez hosta: walczy z {enemy.Name}";
        if (c.Now < state.NextAttack || (c.Kind.Damage is null && c.Kind.Role is null)) return true;
        int damage = c.Director.AttackDamage(c.Mob, enemy.Character);
        BoltCombat? bolt = c.Behaviour.Get<BoltCombat>();
        CastCombat? cast = c.Behaviour.Get<CastCombat>();
        TimeSpan cooldown = bolt?.Cooldown ?? cast?.Cooldown ?? c.Kind.AttackCooldown;
        state.NextAttack = c.Now + c.Jitter(cooldown);
        TimeSpan delay = bolt is null
            ? (cast is null ? Combat.MeleeHitDelay : TimeSpan.FromMilliseconds(1100))
            : LaneSettings.BoltCastDelay + TimeSpan.FromSeconds(Math.Clamp((distance + radius) / bolt.Speed, 0.1f, 1.5f));
        c.Commands.Face(c.Npc, c.Position, enemy.Position);
        c.Director.ScheduleDamage(enemy.Character, damage, c.Now + delay, c.Npc, magic: cast is not null);
        return true;
    }

    private static float Flat(Position a, Position b)
    {
        float dx = a.X - b.X, dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}
