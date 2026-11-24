using BotArena.Ai.Kinds;
using BotArena.Matches;
using GothicOnline.Api;

namespace BotArena.Heroes;

public sealed partial class AbilityService
{
    public const string SwordVisual = "ItMw_BeliarWeapon_2H.3DS";

    public const string HandsFx = "spellFX_RavenHands";

    private sealed record MightHit(TimeSpan Due, Match Match, Player Caster, Faction Team, Position At, AbilityDef Ability);
    private readonly List<MightHit> mights = [];

    private bool BeliarTongue(Match match, Player player, AbilityDef ability, Faction team, Position at, float dx, float dz, TimeSpan now)
    {
        var spot = new Position(at.X + dx * ability.Range, at.Y, at.Z + dz * ability.Range);
        int delay = (int)(ability.Seconds * 1000);
        matches.Send(match, new AbilityGroundMessage(MobCommands.IdOf(player), ability.Fx, spot, delay + 600));
        landings.Add(new Landing(now + TimeSpan.FromMilliseconds(delay), match, player, team, spot, ability.Radius, ability.StunSeconds, HandsFx));
        Touch(player, now);
        return true;
    }

    private bool Flank(Match match, MobDirector director, Player player, AbilityDef ability, Faction team, Position at, TimeSpan now)
    {
        Target? target = Enemies(match, director, team)
            .Where(t => t.Mob is not { Kind.Immobile: true } && Distance(at, t.At) <= ability.Range)
            .OrderBy(t => t.Mob is null || t.Mob.Champion ? 0 : 1).ThenBy(t => Distance(at, t.At)).Cast<Target?>().FirstOrDefault();
        if (target is not { } victim) { Notice?.Invoke(player, "Brak wroga w zasięgu."); return false; }
        float dx = victim.At.X - at.X, dz = victim.At.Z - at.Z, flat = MathF.Max(1f, MathF.Sqrt(dx * dx + dz * dz));
        var land = new Position(victim.At.X + dx / flat * 130f, victim.At.Y, victim.At.Z + dz / flat * 130f);
        Fx(match, player, null, 1, ability.Fx);
        server.Network.Send(player, new AbilityDashMessage(land));
        Touch(player, now);
        return true;
    }

    private bool BeliarMight(Match match, Player player, AbilityDef ability, Faction team, Position to, TimeSpan now)
    {
        Position from = player.Position;
        float dx = to.X - from.X, dz = to.Z - from.Z, flat = MathF.Max(1f, MathF.Sqrt(dx * dx + dz * dz));
        var land = new Position(to.X - dx / flat * 150f, to.Y, to.Z - dz / flat * 150f);
        int millis = (int)(ability.Seconds * 1000);
        int impact = (int)(millis * HeroBuffs.ExecuteImpact);
        server.Network.Send(player, new AbilityMotionMessage(HeroBuffs.ExecuteAnimation, millis, 1, land));
        matches.Send(match, new AbilitySwordMessage(MobCommands.IdOf(player), to, Math.Max(300, impact - 60), SwordVisual, ability.Fx));
        mights.Add(new MightHit(now + TimeSpan.FromMilliseconds(impact), match, player, team, to, ability));
        Touch(player, now);
        return true;
    }

    private void MightImpact(MightHit hit, TimeSpan now)
    {
        if (hit.Match.Director is not { } director || hit.Match.State != MatchState.Running || !hit.Caster.IsValid || hit.Caster.IsDead) return;
        foreach (Target t in Enemies(hit.Match, director, hit.Team).Where(t => t.Mob is not { Kind.Immobile: true } && Flat(hit.At, t.At) <= hit.Ability.Radius).ToArray())
            Hurt(director, hit.Caster, t, hit.Ability, now);
    }

    private void Servant(Match match, Player killer, string victimHero, PlayerVisual? look, Position at)
    {
        if (!killer.IsValid || match.State != MatchState.Running || match.Director is not { } director || HeroCatalog.Find(victimHero) is not { } victim) return;
        if (PassiveOf(killer, AbilityKind.Servant) is not { } servant || !match.Players.TryGetValue(killer.Id, out MatchPlayer? mp)) return;
        AbilityDef scaled = StatsOf(killer) is { } own
            ? servant with { Damage = HeroResources.AbilityDamage(servant, own), Heal = HeroResources.AbilityHeal(servant, own) } : servant;
        MobKind kind = SummonKinds.ServantOf(victim, mp.Team, look, scaled.Heal, scaled.Damage);
        Mob pet = director.SpawnCompanion(kind, killer, at, server.Clock.Elapsed, TimeSpan.FromSeconds(servant.Seconds), limit: 1);
        if (pet.Npc is null) return;
        Fx(match, killer, null, 1, servant.Fx);
        Notice?.Invoke(killer, $"Sługa: {victim.Name} walczy dla ciebie przez {servant.Seconds:0.#} s.");
    }
}
