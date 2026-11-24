using BotArena.Heroes;

namespace BotArena.Ai.Behaviours;

public static partial class MobBehaviours
{
    public static MobBehaviour LaneChampion(HeroDef hero) => new($"bohater ({hero.Name})",
        new Perception(pursuit: 1.5f, players: true, mobRange: ChampionSightMobs, playerRange: ChampionSightPlayers),
        new TargetMemory(TimeSpan.FromSeconds(5)),
        new ChampionTactics(),
        new ChampionSkills(hero),
        hero.Rune is not null ? new CastCombat(new Distance(LaneSettings.MaxAttackRange.Centimeters - 100), CasterCooldown, LaneSettings.SpellSlot, 10)
            : hero.WeaponClass == HeroWeapon.Bow ? new RangedCombat(new Distance(LaneSettings.MaxAttackRange.Centimeters - 100)) : new MeleeCombat(circles: true),
        new ChampionStance());

    private static readonly TimeSpan CasterCooldown = TimeSpan.FromMilliseconds(1800);

    private const float ChampionSightMobs = 1400;
    private const float ChampionSightPlayers = 1600;
}
