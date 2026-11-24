using BotArena.Matches;
using GothicOnline.Api;

namespace BotArena.Heroes;

public sealed partial class AbilityService
{
    private sealed class Burn(Player source, Character victim, MobDirector director, Match match)
    {
        public Player Source { get; } = source;
        public Character Victim { get; } = victim;
        public MobDirector Director { get; } = director;
        public Match Match { get; } = match;
        public TimeSpan Until, NextTick;
    }

    private readonly List<Burn> burns = [];

    private bool HasRedBuff(Player player) =>
        matches.Of(player) is { } match && match.Players.TryGetValue(player.Id, out MatchPlayer? mp)
        && mp.Elixirs.TryGetValue(JungleSettings.RedKey, out long until) && until > Environment.TickCount64;

    private void TryIgnite(Player source, Character victim, MobDirector director)
    {
        if (!HasRedBuff(source) || matches.Of(source) is not { } match || !victim.IsValid || victim.IsDead || ReferenceEquals(source, victim)) return;
        if (director.MobOf(victim) is { Kind.Immobile: true }) return;
        TimeSpan now = server.Clock.Elapsed;
        Burn? burn = burns.FirstOrDefault(b => ReferenceEquals(b.Victim, victim) && b.Source.Id == source.Id);
        if (burn is null)
        {
            burn = new Burn(source, victim, director, match) { NextTick = now + TimeSpan.FromSeconds(1) };
            burns.Add(burn);
            Fx(match, source, victim, 2, JungleSettings.BurnFx);
        }
        burn.Until = now + TimeSpan.FromSeconds(JungleSettings.BurnSeconds);
    }

    private void UpdateBurns(TimeSpan now)
    {
        for (int i = burns.Count - 1; i >= 0; i--)
        {
            Burn burn = burns[i];
            bool over = !burn.Victim.IsValid || burn.Victim.IsDead || !burn.Source.IsValid || now >= burn.Until;
            if (over) { burns.RemoveAt(i); continue; }
            if (now < burn.NextTick) continue;
            burn.NextTick += TimeSpan.FromSeconds(1);
            Fx(burn.Match, burn.Source, burn.Victim, 2, JungleSettings.BurnFx);
            if (burn.Director.MobOf(burn.Victim) is not null) burn.Director.ScheduleDamage(burn.Victim, JungleSettings.BurnDamage, now, burn.Source, magic: true);
            else pending.Add(new Pending(now, burn.Victim, JungleSettings.BurnDamage, burn.Source, Magic: true));
        }
    }
}
