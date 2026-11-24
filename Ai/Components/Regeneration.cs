namespace BotArena.Ai.Components;

public sealed class RegenerationState
{
    public TimeSpan NextRegen;
}

public sealed class Regeneration(float perSecond = 0.08f, TimeSpan? delay = null) : MobComponent
{
    private readonly TimeSpan delay = delay ?? TimeSpan.FromSeconds(6);
    public override string Name => $"regeneracja {perSecond * 100:0}%/s";

    public override void Calm(MobContext c)
    {
        RegenerationState state = c.Get<RegenerationState>();
        if (c.State.Health >= c.State.MaxHealth || c.Now - c.Mob.LastCombat < delay || c.Now < state.NextRegen) return;
        state.NextRegen = c.Now + TimeSpan.FromSeconds(1);
        int health = Math.Min(c.State.MaxHealth, c.State.Health + Math.Max(1, (int)(c.State.MaxHealth * perSecond)));
        if (health != c.State.Health) c.Npc.Health = health;
    }
}
