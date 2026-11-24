using BotArena.Matches;

namespace BotArena.Heroes;

public sealed partial class AbilityService
{
    private void RankMessage(Player player, AbilityRankMessage message) => RankUp(player, message.Slot);

    public bool RankUp(Player player, int slot)
    {
        if (matches.Of(player) is not { State: MatchState.Running } match || !match.Players.TryGetValue(player.Id, out MatchPlayer? mp)) return false;
        if (HeroCatalog.Find(mp.Hero) is not { } hero || slot < 0 || slot >= hero.Abilities.Length || slot >= mp.Ranks.Length) return false;
        AbilityDef ability = hero.Abilities[slot];
        if (!HeroResources.Rankable(hero, slot)) return false;
        int level = mp.Level, rank = mp.Ranks[slot];
        if (rank >= HeroResources.MaxRank) { Notice?.Invoke(player, $"{ability.Name} ma już najwyższy stopień."); return false; }
        if (HeroResources.SkillPoints(level, mp.Ranks) <= 0) { Notice?.Invoke(player, "Brak punktów umiejętności (jeden za każdy poziom)."); return false; }
        int need = HeroResources.RequiredLevel(hero, slot, rank + 1);
        if (level < need) { Notice?.Invoke(player, $"{ability.Name}: stopień {rank + 1} dopiero od poziomu {need}."); return false; }
        mp.Ranks[slot] = rank + 1;
        Notice?.Invoke(player, rank == 0 ? $"Nauczono: {ability.Name}." : $"{ability.Name}: stopień {rank + 1} z {HeroResources.MaxRank}.");
        matches.RefreshStats(match, mp);
        return true;
    }
}
