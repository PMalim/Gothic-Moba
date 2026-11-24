namespace BotArena.Ai.Components;

public sealed class GrudgeState : IPersistentState
{
    public Dictionary<string, DateTime> Players { get; set; } = [];
}

public sealed class Grudge(TimeSpan duration) : MobComponent
{
    public override string Name => $"uraza {duration.TotalMinutes:0} min";

    public override void Sense(MobContext c)
    {
        GrudgeState state = c.Get<GrudgeState>();
        if (state.Players.Count == 0) return;
        DateTime now = DateTime.UtcNow;
        foreach (var (name, until) in state.Players.ToArray()) if (now >= until) state.Players.Remove(name);
        foreach (Combatant player in c.Players)
        {
            if (!state.Players.ContainsKey(((Player)player.Character).Name)) continue;
            if (c.Memory.IsIgnored(player.Character, c.Now)) continue;
            float range = c.Kind.SightRange.Centimeters * (c.Memory.Recall(player.Character) is null ? 1 : 2);
            if (c.Position.DistanceTo(player.Position).Centimeters > range) continue;
            KnownCharacter known = c.Memory.See(player.Character, player.Position, c.Now);
            known.Grudge = true;
            known.Threat = MathF.Max(known.Threat, 30);
        }
    }

    public override void Damaged(MobHit hit) => Remember(hit.Mob, hit.Attacker);
    public override void Died(Mob mob, Character? killer) { if (killer is not null) Remember(mob, killer); }

    private void Remember(Mob mob, Character who)
    {
        if (who is Player { IsValid: true } player) mob.Memory.Get<GrudgeState>().Players[player.Name] = DateTime.UtcNow + duration;
    }
}
