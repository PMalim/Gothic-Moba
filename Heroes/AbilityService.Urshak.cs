using BotArena.Matches;
using GothicOnline.Api;

namespace BotArena.Heroes;

public sealed partial class AbilityService
{

    private sealed class Scorch(Player source, Character victim, MobDirector director)
    {
        public Player Source { get; } = source;
        public Character Victim { get; } = victim;
        public MobDirector Director { get; } = director;
        public int PerSecond;
        public TimeSpan Until, NextTick;
    }

    private readonly List<Scorch> scorches = [];

    private bool Ignite(Match match, Player player, AbilityDef ability, TimeSpan now)
    {
        BuffState state = Buffs(player);
        StopAura(player, state.FireFx);
        state.FireCharges = HeroBuffs.IgniteCharges;
        state.FireUntil = now + TimeSpan.FromSeconds(ability.Seconds);
        state.FireDamage = DamageOf(player, ability);
        state.FireFx = ability.Fx;
        Aura(match, player, ability.Fx, ability.Seconds);
        Touch(player, now);
        PushBuffs(player);
        return true;
    }

    private void TryScorch(Player attacker, Character victim, MobDirector director, BuffState state, TimeSpan now)
    {
        if (state.FireCharges <= 0 || now >= state.FireUntil || !victim.IsValid || victim.IsDead || ReferenceEquals(attacker, victim)) return;
        if (director.MobOf(victim) is { Kind.Immobile: true }) return;
        state.FireCharges--;
        Scorch? scorch = scorches.FirstOrDefault(s => ReferenceEquals(s.Victim, victim) && s.Source.Id == attacker.Id);
        if (scorch is null) { scorch = new Scorch(attacker, victim, director) { NextTick = now + TimeSpan.FromSeconds(HeroBuffs.IgniteTick) }; scorches.Add(scorch); }
        scorch.PerSecond = Math.Max(scorch.PerSecond, state.FireDamage);
        scorch.Until = now + TimeSpan.FromSeconds(HeroBuffs.IgniteSeconds);
        if (matches.Of(attacker) is { } match) Fx(match, attacker, victim, 2, JungleSettings.BurnFx);
        if (state.FireCharges == 0) EndFire(attacker, state);
        PushBuffs(attacker);
    }

    private void EndFire(Player player, BuffState state)
    {
        StopAura(player, state.FireFx);
        state.FireFx = "";
        state.FireCharges = 0;
    }

    private void UpdateScorches(TimeSpan now)
    {
        for (int i = scorches.Count - 1; i >= 0; i--)
        {
            Scorch scorch = scorches[i];
            if (!scorch.Victim.IsValid || scorch.Victim.IsDead || !scorch.Source.IsValid || now >= scorch.Until) { scorches.RemoveAt(i); continue; }
            if (now < scorch.NextTick) continue;
            scorch.NextTick += TimeSpan.FromSeconds(HeroBuffs.IgniteTick);
            int amount = (int)Math.Round(scorch.PerSecond * HeroBuffs.IgniteTick);
            if (matches.Of(scorch.Source) is { } match) Fx(match, scorch.Source, scorch.Victim, 2, JungleSettings.BurnFx);
            if (amount <= 0) continue;
            if (scorch.Director.MobOf(scorch.Victim) is not null) scorch.Director.ScheduleDamage(scorch.Victim, amount, now, scorch.Source, magic: true);
            else pending.Add(new Pending(now, scorch.Victim, amount, scorch.Source, Magic: true));
        }
    }

    private void TribeMobKill(Player killer) => AddTribe(killer, HeroBuffs.TribeMobStacks);

    private void TribeChampionKill(Match match, Player killer, string victimHero, PlayerVisual? look, Position at) => AddTribe(killer, HeroBuffs.TribeChampionStacks);

    private void AddTribe(Player killer, int stacks)
    {
        if (!killer.IsValid || PassiveOf(killer, AbilityKind.TribeProgress) is null) return;
        BuffState state = Buffs(killer);
        state.TribeStacks = Math.Min(HeroBuffs.TribeMaxStacks, state.TribeStacks + stacks);
        PushBuffs(killer);
    }

    private bool TribeProgress(Match match, MobDirector director, Player player, AbilityDef ability, Faction team, TimeSpan now)
    {
        BuffState state = Buffs(player);
        Character? focus;
        try { focus = player.Focus; } catch (Exception) { focus = null; }
        if (focus is not { IsValid: true, IsDead: false } ally || ReferenceEquals(ally, player)) { Notice?.Invoke(player, "Postęp Plemienia: spójrz na sojusznika (focus), żeby go wskazać."); return false; }
        if (state.TribeStacks <= 0) { Notice?.Invoke(player, "Postęp Plemienia: nie masz stacków (zabijaj wrogów)."); return false; }
        if (Distance(player.Position, ally.Position) > ability.Range) { Notice?.Invoke(player, "Postęp Plemienia: podejdź do sojusznika."); return false; }
        float share = Math.Min(HeroBuffs.TribeMaxShare, DamageOf(player, ability));
        int xp = Math.Max(1, (int)Math.Round(state.TribeStacks * MatchService.MinionXp * share / 100f));
        string? name = matches.GiveXp(match, team, ally, xp);
        if (name is null) { Notice?.Invoke(player, "Postęp Plemienia działa tylko na sojuszniczego bohatera."); return false; }
        Notice?.Invoke(player, $"Postęp Plemienia: {name} dostaje {xp} XP ({state.TribeStacks} stacków, {share:0}%).");
        if (ally is Player friend) Notice?.Invoke(friend, $"Postęp Plemienia: +{xp} XP od {player.Name}.");
        Fx(match, player, ally, 2, ability.Fx);
        state.TribeStacks = 0;
        Touch(player, now);
        PushBuffs(player);
        return true;
    }

    private sealed class TotemPost(Match match, Player caster, Faction team, Position at, AbilityDef ability, TimeSpan until)
    {
        public Match Match { get; } = match;
        public Player Caster { get; } = caster;
        public Faction Team { get; } = team;
        public Position At { get; } = at;
        public AbilityDef Ability { get; } = ability;
        public TimeSpan Until { get; } = until;
        public TimeSpan NextShot;
    }

    private readonly List<TotemPost> totems = [];

    private bool Totem(Match match, Player player, AbilityDef ability, Faction team, Position at, float dx, float dz, TimeSpan now)
    {
        totems.RemoveAll(t => t.Caster.Id == player.Id);
        var spot = new Position(at.X + dx * HeroBuffs.TotemDistance, at.Y, at.Z + dz * HeroBuffs.TotemDistance);
        int millis = (int)(ability.Seconds * 1000);
        matches.Send(match, new AbilityTotemMessage(MobCommands.IdOf(player), spot, millis, HeroBuffs.TotemVisual, ability.Fx));
        totems.Add(new TotemPost(match, player, team, spot, ability, now + TimeSpan.FromMilliseconds(millis)) { NextShot = now + TimeSpan.FromMilliseconds(700) });
        Touch(player, now);
        return true;
    }

    private void UpdateTotems(TimeSpan now)
    {
        for (int i = totems.Count - 1; i >= 0; i--)
        {
            TotemPost totem = totems[i];
            if (now >= totem.Until || !totem.Caster.IsValid || totem.Match.State != MatchState.Running || totem.Match.Director is not { } director) { totems.RemoveAt(i); continue; }
            if (now < totem.NextShot) continue;
            totem.NextShot = now + TimeSpan.FromSeconds(HeroBuffs.TotemInterval);
            Target? target = Enemies(totem.Match, director, totem.Team)
                .Where(t => t.Mob is not { Kind.Immobile: true } && Distance(totem.At, t.At) <= totem.Ability.Range)
                .OrderBy(t => t.Mob is null || t.Mob.Champion ? 0 : 1).ThenBy(t => Distance(totem.At, t.At)).Cast<Target?>().FirstOrDefault();
            if (target is not { } hit) continue;
            Fx(totem.Match, totem.Caster, hit.Character, 2, HeroBuffs.TotemShotFx);
            Hurt(director, totem.Caster, hit, totem.Ability, now + TimeSpan.FromMilliseconds(250));
        }
    }

    private bool TribeGuard(Match match, MobDirector director, Player player, AbilityDef ability, Faction team, Position at, TimeSpan now)
    {
        float seconds = MathF.Min(HeroBuffs.GuardMaxSeconds, ability.Seconds + MathF.Min(0.5f, (StatsOf(player)?.Ap ?? 0) * 0.003f));
        TimeSpan until = now + TimeSpan.FromSeconds(seconds);
        int millis = (int)(seconds * 1000);
        int covered = 0;
        foreach (MatchPlayer member in match.Players.Values.Where(p => p.Team == team).ToArray())
        {
            if (server.Players.Find(member.Id) is not { IsValid: true, IsDead: false } friend || (friend.Id != player.Id && Distance(at, friend.Position) > ability.Radius)) continue;
            if (!(shields.TryGetValue(friend.Id, out var known) && known.Until > until && known.Reduction >= 1f)) shields[friend.Id] = (until, 1f);
            matches.Send(match, new AbilitySkullMessage(MobCommands.IdOf(friend), millis, HeroBuffs.GuardSkullVisual));
            Fx(match, friend, null, 1, ability.Fx);
            PushBuffs(friend);
            covered++;
        }
        foreach (Mob bot in director.Mobs.Where(m => m.Champion && m.Kind.Faction == team && m.Npc is { IsValid: true, IsDead: false } && m.State != MobState.Dead).ToArray())
        {
            if (Distance(at, bot.LastPosition) > ability.Radius) continue;
            director.Shield(bot, until, 1f);
            matches.Send(match, new AbilitySkullMessage(MobCommands.IdOf(bot.Npc!), millis, HeroBuffs.GuardSkullVisual));
            Fx(match, player, bot.Npc, 2, ability.Fx);
            covered++;
        }
        Notice?.Invoke(player, $"Plemię jest najważniejsze: {covered} bohaterów niewrażliwych przez {seconds:0.#} s.");
        Touch(player, now);
        return true;
    }

    private void UpdateUrshak(TimeSpan now)
    {
        UpdateScorches(now);
        UpdateTotems(now);
        foreach ((PlayerId id, BuffState state) in buffs.ToArray())
        {
            if (state.FireFx.Length == 0 || now < state.FireUntil || server.Players.Find(id) is not { IsValid: true } player) continue;
            EndFire(player, state);
            PushBuffs(player);
        }
    }

    private void ResetUrshak(Player player) => totems.RemoveAll(t => t.Caster.Id == player.Id);
}
