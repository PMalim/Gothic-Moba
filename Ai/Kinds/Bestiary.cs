using BotArena.GameData;

namespace BotArena.Ai.Kinds;

public static class Bestiary
{
    public static readonly MobKind Bandit = new()
    {
        Key = "bandyta", DisplayName = "Bandyta", Instance = GameNpcs.BDT_1013_BANDIT_L, Faction = Faction.Bandits, Humanoid = true,
        Behaviour = MobBehaviours.PackHunter(flee: 0.15f, remember: 10),
        Health = 180, Strength = 55, Skill = 40, Armor = GameItems.ITAR_BDT_M, MeleeWeapon = GameItems.ITMW_1H_VLK_SWORD,
        MeleeMode = WeaponMode.OneHandedSword, SightRange = new(1600), AttackRange = new(190),
        AttackCooldown = TimeSpan.FromMilliseconds(1200), Attacks = [MeleeAttack.Forward, MeleeAttack.Left, MeleeAttack.Right],
        RespawnDelay = TimeSpan.FromSeconds(20),
    };

    public static readonly MobKind BanditLeader = Bandit with
    {
        Key = "herszt", DisplayName = "Herszt bandytów", Instance = GameNpcs.BDT_10309_ADDON_RANGERBANDIT_L,
        Behaviour = MobBehaviours.PackHunter(leader: true, remember: 12),
        Health = 360, Strength = 90, Skill = 70, Armor = GameItems.ITAR_BDT_H, MeleeWeapon = GameItems.ITMW_ZWEIHAENDER1,
        MeleeMode = WeaponMode.TwoHandedSword, AttackRange = new(230), AttackCooldown = TimeSpan.FromMilliseconds(1600),
        RespawnDelay = TimeSpan.FromSeconds(45),
    };

    public static readonly MobKind BanditArcher = Bandit with
    {
        Key = "lucznik", DisplayName = "Łucznik", Behaviour = MobBehaviours.Archer(preferred: 1100, flee: 0.3f),
        Health = 140, Dexterity = 70, Skill = 60, Armor = GameItems.ITAR_LEATHER_L,
        MeleeWeapon = GameItems.ITMW_SHORTSWORD1, RangedWeapon = GameItems.ITRW_BOW_L_02, Ammo = GameItems.ITRW_ARROW,
        SightRange = new(2000), AttackCooldown = TimeSpan.FromMilliseconds(2200),
    };

    public static readonly MobKind Wolf = new()
    {
        Key = "wilk", DisplayName = "Wilk", Instance = GameNpcs.WOLF, Faction = Faction.Beasts,
        Behaviour = MobBehaviours.PackHunter(flee: 0.2f, remember: 14, wander: 500),
        Health = 110, Strength = 40, SightRange = new(1800), AttackRange = new(170), AttackCooldown = TimeSpan.FromMilliseconds(900),
        Combo = 0, RespawnDelay = TimeSpan.FromSeconds(18),
    };

    public static readonly MobKind Warg = Wolf with
    {
        Key = "warg", DisplayName = "Warg (przewodnik stada)", Instance = GameNpcs.WARG,
        Behaviour = MobBehaviours.PackHunter(leader: true, remember: 14, wander: 500),
        Health = 280, Strength = 75, AttackRange = new(200), AttackCooldown = TimeSpan.FromMilliseconds(1100),
        RespawnDelay = TimeSpan.FromSeconds(40),
    };

    public static readonly MobKind Scavenger = new()
    {
        Key = "scierwojad", DisplayName = "Ścierwojad", Instance = GameNpcs.SCAVENGER, Faction = Faction.Prey,
        Behaviour = MobBehaviours.Herd(flee: 0.35f, wander: 600),
        Health = 90, Strength = 30, SightRange = new(1200), AttackRange = new(180),
        AttackCooldown = TimeSpan.FromMilliseconds(1300), Combo = 0, RespawnDelay = TimeSpan.FromSeconds(15),
    };

    public static readonly MobKind Skeleton = new()
    {
        Key = "szkielet", DisplayName = "Szkielet", Instance = GameNpcs.SKELETON, Faction = Faction.Undead, Humanoid = true,
        Behaviour = MobBehaviours.Guard(),
        Health = 220, Strength = 60, Skill = 50, MeleeWeapon = GameItems.ITMW_1H_MIL_SWORD, MeleeMode = WeaponMode.OneHandedSword,
        SightRange = new(1400), AttackRange = new(190), AttackCooldown = TimeSpan.FromMilliseconds(1500),
        Attacks = [MeleeAttack.Forward, MeleeAttack.Left, MeleeAttack.Right], RespawnDelay = TimeSpan.FromSeconds(30),
    };

    public static readonly MobKind Zombie = new()
    {
        Key = "zombie", DisplayName = "Zombie", Instance = GameNpcs.ZOMBIE01, Faction = Faction.Undead,
        Behaviour = MobBehaviours.Hunter(remember: 4, wander: 150),
        Health = 300, Strength = 70, SightRange = new(1000), AttackRange = new(180), AttackCooldown = TimeSpan.FromMilliseconds(2000),
        Combo = 0, RespawnDelay = TimeSpan.FromSeconds(30),
    };

    public static readonly MobKind OrcWarrior = new()
    {
        Key = "ork", DisplayName = "Ork wojownik", Instance = GameNpcs.ORCWARRIOR_ROAM, Faction = Faction.Orcs, Humanoid = true,
        Behaviour = MobBehaviours.PackHunter(leader: true, flank: false, circles: false, leash: 3200),
        Health = 420, Strength = 100, Skill = 60, MeleeWeapon = GameItems.ITMW_2H_ORCAXE_02, MeleeMode = WeaponMode.TwoHandedSword,
        SightRange = new(1700), AttackRange = new(240), AttackCooldown = TimeSpan.FromMilliseconds(1800),
        Attacks = [MeleeAttack.Forward, MeleeAttack.Left, MeleeAttack.Right], RespawnDelay = TimeSpan.FromSeconds(50),
    };

    public static readonly MobKind Goblin = new()
    {
        Key = "goblin", DisplayName = "Goblin", Instance = GameNpcs.GOBBO_GREEN, Faction = Faction.Beasts,
        Behaviour = MobBehaviours.PackHunter(flee: 0.4f, wander: 450),
        Health = 70, Strength = 25, SightRange = new(1300), AttackRange = new(160), AttackCooldown = TimeSpan.FromMilliseconds(1000),
        Combo = 0, RespawnDelay = TimeSpan.FromSeconds(15),
    };

    public static readonly MobKind Sheep = new()
    {
        Key = "owca", DisplayName = "Owca", Instance = GameNpcs.SHEEP, Faction = Faction.Prey,
        Behaviour = MobBehaviours.Critter(), Health = 60, RespawnDelay = TimeSpan.FromSeconds(20),
    };

    public static readonly MobKind Drake = new()
    {
        Key = "smok", DisplayName = "Smok Ognia", Instance = GameNpcs.ARENA_DRAKE, Faction = Faction.Beasts,
        Behaviour = MobBehaviours.Drake(), Health = DrakeSettings.Health, Scale = DrakeSettings.Scale, Damage = 0,
        SightRange = DrakeSettings.Aggro, AttackRange = DrakeSettings.BiteRange, Radius = 250, Combo = 0,
        RespawnDelay = DrakeSettings.Respawn, FirstSpawn = DrakeSettings.FirstSpawn,
    };

    public static readonly MobKind Baron = new()
    {
        Key = "baron", DisplayName = "Baron Nashor", Instance = GameNpcs.ARENA_BARON, Faction = Faction.Beasts,
        Behaviour = MobBehaviours.Baron(), Health = BaronSettings.Health, Scale = BaronSettings.Scale, Damage = 0,
        ArmorValue = BaronSettings.Armor, ResistValue = BaronSettings.MagicResist,
        SightRange = BaronSettings.Aggro, AttackRange = BaronSettings.AttackRange, Radius = BaronSettings.Radius, Combo = 0,
        RespawnDelay = BaronSettings.Respawn, FirstSpawn = BaronSettings.FirstSpawn,
    };

    public static readonly MobKind JungleGolem = new()
    {
        Key = "lodowy-golem", DisplayName = "Lodowy Golem", Instance = GameNpcs.ICEGOLEM, Faction = Faction.Beasts,
        Behaviour = MobBehaviours.Jungle(leader: true), Health = 2200, Damage = 75, Scale = 0.9f, SightRange = new(1300), AttackRange = new(300),
        AttackCooldown = TimeSpan.FromMilliseconds(2200), Combo = 0, RespawnDelay = TimeSpan.FromMinutes(5), Gold = 70, Xp = 100, Buff = JungleBuff.Blue,
    };

    public static readonly MobKind JungleWarg = new()
    {
        Key = "dziki-warg", DisplayName = "Wielki Warg", Instance = GameNpcs.WARG, Faction = Faction.Beasts,
        Behaviour = MobBehaviours.Jungle(leader: true), Health = 1300, Damage = 60, SightRange = new(1300), AttackRange = new(210),
        AttackCooldown = TimeSpan.FromMilliseconds(1100), Combo = 0, RespawnDelay = TimeSpan.FromSeconds(100), Gold = 45, Xp = 45,
    };

    public static readonly MobKind JungleWolf = JungleWarg with
    {
        Key = "dziki-wilk", DisplayName = "Wilk", Instance = GameNpcs.WOLF, Behaviour = MobBehaviours.Jungle(), Health = 500, Damage = 28,
        AttackRange = new(170), AttackCooldown = TimeSpan.FromMilliseconds(900), Gold = 15, Xp = 20,
    };

    public static readonly MobKind JungleScavenger = new()
    {
        Key = "wielki-scierwojad", DisplayName = "Wielki Ścierwojad", Instance = GameNpcs.SCAVENGER, Faction = Faction.Beasts,
        Behaviour = MobBehaviours.Jungle(leader: true), Health = 1100, Damage = 55, Scale = 1.4f, SightRange = new(1300), AttackRange = new(230),
        AttackCooldown = TimeSpan.FromMilliseconds(1400), Combo = 0, RespawnDelay = TimeSpan.FromSeconds(100), Gold = 40, Xp = 40,
    };

    public static readonly MobKind JungleScavengerSmall = JungleScavenger with
    {
        Key = "maly-scierwojad", DisplayName = "Ścierwojad", Behaviour = MobBehaviours.Jungle(), Health = 350, Damage = 25, Scale = 0.8f,
        AttackRange = new(170), AttackCooldown = TimeSpan.FromMilliseconds(1200), Gold = 12, Xp = 15,
    };

    public static readonly MobKind JungleShadowbeast = new()
    {
        Key = "cieniostwor", DisplayName = "Cieniostwór", Instance = GameNpcs.SHADOWBEAST, Faction = Faction.Beasts,
        Behaviour = MobBehaviours.Jungle(leader: true), Health = 2100, Damage = 80, Scale = 1.1f, SightRange = new(1300), AttackRange = new(260),
        AttackCooldown = TimeSpan.FromMilliseconds(1900), Combo = 0, RespawnDelay = TimeSpan.FromMinutes(5), Gold = 70, Xp = 100, Buff = JungleBuff.Red,
    };

    public static readonly MobKind JungleSnapper = new()
    {
        Key = "zebacz", DisplayName = "Wielki Zębacz", Instance = GameNpcs.SNAPPER, Faction = Faction.Beasts,
        Behaviour = MobBehaviours.Jungle(leader: true), Health = 1800, Damage = 65, Scale = 1.3f, SightRange = new(1300), AttackRange = new(240),
        AttackCooldown = TimeSpan.FromMilliseconds(1700), Combo = 0, RespawnDelay = TimeSpan.FromSeconds(100), Gold = 55, Xp = 55,
    };

    public static readonly MobKind JungleSnapperSmall = JungleSnapper with
    {
        Key = "maly-zebacz", DisplayName = "Młody Zębacz", Behaviour = MobBehaviours.Jungle(), Health = 650, Damage = 35, Scale = 0.9f,
        AttackRange = new(190), AttackCooldown = TimeSpan.FromMilliseconds(1400), Gold = 18, Xp = 30,
    };

    public static readonly MobKind JungleStoneGolem = new()
    {
        Key = "kamienny-golem", DisplayName = "Kamienny Golem", Instance = GameNpcs.STONEGOLEM, Faction = Faction.Beasts,
        Behaviour = MobBehaviours.Jungle(leader: true), Health = 1900, Damage = 65, Scale = 0.9f, SightRange = new(1300), AttackRange = new(280),
        AttackCooldown = TimeSpan.FromMilliseconds(2000), Combo = 0, RespawnDelay = TimeSpan.FromSeconds(100), Gold = 55, Xp = 55,
    };

    public static readonly MobKind JungleStoneGolemSmall = JungleStoneGolem with
    {
        Key = "maly-kamienny-golem", DisplayName = "Mały Golem", Behaviour = MobBehaviours.Jungle(), Health = 700, Damage = 35, Scale = 0.55f,
        AttackRange = new(200), AttackCooldown = TimeSpan.FromMilliseconds(1500), Gold = 18, Xp = 30,
    };

    public static readonly IReadOnlyList<MobKind> All =
        [Bandit, BanditLeader, BanditArcher, Wolf, Warg, Scavenger, Skeleton, Zombie, OrcWarrior, Goblin, Sheep, Drake, Baron,
         JungleGolem, JungleWarg, JungleWolf, JungleScavenger, JungleScavengerSmall, JungleShadowbeast,
         JungleSnapper, JungleSnapperSmall, JungleStoneGolem, JungleStoneGolemSmall];

    public static MobKind? Find(string key) => All.FirstOrDefault(kind => string.Equals(kind.Key, key, StringComparison.OrdinalIgnoreCase));
}
