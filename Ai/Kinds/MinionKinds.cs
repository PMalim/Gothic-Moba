using BotArena.GameData;

namespace BotArena.Ai.Kinds;

public static class MinionKinds
{
    public static readonly MobKind BlueMelee = new()
    {
        Key = "minion-n-wojownik", DisplayName = "Siepacz (N)", Instance = GameNpcs.MINIONORC_BLUE, Faction = Faction.Blue, Humanoid = false,
        Behaviour = MobBehaviours.LaneMelee(),
        Scale = LaneSettings.MinionScale,
        Role = MinionRole.Melee, Health = MinionStats.BaseHealth(MinionRole.Melee), Damage = (int)MinionStats.BaseAd(MinionRole.Melee),
        Strength = 70, Skill = 60, MeleeMode = WeaponMode.Fists,
        SightRange = new(1100), AttackRange = new(140), AttackCooldown = LaneSettings.MeleeCooldown,
        Attacks = [MeleeAttack.Forward],
        Gold = LaneSettings.MeleeGold,
    };

    public static readonly MobKind BlueCaster = BlueMelee with
    {
        Key = "minion-n-szaman", DisplayName = "Mag (N)", Instance = GameNpcs.MINIONMAGE_BLUE,
        Behaviour = MobBehaviours.LaneCaster(), Role = MinionRole.Caster, Health = MinionStats.BaseHealth(MinionRole.Caster),
        Damage = (int)MinionStats.BaseAd(MinionRole.Caster), Strength = 40,
        Gold = LaneSettings.CasterGold,
    };

    public static readonly MobKind BlueCannon = BlueCaster with
    {
        Key = "minion-n-kanonier", DisplayName = "Armata (N)", Instance = GameNpcs.MINIONCANNON_BLUE,
        Behaviour = MobBehaviours.LaneCannon(), Role = MinionRole.Cannon, Damage = (int)MinionStats.BaseAd(MinionRole.Cannon), Health = MinionStats.BaseHealth(MinionRole.Cannon),
        Scale = LaneSettings.CannonScale, Radius = LaneSettings.CannonRadius, Strength = 60,
        SightRange = new(1800),
        Gold = LaneSettings.CannonGold,
    };

    public static readonly MobKind RedMelee = BlueMelee with
    {
        Key = "minion-c-wojownik", DisplayName = "Siepacz (C)", Instance = GameNpcs.MINIONORC_RED, Faction = Faction.Red,
    };

    public static readonly MobKind RedCaster = BlueCaster with
    {
        Key = "minion-c-szaman", DisplayName = "Mag (C)", Instance = GameNpcs.MINIONMAGE_RED, Faction = Faction.Red,
    };

    public static readonly MobKind RedCannon = BlueCannon with
    {
        Key = "minion-c-kanonier", DisplayName = "Armata (C)", Instance = GameNpcs.MINIONCANNON_RED, Faction = Faction.Red,
    };

    public static readonly MobKind BlueTurret = new()
    {
        Key = "wieza-n", DisplayName = "Wieża niebieskich", Instance = GameNpcs.TOWER_BLUE, Faction = Faction.Blue,
        Behaviour = MobBehaviours.Turret(), Immobile = true, Structure = StructureKind.Turret, Health = LaneSettings.TurretHealth,
        Radius = LaneSettings.TurretRadius, SightRange = LaneSettings.TurretSight,
    };

    public static readonly MobKind RedTurret = BlueTurret with
    {
        Key = "wieza-c", DisplayName = "Wieża czerwonych", Instance = GameNpcs.TOWER_RED, Faction = Faction.Red,
    };

    public static MobKind TurretOf(Faction team) => team == Faction.Blue ? BlueTurret : RedTurret;

    public static MobKind Of(Faction team, MinionRole role) => (team, role) switch
    {
        (Faction.Blue, MinionRole.Melee) => BlueMelee,
        (Faction.Blue, MinionRole.Caster) => BlueCaster,
        (Faction.Red, MinionRole.Melee) => RedMelee,
        (Faction.Red, MinionRole.Caster) => RedCaster,
        (Faction.Blue, MinionRole.Cannon) => BlueCannon,
        (Faction.Red, MinionRole.Cannon) => RedCannon,
        _ => throw new ArgumentOutOfRangeException(nameof(team), team, "Drużyną jest tylko Blue albo Red."),
    };
}
