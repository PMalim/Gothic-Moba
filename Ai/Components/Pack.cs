namespace BotArena.Ai.Components;

public sealed class Pack(bool flank = true, bool hunts = true, bool leader = false) : MobComponent
{
    private static readonly int[] FlankAngles = [0, 50, -50, 95, -95, 140, -140];
    public override string Name => leader ? "przywódca stada" : flank ? "stado (okrąża)" : "stado";
    public bool Leader => leader;

    public override void React(MobContext c)
    {
        c.Mob.FlankAngle = flank && c.Target is { } target ? FlankAngleFor(c.Mob, target.Character, c.Mobs) : 0;
        if (!hunts || c.Target is not { } prey) return;
        foreach (Mob mate in c.Director.CampMates(c.Mob, ArenaSettings.AlertRange))
        {
            if (mate.Target is not null || mate.Behaviour.Get<Perception>() is not { Aggressive: true }) continue;
            if (mate.Memory.IsIgnored(prey.Character, c.Now) || mate.Memory.Recall(prey.Character) is { Visible: true }) continue;
            mate.Memory.See(prey.Character, prey.Position, c.Now);
            mate.Quarry ??= prey.Character;
        }
    }

    public override void Damaged(MobHit hit)
    {
        foreach (Mob mate in hit.Director.CampMates(hit.Mob, ArenaSettings.AlertRange))
        {
            KnownCharacter known = hit.AttackerPosition is { } at ? mate.Memory.See(hit.Attacker, at, hit.Now) : mate.Memory.Know(hit.Attacker);
            known.Threat += 25;
            mate.Memory.StopIgnoring(hit.Attacker);
            hit.Director.Wake(mate);
        }
    }

    public static int FlankAngleFor(Mob mob, Character target, IReadOnlyList<Combatant> mobs)
    {
        int slot = 0;
        foreach (Combatant other in mobs)
            if (other.Mob is { } ally && !ReferenceEquals(ally, mob) && ally.Number < mob.Number && ReferenceEquals(ally.Target, target)
                && (mob.Camp is not null ? ally.Camp == mob.Camp : ally.Camp is null && ally.Kind == mob.Kind))
                slot++;
        return FlankAngles[Math.Min(slot, FlankAngles.Length - 1)];
    }
}
