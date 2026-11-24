namespace BotArena.Ai.Components;

public sealed class CampReset : MobComponent
{
    private sealed class ResetState
    {
        public TimeSpan NextHeal;
    }

    public override string Name => $"reset w obozie ({JungleSettings.ReturnRegen * 100:0}%/s w drodze)";

    public override void Sense(MobContext c)
    {
        if (!c.Get<TerritoryState>().Returning) return;
        ResetState state = c.Get<ResetState>();
        if (c.State.Health >= c.State.MaxHealth || c.Now < state.NextHeal) return;
        state.NextHeal = c.Now + TimeSpan.FromMilliseconds(500);
        c.Director.Heal(c.Mob, Math.Max(1, (int)(c.Mob.MaxHealth * JungleSettings.ReturnRegen * 0.5f)));
    }

    public override void Calm(MobContext c)
    {
        ResetState state = c.Get<ResetState>();
        if (c.FromHome.Centimeters > 400f || c.State.Health >= c.State.MaxHealth || c.Now < state.NextHeal) return;
        state.NextHeal = c.Now + TimeSpan.FromSeconds(1);
        c.Director.Heal(c.Mob, Math.Max(1, (int)(c.Mob.MaxHealth * JungleSettings.ResetRegen)));
    }
}
