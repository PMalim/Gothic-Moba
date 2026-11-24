using BotArena.GameData;
using BotArena.Heroes;

namespace BotArena.Ai.Kinds;

public static class ChampionKinds
{
    public static readonly TimeSpan RespawnDelay = TimeSpan.FromSeconds(15);

    private static readonly Dictionary<(string Hero, Faction Team), MobKind> cache = [];

    public static MobKind Of(HeroDef hero, Faction team)
    {
        if (cache.TryGetValue((hero.Key, team), out MobKind? known)) return known;
        bool bow = hero.WeaponClass == HeroWeapon.Bow;
        bool twoHanded = hero.WeaponClass == HeroWeapon.TwoHanded;
        var kind = new MobKind
        {
            Key = $"bohater-{hero.Key}-{(team == Faction.Blue ? "n" : "c")}",
            DisplayName = $"{hero.Name} (bot)",
            Instance = hero.Instance ?? GameNpcs.BDT_1013_BANDIT_L,
            Faction = team,
            Humanoid = hero.Instance is null,
            Behaviour = MobBehaviours.LaneChampion(hero),
            Health = hero.Health,
            Strength = hero.Strength,
            Dexterity = hero.Dexterity,
            Skill = 70,
            Armor = hero.Armor,
            Look = hero.Look,
            Damage = HeroResources.BasicAttackOf(hero, HeroResources.StatsOf(hero, 1)).Damage,
            MeleeWeapon = bow ? GameItems.ITMW_SHORTSWORD1 : hero.Weapon,
            MeleeMode = hero.Weapon is null && !bow ? WeaponMode.Fists : twoHanded ? WeaponMode.TwoHandedSword : WeaponMode.OneHandedSword,
            RangedWeapon = bow ? hero.Weapon : null,
            Rune = hero.Rune,
            Mana = hero.Rune is null ? hero.WeaponMana ?? 100 : hero.GameMana,
            Ammo = bow ? hero.Ammo ?? GameItems.ITRW_ARROW : null,
            SightRange = new(1800),
            AttackRange = new(twoHanded ? 230 : 190),
            AttackCooldown = TimeSpan.FromMilliseconds(twoHanded ? 1600 : bow ? 2000 : 1200),
            Attacks = [MeleeAttack.Forward, MeleeAttack.Left, MeleeAttack.Right],
            RespawnDelay = RespawnDelay,
        };
        cache[(hero.Key, team)] = kind;
        return kind;
    }
}
