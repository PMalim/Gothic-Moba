namespace BotArena.Ai.Lanes;

public static class MinionStats
{
    public const double UpgradeSeconds = 90;
    public const float StructureChampionFactor = 0.6f;

    public static int BaseHealth(MinionRole role) => role switch { MinionRole.Melee => 465, MinionRole.Caster => 284, _ => 835 };
    public static float BaseAd(MinionRole role) => role switch { MinionRole.Melee => 11f, MinionRole.Caster => 21f, _ => 37.5f };

    private static (float Hp, float Ad) Growth(MinionRole role) => role switch
    {
        MinionRole.Melee => (22f, 0.5f),
        MinionRole.Caster => (13f, 0.9f),
        _ => (40f, 1.5f),
    };

    public static int HealthAt(MinionRole role, int upgrades) => (int)Math.Round(BaseHealth(role) + Growth(role).Hp * Math.Max(0, upgrades));
    public static float AdAt(MinionRole role, int upgrades) => BaseAd(role) + Growth(role).Ad * Math.Max(0, upgrades);

    public static int StructureArmor(StructureKind kind) => kind switch { StructureKind.Turret => 40, StructureKind.Inhibitor or StructureKind.Nexus => 20, _ => 0 };

    public static float VsMinionBonus(MinionRole role) => role switch { MinionRole.Melee => 0.02f, MinionRole.Caster => 0.035f, _ => 0.05f };

    public static float TowerShotShare(MinionRole role) => role switch { MinionRole.Melee => 0.45f, MinionRole.Caster => 0.70f, _ => 0.14f };

    public static float TowerAd(double minutes) => (float)Math.Min(TowerMaxAd, TowerBaseAd + TowerAdPerMinute * Math.Max(0, minutes));
    public const float TowerBaseAd = 182f, TowerMaxAd = 350f, TowerAdPerMinute = 8.8f;
    public const float TowerStackBonus = 0.4f;
    public const int TowerMaxStacks = 3;
}
