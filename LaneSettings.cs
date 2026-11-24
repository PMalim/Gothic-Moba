using BotArena.Ai.Lanes;

namespace BotArena;

public enum MinionRole { Melee, Caster, Cannon }

public static class LaneSettings
{
    private static readonly Position BlueSpawn = new(9867, 572, 10185), RedSpawn = new(-9656, 576, -9873);
    private static Position Mirror(Position p) => new(BlueSpawn.X + RedSpawn.X - p.X, p.Y, BlueSpawn.Z + RedSpawn.Z - p.Z);
    private static readonly (float X, float Z) RedBaseShift = (272f, -535f);

    private static TurretDef Opposite(TurretDef blue)
    {
        Position at = Mirror(blue.At);
        if (blue.Kind is StructureKind.Inhibitor or StructureKind.Nexus || (blue.Kind == StructureKind.Turret && blue.Tier is 0 or 3))
            at = new Position(at.X + RedBaseShift.X, at.Y, at.Z + RedBaseShift.Z);
        return blue with { Team = Faction.Red, At = at, Angle = (blue.Angle + 180f) % 360f };
    }

    private static readonly TurretDef[] BlueStructures =
    [
        new(Faction.Blue, new Position(3561, 129, 3600), 211, StructureKind.Turret, 2),
        new(Faction.Blue, new Position(6470, 187, 6122), 229, StructureKind.Turret, 3),
        new(Faction.Blue, new Position(6894, 187, 6667), 35, StructureKind.Inhibitor),
        new(Faction.Blue, new Position(7988, 187, 8609), 222, StructureKind.Turret, 0),
        new(Faction.Blue, new Position(8710, 187, 7691), 233, StructureKind.Turret, 0),
        new(Faction.Blue, new Position(8623, 187, 8578), 221, StructureKind.Nexus),
        new(Faction.Blue, new Position(10179, 499, 10415), 228, StructureKind.Dragon),
    ];

    private static readonly TurretDef[] Structures =
    [
        new(Faction.Blue, new Position(1864, 129, 363), 258, StructureKind.Turret, 1),
        new(Faction.Red, new Position(-1088, 129, -544), 68, StructureKind.Turret, 1),
        .. BlueStructures,
        .. BlueStructures.Select(Opposite),
    ];

    private static readonly TurretDef[] BottomBlue =
    [
        new(Faction.Blue, new Position(-7542, 129, 10339), 208, StructureKind.Turret, 1),
        new(Faction.Blue, new Position(-1528, 129, 10423), 259, StructureKind.Turret, 2),
        new(Faction.Blue, new Position(5266, 187, 10486), 268, StructureKind.Turret, 3),
        new(Faction.Blue, new Position(5947, 187, 10424), 269, StructureKind.Inhibitor),
    ];

    private static readonly TurretDef[] TopBlue =
    [
        new(Faction.Blue, new Position(9810, 129, -8145), 234, StructureKind.Turret, 1),
        new(Faction.Blue, new Position(10973, 129, -2023), 203, StructureKind.Turret, 2),
        new(Faction.Blue, new Position(10422, 187, 5181), 180, StructureKind.Turret, 3),
        new(Faction.Blue, new Position(10268, 187, 5698), 169, StructureKind.Inhibitor),
    ];

    private static readonly TurretDef[] BottomRed =
    [
        new(Faction.Red, new Position(-10236, 157, 6972), 38, StructureKind.Turret, 1),
        new(Faction.Red, new Position(-10120, 279, 1777), 7, StructureKind.Turret, 2),
        Opposite(TopBlue[2]),
        Opposite(TopBlue[3]),
    ];

    private static Position Aside(Position at, float dx, float dz) => new(at.X + dx, at.Y, at.Z + dz);

    private static readonly Position[] BottomRoute =
    [
        new(9071, 422, 10351),
        new(5583, 187, 10035),
        new(688, 129, 11027),
        new(-6461, 129, 10514),
        new(-8112, 129, 8542),
        new(-9599, 257, 6108),
        new(-9570, 279, -2),
        new(-9714, 159, -3877),
        new(-9095, 187, -5926),
        new(-10153, 430, -8253),
    ];

    private static readonly Position[] TopRoute =
    [
        new(10342, 422, 8427),
        new(9918, 187, 5985),
        new(10283, 187, 4415),
        new(10432, 129, -1528),
        new(9974, 129, -7028),
        new(8299, 129, -9222),
        new(-4508, 165, -10869),
        new(-4942, 187, -10183),
        new(-7473, 430, -10455),
    ];

    private static readonly TurretDef[] TopRed =
    [
        new(Faction.Red, new Position(5988, 164, -10729), 58, StructureKind.Turret, 1),
        Opposite(BottomBlue[1]),
        Opposite(BottomBlue[2]),
        Opposite(BottomBlue[3]),
    ];

    public static readonly LaneDef[] Lanes =
    [
        new("Środek",
        [
            new Position(9867, 572, 10185),
            new Position(5487, 129, 5218),
            new Position(3045, 129, 2540),
            new Position(1698, 129, 991),
            new Position(-46, 129, -511),
            new Position(-1879, 279, -2073),
            new Position(-4602, 179, -5392),
            new Position(-6562, 187, -7339),
            new Position(-9656, 576, -9873),
        ],
        Structures),
        new("Dolna", BottomRoute, [.. BottomBlue, .. BottomRed]),
        new("Górna", TopRoute, [.. TopBlue, .. TopRed]),
    ];

    public const int TurretHealth = 3500;
    public const int NexusTurretHealth = 2700, InhibitorHealth = 4000, NexusHealth = 5500;
    public static readonly TimeSpan InhibitorRespawn = TimeSpan.FromMinutes(5);
    public const float InhibitorRadius = 140, NexusRadius = 190;
    public static readonly Distance DragonRange = new(2000);
    public static readonly TimeSpan DragonCooldown = TimeSpan.FromMilliseconds(1200);
    public static readonly TimeSpan DragonShotDelay = TimeSpan.FromMilliseconds(350);
    public const int DragonDamage = 99999;
    public static readonly Distance TurretRange = new(1100);
    public static Distance TowerHitRange => new(MathF.Min(MaxAttackRange.Centimeters, TurretRange.Centimeters));
    public static readonly TimeSpan TurretStreakWindow = TimeSpan.FromSeconds(6);
    public static Distance MaxAttackRange => new(1200);
    public const float AttackRangeSlack = 50;
    public static Distance TurretSight => new(TurretRange.Centimeters + TurretRadius + 10);
    public static readonly TimeSpan TurretCooldown = TimeSpan.FromMilliseconds(2500);
    public static readonly TimeSpan TurretShotDelay = TimeSpan.FromMilliseconds(700);
    public const float TurretRadius = 95;

    public static readonly bool Enabled = true;
    public static readonly bool WavesEnabled = true;
    public static readonly bool TurretsEnabled = true;

    public static readonly TimeSpan FirstWave = TimeSpan.FromSeconds(65);
    public const bool RepeatWaves = true;
    public static readonly TimeSpan WaveInterval = TimeSpan.FromSeconds(30);
    public static readonly MinionRole[] Wave =
    [
        MinionRole.Melee, MinionRole.Melee, MinionRole.Melee,
        MinionRole.Caster, MinionRole.Caster, MinionRole.Caster,
    ];
    public static readonly TimeSpan WaveStepGap = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan WaveGroupGap = TimeSpan.FromSeconds(2);
    public const int MaxPerTeam = 36;

    public static readonly bool AttackPlayers = true;
    public const float PlayerNoticeRange = 300;
    public static readonly Distance EngageRange = new(1100);
    public static readonly Distance StructureNotice = new(1900);

    public const float PaceSpeed = 250;
    public const float PaceFactor = 0.95f;
    public const float PaceMargin = 120;
    public const bool HoldWalks = true;
    public static readonly TimeSpan HoldCheck = TimeSpan.FromMilliseconds(100);
    public static readonly Distance ContactRange = new(900);
    public const float WaypointReach = 250;
    public const float ColumnSpread = 110;

    public const float OrcScale = 0.5f;
    public const float MinionScale = 0.7f;

    public const int PlayerMaxHit = 250;
    public static readonly TimeSpan HitGap = TimeSpan.FromMilliseconds(150);
    public static readonly TimeSpan AutoAttackGap = TimeSpan.FromMilliseconds(150);
    public const float MaxHitDistance = 12000;
    public const int MeleeGold = 20, CasterGold = 17, CannonGold = 60;
    public const int TowerGold = 500;
    public static readonly TimeSpan TowerGoldWindow = TimeSpan.FromSeconds(20);
    public const float TowerGoldRadius = 2000f;
    public static readonly TimeSpan MeleeCooldown = TimeSpan.FromMilliseconds(1400);

    public static readonly Distance SpellRange = new(900);
    public static readonly TimeSpan SpellCooldown = TimeSpan.FromMilliseconds(3000);
    public const float BoltSpeed = 1000f;
    public static readonly TimeSpan BoltCastDelay = TimeSpan.FromMilliseconds(480);
    public const int SpellSlot = 0;

    public const int CannonEvery = 3;
    public const float CannonScale = 0.95f;
    public const float CannonRadius = 110;
    public static readonly Distance CannonRange = TowerHitRange;
    public static readonly TimeSpan CannonCooldown = TimeSpan.FromMilliseconds(4500);
    public const float CannonBallSpeed = 650f;
    public const bool LogSpells = true;
}
