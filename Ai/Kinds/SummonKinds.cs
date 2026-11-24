using BotArena.GameData;
using BotArena.Heroes;

namespace BotArena.Ai.Kinds;

public static class SummonKinds
{
    private static readonly MobBehaviour Pet = MobBehaviours.Summoned();
    private static readonly MobBehaviour CopyBehaviour = MobBehaviours.DecoyCopy();

    public static MobKind Of(AbilityDef ability, Faction team)
    {
        string suffix = team == Faction.Blue ? "n" : "c";
        if (ability.Key == "golem")
        {
            return new MobKind
            {
                Key = $"przywolany-golem-{suffix}", DisplayName = "Golem (przywołany)", Instance = GameNpcs.STONEGOLEM, Faction = team, Humanoid = false,
                Behaviour = Pet, Health = ability.Heal, Damage = ability.Damage, Strength = 80, Skill = 60, MeleeMode = WeaponMode.Fists,
                SightRange = new(1600), AttackRange = new(230), AttackCooldown = TimeSpan.FromMilliseconds(2200), Combo = 0,
                Attacks = [MeleeAttack.Forward],
            };
        }
        return new MobKind
        {
            Key = $"przywolany-szkielet-{suffix}", DisplayName = "Szkielet (przywołany)", Instance = GameNpcs.SKELETON, Faction = team, Humanoid = true,
            Behaviour = Pet, Health = ability.Heal, Damage = ability.Damage, Strength = 40, Skill = 40,
            MeleeWeapon = GameItems.ITMW_1H_MIL_SWORD, MeleeMode = WeaponMode.OneHandedSword,
            SightRange = new(1600), AttackRange = new(190), AttackCooldown = TimeSpan.FromMilliseconds(1500),
            Attacks = [MeleeAttack.Forward, MeleeAttack.Left, MeleeAttack.Right],
        };
    }

    private static readonly MobBehaviour ServantBehaviour = MobBehaviours.Summoned();

    public static MobKind ServantOf(HeroDef victim, Faction team, GothicOnline.Api.PlayerVisual? look, int health, int damage)
    {
        bool twoHanded = victim.WeaponClass == HeroWeapon.TwoHanded;
        var weapon = victim.WeaponClass == HeroWeapon.Bow ? GameItems.ITMW_SHORTSWORD1 : victim.Weapon;
        return new MobKind
        {
            Key = $"sluga-{victim.Key}-{(team == Faction.Blue ? "n" : "c")}", DisplayName = $"{victim.Name} (sługa)", Instance = GameNpcs.BDT_1013_BANDIT_L, Faction = team, Humanoid = true,
            Behaviour = ServantBehaviour, Health = health, Damage = damage, Strength = victim.Strength, Dexterity = victim.Dexterity, Skill = 60,
            Armor = victim.Armor, Look = look ?? victim.Look, MeleeWeapon = weapon, Mana = victim.WeaponMana ?? 100,
            MeleeMode = weapon is null ? WeaponMode.Fists : twoHanded ? WeaponMode.TwoHandedSword : WeaponMode.OneHandedSword,
            SightRange = new(1600), AttackRange = new(twoHanded ? 230 : 190), AttackCooldown = TimeSpan.FromMilliseconds(twoHanded ? 1600 : 1200),
            Attacks = [MeleeAttack.Forward, MeleeAttack.Left, MeleeAttack.Right],
        };
    }

    public static MobKind CopyOf(HeroDef hero, Faction team, GothicOnline.Api.PlayerVisual look, GothicOnline.Api.Scale3 scale) => new()
    {
        Key = $"kopia-{hero.Key}-{(team == Faction.Blue ? "n" : "c")}", DisplayName = hero.Name, Instance = GameNpcs.BDT_1013_BANDIT_L, Faction = team, Humanoid = true,
        Behaviour = CopyBehaviour, Health = 150, Strength = hero.Strength, Dexterity = hero.Dexterity,
        Armor = hero.Armor, Look = look, MeleeWeapon = hero.Weapon, Rune = hero.Rune, Mana = 100,
        MeleeMode = hero.Weapon is null ? WeaponMode.Fists : hero.WeaponClass == HeroWeapon.TwoHanded ? WeaponMode.TwoHandedSword : WeaponMode.OneHandedSword,
        Scale = scale.X,
    };
}
