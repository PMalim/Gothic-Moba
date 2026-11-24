using BotArena.Ai.Lanes;
using BotArena.GameData;

namespace BotArena.Ai.Kinds;

public static class StructureKinds
{
    public static readonly MobKind BlueNexusTurret = MinionKinds.BlueTurret with
    {
        Key = "wieza-nexus-n", DisplayName = "Wieża nexusa niebieskich", Health = LaneSettings.NexusTurretHealth,
    };

    public static readonly MobKind RedNexusTurret = MinionKinds.RedTurret with
    {
        Key = "wieza-nexus-c", DisplayName = "Wieża nexusa czerwonych", Health = LaneSettings.NexusTurretHealth,
    };

    public static readonly MobKind BlueInhibitor = new()
    {
        Key = "inhibitor-n", DisplayName = "Inhibitor niebieskich", Instance = GameNpcs.STRUCT_INHIBITOR_BLUE, Faction = Faction.Blue,
        Behaviour = MobBehaviours.Structure(), Immobile = true, Structure = StructureKind.Inhibitor, Respawns = true,
        Health = LaneSettings.InhibitorHealth, Radius = LaneSettings.InhibitorRadius, RespawnDelay = LaneSettings.InhibitorRespawn,
    };

    public static readonly MobKind RedInhibitor = BlueInhibitor with
    {
        Key = "inhibitor-c", DisplayName = "Inhibitor czerwonych", Instance = GameNpcs.STRUCT_INHIBITOR_RED, Faction = Faction.Red,
    };

    public static readonly MobKind BlueNexus = new()
    {
        Key = "nexus-n", DisplayName = "Nexus niebieskich", Instance = GameNpcs.STRUCT_NEXUS_BLUE, Faction = Faction.Blue,
        Behaviour = MobBehaviours.Structure(), Immobile = true, Structure = StructureKind.Nexus,
        Health = LaneSettings.NexusHealth, Radius = LaneSettings.NexusRadius,
    };

    public static readonly MobKind RedNexus = BlueNexus with
    {
        Key = "nexus-c", DisplayName = "Nexus czerwonych", Instance = GameNpcs.STRUCT_NEXUS_RED, Faction = Faction.Red,
    };

    public static readonly MobKind BlueDragon = new()
    {
        Key = "smok-n", DisplayName = "Smok niebieskich", Instance = GameNpcs.STRUCT_DRAGON_BLUE, Faction = Faction.Blue,
        Behaviour = MobBehaviours.Dragon(), Immobile = true, Structure = StructureKind.Dragon,
        Health = 100000, Radius = 100, SightRange = LaneSettings.DragonRange,
    };

    public static readonly MobKind RedDragon = BlueDragon with
    {
        Key = "smok-c", DisplayName = "Smok czerwonych", Instance = GameNpcs.STRUCT_DRAGON_RED, Faction = Faction.Red,
    };

    public static MobKind Of(TurretDef def)
    {
        bool blue = def.Team == Faction.Blue;
        return def.Kind switch
        {
            StructureKind.Inhibitor => blue ? BlueInhibitor : RedInhibitor,
            StructureKind.Nexus => blue ? BlueNexus : RedNexus,
            StructureKind.Dragon => blue ? BlueDragon : RedDragon,
            _ when def.Tier == 0 => blue ? BlueNexusTurret : RedNexusTurret,
            _ => MinionKinds.TurretOf(def.Team),
        };
    }
}
